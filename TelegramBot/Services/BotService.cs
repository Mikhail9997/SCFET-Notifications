using System.Net;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.ReplyMarkups;
using TelegramBot.Models;

namespace TelegramBot.Services;

public class BotService
{
    private readonly TelegramBotClient  _botClient;
    private readonly ApiService _apiService;
    private readonly Dictionary<long, BotUserState> _userStates;
    private readonly ILogger<BotService> _logger;
    private readonly string _appUrl;
    
    public BotService(string botToken, string apiBaseUrl, ILogger<BotService> logger, IConfiguration configuration)
    {
        _logger = logger;
        
        bool useProxy = configuration.GetValue<bool>("TelegramBot:UseProxy", false);
        string? proxyHost = configuration["TelegramBot:Proxy:Host"];
        int? proxyPort = configuration.GetValue<int?>("TelegramBot:Proxy:Port");
        string? proxyType = configuration["TelegramBot:Proxy:Type"] ?? "http";
        
        HttpMessageHandler httpMessageHandler;
    
        if (useProxy && !string.IsNullOrEmpty(proxyHost) && proxyPort.HasValue)
        {
            _logger.LogInformation($"Using {proxyType} proxy: {proxyHost}:{proxyPort}");
        
            if (proxyType.ToLower() == "socks5")
            {
                // Для SOCKS5 используем SocketsHttpHandler
                var proxy = new WebProxy($"socks5://{proxyHost}:{proxyPort.Value}")
                {
                    BypassProxyOnLocal = false
                };
            
                httpMessageHandler = new SocketsHttpHandler
                {
                    Proxy = proxy,
                    UseProxy = true,
                    ConnectTimeout = TimeSpan.FromSeconds(30), 
                    PooledConnectionLifetime = TimeSpan.FromMinutes(5),
                    PooledConnectionIdleTimeout = TimeSpan.FromMinutes(2),
                    MaxConnectionsPerServer = 10,
                    SslOptions = new System.Net.Security.SslClientAuthenticationOptions
                    {
                        RemoteCertificateValidationCallback = (sender, certificate, chain, sslPolicyErrors) => true
                    }
                };
            }
            else
            {
                var proxy = new WebProxy(proxyHost, proxyPort.Value);
                httpMessageHandler = new HttpClientHandler
                {
                    Proxy = proxy,
                    UseProxy = true
                };
            }
        }
        else
        {
            httpMessageHandler = new HttpClientHandler();
        }
    
        var httpClient = new HttpClient(httpMessageHandler)
        {
            Timeout = TimeSpan.FromSeconds(60)
        };
    
        _botClient = new TelegramBotClient(botToken, httpClient);
        _apiService = new ApiService(apiBaseUrl);
        _userStates = new Dictionary<long, BotUserState>();
        _appUrl = configuration["App_Url"] ?? "";
    }
    
    public async Task StartAsync()
    {
        _botClient.StartReceiving(HandleUpdateAsync, HandleErrorAsync);
            
        var me = await _botClient.GetMe();
        _logger.LogInformation($"Bot @{me.Username} started successfully!");

        // Запускаем очистку неактивных сессий
        _ = Task.Run(CleanupInactiveSessions);
    }

    private async Task HandleUpdateAsync(ITelegramBotClient botClient, Update update, CancellationToken cancellationToken)
    {
        try
        {
            if (update.Message?.Text == "/start" || update.Message?.Text == "/register")
            {
                await StartRegistration(update.Message.Chat.Id, update.Message.MessageId);

                return;
            }

            if (update.Message?.Text == "/info")
            {
                var from = update.Message.From;
                await SendMessage(update.Message.Chat.Id, 
                    $"👤 Ваш ID: {from.Id}\n" +
                    $"📝 Username: @{from.Username}\n" +
                    $"👤 Имя: {from.FirstName}\n" +
                    $"💬 Chat ID: {update.Message.Chat.Id}");
                return;
            }
            
            var chatId = update.Message?.Chat.Id ?? update.CallbackQuery?.Message?.Chat.Id ?? 0;
            
            if(chatId == 0) return;

            if (!_userStates.TryGetValue(chatId, out var userState))
            {
                await SendMessage(chatId, "Пожалуйста, начните регистрацию с помощью команды /start или /register");
                return;
            }

            userState.LastActivity = DateTime.UtcNow;

            //Обрабатываем текущее состояния пользователя в регистрации
            switch (userState.State)
            {
                case RegistrationState.Start:
                    await StartRegistration(chatId, update.Message.MessageId);
                    break;
                case RegistrationState.WaitingForEmail:
                    await ProcessEmail(chatId, update.Message!.Text!);
                    break;
                
                case RegistrationState.WaitingForFirstName:
                    await ProcessFirstName(chatId, update.Message!.Text!);
                    break;
                
                case RegistrationState.WaitingForLastName:
                    await ProcessLastName(chatId, update.Message!.Text!);
                    break;
                
                case RegistrationState.WaitingForPassword:
                    await ProcessPassword(chatId, update.Message!.Text!);
                    break;
                
                case RegistrationState.WaitingForGroupSelection:
                    if (update.CallbackQuery != null)
                    {
                        await ProcessGroupSelection(chatId, update.CallbackQuery.Data!, update.CallbackQuery.Message!.MessageId, update.CallbackQuery.From.Id);
                    }
                    break;
            }
        }
        catch(Exception ex)
        {
            _logger.LogError(ex, "Error handling update");
        }
    }

    private async Task StartRegistration(long chatId, int messageId)
    {
        var userState = new BotUserState()
        {
            ChatId = chatId,
            State = RegistrationState.WaitingForEmail
        };

        _userStates[chatId] = userState;
        await SendMessage(chatId, 
            "🎓 Добро пожаловать в систему уведомлений СКФЭТ!\n\n" +
            "Давайте зарегистрируем вас в системе.\n\n" +
            "📧 Пожалуйста, введите ваш email:");
    }

    private async Task ProcessEmail(long chatId, string email)
    {
        if (!IsValidEmail(email))
        {
            await SendMessage(chatId, "❌ Неверный формат email. Пожалуйста, введите корректный email:");
            return;
        }
        
        // Проверяем, существует ли email
        var emailExists = await _apiService.CheckEmailExistsAsync(email);
        if (emailExists)
        {
            await SendMessage(chatId, "❌ Пользователь с таким email уже зарегистрирован. Пожалуйста, введите другой email:");
            return;
        }

        _userStates[chatId].Email = email;
        _userStates[chatId].State = RegistrationState.WaitingForFirstName;
        
        await SendMessage(chatId, "✅ Email принят!\n\n👤 Теперь введите ваше имя:");
    }

    private async Task ProcessFirstName(long chatId, string firstName)
    {
        if (string.IsNullOrWhiteSpace(firstName) || firstName.Length < 2)
        {
            await SendMessage(chatId, "❌ Имя должно содержать хотя бы 2 символа. Пожалуйста, введите ваше имя:");
            return;
        }

        _userStates[chatId].FirstName = firstName;
        _userStates[chatId].State = RegistrationState.WaitingForLastName;
        
        await SendMessage(chatId, "✅ Имя принято!\n\n👤 Теперь введите вашу фамилию:");
    }

    private async Task ProcessLastName(long chatId, string lastName)
    {
        if (string.IsNullOrWhiteSpace(lastName) || lastName.Length < 2)
        {
            await SendMessage(chatId, "❌ Фамилия должна содержать хотя бы 2 символа. Пожалуйста, введите вашу фамилию:");
            return;
        }
        
        _userStates[chatId].LastName = lastName.Trim();
        _userStates[chatId].State = RegistrationState.WaitingForPassword;

        await SendMessage(chatId, 
            "✅ Фамилия принята!\n\n" +
            "🔐 Теперь придумайте пароль (минимум 6 символов):");
    }

    private async Task ProcessPassword(long chatId, string password)
    {
        if (string.IsNullOrWhiteSpace(password) || password.Length < 6)
        {
            await SendMessage(chatId, "❌ Пароль должен содержать минимум 6 символов. Пожалуйста, придумайте пароль:");
            return;
        }

        _userStates[chatId].Password = password;
        _userStates[chatId].State = RegistrationState.WaitingForGroupSelection;
        
        await ShowGroupSelection(chatId);
    }

    private async Task ShowGroupSelection(long chatId)
    {
        var groups = await _apiService.GetGroupsAsync();

        if (groups == null || !groups.Any())
        {
            await SendMessage(chatId, "❌ В системе пока нет доступных групп. Обратитесь к администратору.");
            _userStates.Remove(chatId);
            return;
        }

        var keyboard = new List<List<InlineKeyboardButton>>();

        foreach (var group in groups)
        {
            keyboard.Add(new()
            {
                InlineKeyboardButton.WithCallbackData(
                    $"{group.Name} ({group.StudentCount} студентов)", 
                    $"group_{group.Id}")
            });
        }
        var replyMarkup = new InlineKeyboardMarkup(keyboard);
        
        await SendMessage(chatId, 
            "✅ Пароль принят!\n\n" +
            "🎯 Теперь выберите вашу группу из списка ниже:",
            replyMarkup);
    }

    private async Task ProcessGroupSelection(long chatId, string callbackData, int messageId, long userId)
    {
        if(!callbackData.StartsWith("group_")) return;
        
        var groupId = callbackData.Substring(6);
        var userState = _userStates[chatId];
        
        // Получаем группы для отображения названия
        var groups = await _apiService.GetGroupsAsync();
        var selectedGroup = groups?.FirstOrDefault(g => g.Id.ToString() == groupId);

        if (selectedGroup == null)
        {
            await SendMessage(chatId, "❌ Ошибка выбора группы. Попробуйте снова.");
            await ShowGroupSelection(chatId);
            return;
        }

        userState.SelectedGroup = groupId;
        userState.State = RegistrationState.Completed;
        
        // Удаляем клавиатуру
        await _botClient.EditMessageReplyMarkup(
            chatId, messageId, 
            replyMarkup: null);
        
        // Регистрируем пользователя
        var registerRequest = new RegisterRequest
        {
            Email = userState.Email!,
            Password = userState.Password!,
            ConfirmPassword = userState.Password!,
            FirstName = userState.FirstName!,
            LastName = userState.LastName!,
            Role = Enum.TryParse("Student", out UserRole role) ? role : UserRole.Student,
            GroupId = Guid.Parse(groupId),
            ChatId = chatId.ToString(),
            TelegramId = userId.ToString().ToLower()
        };
        
        var registrationResult = await _apiService.RegisterStudentAsync(registerRequest);
        
        if (registrationResult == RegistrationResult.Success)
        {
            await SendMessage(chatId,
                $"🎉 Регистрация успешно завершена!\n\n" +
                $"📧 Email: {userState.Email}\n" +
                $"👤 Имя: {userState.FirstName} {userState.LastName}\n" +
                $"🎯 Группа: {selectedGroup.Name}\n\n" +
                $"📱 Вы можете войти в мобильное приложение СКФЭТ с вашими учетными данными после проверки администрации.\n\n" +
                $"📲 Скачать приложение: {_appUrl}\n\n" +
                $"🔐 Логин: {userState.Email}\n" +
                $"🔑 Пароль: {userState.Password}\n\n" +
                $"⚠️ Сохраните эти данные в надежном месте!");

            // Очищаем состояние
            _userStates.Remove(chatId);
        }
        else
        {
            var message = registrationResult switch
            {
                RegistrationResult.DeviceTokenNullError => "❌ Произошла ошибка при регистрации...",
                RegistrationResult.DeviceTokenAlreadyExists => "❌ Устройство уже зарегистрировано",
                RegistrationResult.UnknownError => "❌ Произошла неизвестная ошибка...",
                _ => "❌ Неизвестный результат регистрации."
            };

            await SendMessage(chatId, message);
            
            // Сбрасываем состояние
            userState.State = RegistrationState.Start;
        }
        
    }
    
    public async Task OnUserIsActiveEvent(UserIsActiveMessage message)
    {
        try
        {
            var chatId = long.Parse(message.ChatId);
            
            if (message.IsActive)
            {
                await SendAccountApprovedMessage(chatId, message.FirstName, message.LastName, message.Email, message.Role);
            }
            else
            {
                await SendAccountRejectedMessage(chatId, message.FirstName, message.LastName, message.Email);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Ошибка при отправке уведомления для ChatId: {message.ChatId}");
        }
    }
    
    private async Task SendAccountApprovedMessage(long chatId, string firstName, string lastName, string email, string role)
    {
        var messageText = $"🎉 Ваш аккаунт активирован!\n\n" +
                          $"👤 Пользователь: {firstName} {lastName}\n" +
                          $"📧 Email: {email}\n" +
                          $"🎯 Роль: {role}\n\n" +
                          $"✅ Теперь вы можете полноценно использовать все возможности системы.\n\n" +
                          $"Спасибо за регистрацию!";

        await SendMessage(chatId, messageText);
    }

    private async Task SendAccountRejectedMessage(long chatId, string firstName, string lastName, string email)
    {
        var messageText = $"❌ Ваш аккаунт отклонен\n\n" +
                          $"👤 Пользователь: {firstName} {lastName}\n" +
                          $"📧 Email: {email}\n\n" +
                          $"⚠️ К сожалению, ваша регистрация не была одобрена администратором.\n\n" +
                          $"Если вы считаете, что это ошибка, пожалуйста, свяжитесь с поддержкой.";

        await SendMessage(chatId, messageText);
    }
    
    private async Task SendMessage(long chatId, string message, ReplyMarkup? replyMarkup = null)
    {
        try
        {
            await _botClient.SendMessage(
                chatId:chatId, 
                message, 
                replyMarkup:replyMarkup);
        }
        catch(Exception ex)
        {
            _logger.LogError(ex, "Error sending message to chat {ChatId}", chatId);
        }
    }

    private async Task CleanupInactiveSessions()
    {
        while (true)
        {
            await Task.Delay(TimeSpan.FromMinutes(10));

            var now = DateTime.UtcNow;
            var inactiveUsers = _userStates
                .Where(kvp => (now - kvp.Value.LastActivity) > (TimeSpan.FromMinutes(30)))
                .ToList();

            foreach (var (chatId, state) in inactiveUsers)
            {
                _userStates.Remove(chatId);
                
                try
                {
                    await SendMessage(chatId, 
                        "⏰ Ваша сессия регистрации истекла из-за неактивности.\n" +
                        "Для начала новой регистрации используйте команду /start или /register");
                }
                catch
                {
                    
                }
            }
        }
    }
    
    private bool IsValidEmail(string email)
    {
        try
        {
            var addr = new System.Net.Mail.MailAddress(email);
            return addr.Address == email;
        }
        catch
        {
            return false;
        }
    }
    
    private Task HandleErrorAsync(ITelegramBotClient arg1, Exception exception, CancellationToken arg3)
    {
        _logger.LogError(exception, "Telegram Bot error");
        return Task.CompletedTask;
    }
}
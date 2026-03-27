using System.Net;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using PhoneNumbers;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.ReplyMarkups;
using TelegramBot.Models;
using User = TelegramBot.Models.User;

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
            Message? message = update.Message;
            if (message != null && message.Text != null)
            {
                if (message.Text.StartsWith("/"))
                {
                    await HandleCommandAsync(message);
                    return;
                }
            }
            await HandleTextAsync(update);
        }
        catch(Exception ex)
        {
            _logger.LogError(ex, "Error handling update");
        }
    }
    
    private async Task HandleCommandAsync(Message message)
    {
        var chatId = message.Chat.Id;
        var command = message.Text;

        switch (command)
        {
            case "/start":
                await SendStartMessage(chatId);
                break;
            case "/registerAsStudent":
                await StartRegistration(chatId, UserRole.Student);
                break;
            case "/registerAsParent":
                await StartRegistration(chatId, UserRole.Parent);
                break;
            case "/info":
                await SendInfoMessage(message);
                break;
        }
    }

    private async Task HandleTextAsync(Update update)
    {
        var chatId = update.Message?.Chat.Id ?? update?.CallbackQuery?.Message?.Chat.Id ?? 0;
        
        if (!_userStates.TryGetValue(chatId, out var userState))
        {
            await SendMessage(chatId, "Пожалуйста, начните регистрацию с помощью команды /start");
            return;
        }

        userState.LastActivity = DateTime.UtcNow;

        //Обрабатываем текущее состояния пользователя в регистрации
        switch (userState.State)
        {
            case RegistrationState.WaitingForEmail:
                await ProcessEmail(chatId, update.Message!.Text!);
                break;
            case RegistrationState.WaitingForPhoneNumber:
                await ProcessPhoneNumber(chatId, update.Message!.Text!);
                break;
            case RegistrationState.WaitingForFirstName:
                await ProcessFirstName(chatId, update.Message!.Text!);
                break;
                
            case RegistrationState.WaitingForLastName:
                await ProcessLastName(chatId, update.Message!.Text!);
                break;
                
            case RegistrationState.WaitingForPassword:
                await ProcessPassword(chatId, update.Message!.Text!, update.Message!.From!.Id);
                break;
                
            case RegistrationState.WaitingForGroupSelection:
                if (update.CallbackQuery != null)
                {
                    await ProcessGroupSelection(chatId, update.CallbackQuery.Data!, update.CallbackQuery.Message!.MessageId, update.CallbackQuery.From.Id);
                }
                break;
            default:
                await SendMessage(chatId,"Я тебя не понимаю");
                break;
        }
    }
    
    private async Task StartRegistration(long chatId, UserRole role)
    {
        var userState = new BotUserState()
        {
            ChatId = chatId,
            State = RegistrationState.WaitingForEmail,
            Role = role
        };

        _userStates[chatId] = userState;
        await SendMessage(chatId, "📧 Пожалуйста, введите ваш email:");
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
        _userStates[chatId].State = RegistrationState.WaitingForPhoneNumber;
        
        await SendMessage(chatId, "✅ Email принят!\n\n📞 Теперь введите номер телефона в международном формате, например: +7 912 345-67-89:");
    }

    private async Task ProcessPhoneNumber(long chatId, string phoneNumber)
    {
        if (!IsValidPhoneNumber(phoneNumber))
        {
            await SendMessage(chatId, "❌ Неверный формат номера телефона. Пожалуйста, введите корректный номер телефона:");
            return;
        }
        
        // проверяем, существует ли номер телефона
        bool phoneNumberExist = await _apiService.CheckPhoneNumberExistsAsync(phoneNumber);
        if (phoneNumberExist)
        {
            await SendMessage(chatId, "❌ Пользователь с таким номером телефона уже зарегистрирован. Пожалуйста, введите другой номер телефона:");
            return;
        }
        
        _userStates[chatId].PhoneNumber = phoneNumber;
        _userStates[chatId].State = RegistrationState.WaitingForFirstName;
        
        await SendMessage(chatId, "✅ Номер телефона принят!\n\n👤 Теперь введите ваше имя:");
    }
    
    private async Task ProcessFirstName(long chatId, string firstName)
    {
        if (string.IsNullOrWhiteSpace(firstName) || firstName.Length < 2 || firstName.Length > 50)
        {
            await SendMessage(chatId, "❌ Имя должно содержать не менее 2 символа и не более 50 символов. Пожалуйста, введите ваше имя:");
            return;
        }

        _userStates[chatId].FirstName = firstName;
        _userStates[chatId].State = RegistrationState.WaitingForLastName;
        
        await SendMessage(chatId, "✅ Имя принято!\n\n👤 Теперь введите вашу фамилию:");
    }

    private async Task ProcessLastName(long chatId, string lastName)
    {
        if (string.IsNullOrWhiteSpace(lastName) || lastName.Length < 2 || lastName.Length > 50)
        {
            await SendMessage(chatId, "❌ Фамилия должна содержать не менее 2 символа и не более 50 символов. Пожалуйста, введите вашу фамилию:");
            return;
        }
        
        _userStates[chatId].LastName = lastName.Trim();
        _userStates[chatId].State = RegistrationState.WaitingForPassword;

        await SendMessage(chatId, 
            "✅ Фамилия принята!\n\n" +
            "🔐 Теперь придумайте пароль (минимум 6 символов):");
    }

    private async Task ProcessPassword(long chatId, string password, long userId)
    {
        if (string.IsNullOrWhiteSpace(password) || password.Length < 6)
        {
            await SendMessage(chatId, "❌ Пароль должен содержать минимум 6 символов. Пожалуйста, придумайте пароль:");
            return;
        }

        _userStates[chatId].Password = password;

        // группу могут иметь только студенты
        if (_userStates[chatId].Role == UserRole.Student)
        {
            _userStates[chatId].State = RegistrationState.WaitingForGroupSelection;
            await ShowGroupSelection(chatId, userId);
        }
        else
        {
            _userStates[chatId].State = RegistrationState.Completed;
            await ProcessRegistration(chatId, userId);
        }
    }

    private async Task ShowGroupSelection(long chatId, long userId)
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
            await ShowGroupSelection(chatId, userId);
            return;
        }

        userState.SelectedGroup = new Group()
        {
            Name = selectedGroup.Name,
            Id = selectedGroup.Id,
            StudentCount = selectedGroup.StudentCount
        };
        userState.State = RegistrationState.Completed;
        
        // Удаляем клавиатуру
        await _botClient.EditMessageReplyMarkup(
            chatId, messageId, 
            replyMarkup: null);
        
        await ProcessRegistration(chatId, userId);
    }

    private async Task ProcessRegistration(long chatId, long userId)
    {
        if (!_userStates.TryGetValue(chatId, out var userState))
        {
            _logger.LogWarning($"Registration state not found for chat {chatId}");
            await SendMessage(chatId, "❌ Сессия регистрации истекла. Пожалуйста, начните заново с команды /start");
            return;
        }
        
        // Регистрируем пользователя
        var registerRequest = new RegisterRequest
        {
            Email = userState.Email!,
            PhoneNumber = userState.PhoneNumber!,
            Password = userState.Password!,
            ConfirmPassword = userState.Password!,
            FirstName = userState.FirstName!,
            LastName = userState.LastName!,
            Role = userState.Role!.Value,
            GroupId = userState?.SelectedGroup?.Id ?? null,
            ChatId = chatId.ToString(),
            TelegramId = userId.ToString().ToLower()
        };
        
        var registrationResult = await _apiService.RegisterAsync(registerRequest);
        
        if (registrationResult == RegistrationResult.Success)
        {
            var message = $"🎉 Регистрация успешно завершена!\n\n" +
                          $"📧 Email: {userState.Email}\n" +
                          $"📞 Телефон: {userState.PhoneNumber}\n" +
                          $"👤 Имя: {userState.FirstName} {userState.LastName}\n";
            
            if (userState.SelectedGroup != null)
            {
                message += $"🎯 Группа: {userState.SelectedGroup.Name}\n\n";
            }
            else
            {
                message += "\n";
            }
        
            message += $"📱 Вы можете войти в мобильное приложение СКФЭТ с вашими учетными данными после проверки администрации.\n\n" +
                       $"📲 Скачать приложение: {_appUrl}\n\n" +
                       $"🔐 Логин: {userState.Email}\n" +
                       $"🔑 Пароль: {userState.Password}\n\n" +
                       $"⚠️ Сохраните эти данные в надежном месте!";
        
            await SendMessage(chatId, message);
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
        }
        // Очищаем состояние
        _userStates.Remove(chatId);
    }
    
    public async Task OnUserIsActiveEvent(UserIsActiveMessage message)
    {
        try
        {
            var chatId = long.Parse(message.ChatId);
            
            if (message.IsActive)
            {
                await SendAccountApprovedMessage(chatId, message.FirstName, message.LastName, message.Email, message.Role, message.PhoneNumber);
            }
            else
            {
                await SendAccountRejectedMessage(chatId, message.FirstName, message.LastName, message.Email, message.PhoneNumber);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Ошибка при отправке уведомления для ChatId: {message.ChatId}");
        }
    }
    
    public async Task SendMaintenanceNotificationAsync(MaintenanceNotificationMessage notification)
    {
        try
        {
            var message = $"⚠️ ТЕХНИЧЕСКИЕ РАБОТЫ\n\n" +
                          $"{notification.Title}\n" +
                          $"{notification.Message}\n\n" +
                          $"🕐 Начало: {notification.StartTime:dd.MM.yyyy HH:mm}\n" +
                          (notification.EndTime.HasValue
                              ? $"🕐 Окончание: {notification.EndTime:dd.MM.yyyy HH:mm}\n"
                              : "");

            List<User> students = await _apiService.GetStudents();
            
            List<User> uniqueStudents = students
                .Where(s => !string.IsNullOrEmpty(s.ChatId) && long.TryParse(s.ChatId, out var _))
                .DistinctBy(s => s.ChatId)
                .ToList();

            foreach (User student in uniqueStudents)
            {
                if (long.TryParse(student.ChatId, out long chatId))
                {
                    await SendMessage(chatId, message);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending maintenance notification to users");
            throw;
        }
    }
    
    private async Task SendAccountApprovedMessage(long chatId, string firstName, string lastName, string email, string role, string phoneNumber)
    {
        var messageText = $"🎉 Ваш аккаунт активирован!\n\n" +
                          $"👤 Пользователь: {firstName} {lastName}\n" +
                          $"📧 Email: {email}\n" +
                          $"📞 Телефон: {phoneNumber}\n" +
                          $"🎯 Роль: {role}\n\n" +
                          $"✅ Теперь вы можете полноценно использовать все возможности системы.\n\n" +
                          $"Спасибо за регистрацию!";

        await SendMessage(chatId, messageText);
    }

    private async Task SendAccountRejectedMessage(long chatId, string firstName, string lastName, string email, string phoneNumber)
    {
        var messageText = $"❌ Ваш аккаунт отклонен\n\n" +
                          $"👤 Пользователь: {firstName} {lastName}\n" +
                          $"📧 Email: {email}\n" +
                          $"📞 Телефон: {phoneNumber}\n\n" +
                          $"⚠️ К сожалению, ваша регистрация не была одобрена администратором.\n\n" +
                          $"Если вы считаете, что это ошибка, пожалуйста, свяжитесь с поддержкой.";

        await SendMessage(chatId, messageText);
    }

    private async Task SendInfoMessage(Message message)
    {
        var from = message.From;
        await SendMessage(message.Chat.Id, 
            $"👤 Ваш ID: {from.Id}\n" +
            $"📝 Username: @{from.Username}\n" +
            $"👤 Имя: {from.FirstName}\n" +
            $"💬 Chat ID: {message.Chat.Id}");
        return;
    }
    
    private async Task SendStartMessage(long chatId)
    {
        var message = "👋 Добро пожаловать в систему уведомлений СКФЭТ!\n\n" +
                      $"📊 Давайте зарегистрируем вас в системе.\n\n" +
                      "Выберите способ регистрации:\n" +
                      "/registerAsStudent - зарегистрироваться как студент\n" +
                      "/registerAsParent - зарегистрироваться как родитель";
        
        await SendMessage(chatId, message);
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
                        "Для начала новой регистрации используйте команду /start");
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
    
    private bool IsValidPhoneNumber(string phoneNumber)
    {
        try
        {
            var phoneUtil = PhoneNumberUtil.GetInstance();

            var parsedNumber = phoneUtil.Parse(phoneNumber, null);
        
            return phoneUtil.IsValidNumber(parsedNumber);
        }
        catch (NumberParseException ex)
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
using Microsoft.Extensions.Logging;
using StackExchange.Redis;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;
using TelegramBotEmployee.Models;
using BotUserState = TelegramBotEmployee.Models.BotUserState;
using RegistrationState = TelegramBotEmployee.Models.RegistrationState;
using UserRole = TelegramBotEmployee.Models.UserRole;

namespace TelegramBotEmployee.Services;

public class BotService
{
    private readonly TelegramBotClient  _botClient;
    private readonly ApiService _apiService;
    private readonly ILogger<BotService> _logger;
    private readonly RedisCache _redis;
    private long _botId;

    public BotService(string botToken, ILogger<BotService> logger, ApiService apiService, RedisCache redis)
    {
        _botClient = new TelegramBotClient(botToken);
        _logger = logger;
        _apiService = apiService;
        _redis = redis;
    }
    
    public async Task StartAsync()
    {
        _botClient.StartReceiving(HandleUpdateAsync, HandleErrorAsync);
            
        var me = await _botClient.GetMe();
        _botId = me.Id;
        _logger.LogInformation($"Bot @{me.Username} started successfully!");
    }

    private async Task HandleUpdateAsync(ITelegramBotClient botClient, Update update, CancellationToken cancellationToken)
    {
        try
        {
            switch (update.Type)
            {
                case UpdateType.Message:
                    await HandleMessageAsync(update.Message);
                    break;
                case UpdateType.CallbackQuery:
                    await HandleCallbackQueryAsync(update.CallbackQuery);
                    break;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling update");
        }
    }

    private async Task HandleMessageAsync(Message message)
    {
        if (message.Text != null)
        {
            var chatId = message.Chat.Id;
            var userState = await _redis.GetAsync<BotUserState>($"{chatId.ToString()}-employee");
            
            if (message.Text.StartsWith("/"))
            {
                await HandleCommandAsync(message);
            }
            else
            {
                await HandleTextMessageAsync(message);
            }
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
            case "/registerAsTeacher":
                await StartRegistrationAsync(UserRole.Teacher, chatId);
                break;
            case "/registerAsAdministrator":
                await StartRegistrationAsync(UserRole.Administrator, chatId);
                break;
            default:
                await SendMessage(chatId, "Неизвестная команда");
                break;
        }
    }

    private async Task HandleTextMessageAsync(Message message)
    {
        var chatId = message.Chat.Id;

        if (!await _redis.ExistsAsync($"{chatId.ToString()}-employee"))
        {
            await SendMessage(chatId, "Пожалуйста, начните регистрацию с помощью команды /start");
            return;
        }

        var userState = await _redis.GetAsync<BotUserState>($"{chatId.ToString()}-employee");
        userState.LastActivity = DateTime.UtcNow;

        //Обрабатываем текущее состояния пользователя в регистрации
        switch (userState.State)
        {
            case RegistrationState.Start:
                await SendStartMessage(chatId); 
                break;
            case RegistrationState.WaitingForEmail:
                await ProcessEmail(chatId, message.Text);
                break;
                
            case RegistrationState.WaitingForFirstName:
                await ProcessFirstName(chatId, message.Text);
                break;
                
            case RegistrationState.WaitingForLastName:
                await ProcessLastName(chatId, message.Text);
                break;
                
            case RegistrationState.WaitingForPassword:
                await ProcessPassword(chatId, message.Text, message.From.Id.ToString());
                break;
            case RegistrationState.Completed:
                await SendStartMessage(chatId);
                break;
        }
    }

    private async Task HandleCallbackQueryAsync(CallbackQuery callbackQuery)
    {
        
    }

    private async Task StartRegistrationAsync(UserRole role, long chatId)
    {
        // Проверяем лимит перед началом регистрации
        if (!await CheckDailyLimitAsync(chatId))
        {
            await SendDailyLimitExceededMessage(chatId);
            return;
        }
        
        var botUserState = new BotUserState()
        {
            State = RegistrationState.WaitingForEmail,
            Role = role,
            LastActivity = DateTime.UtcNow
        };

        if (await _redis.ExistsAsync($"{chatId.ToString()}-employee"))
        {
            botUserState = await _redis.GetAsync<BotUserState>($"{chatId.ToString()}-employee");
            
            // Проверяем лимит для существующего пользователя
            if (!await CheckDailyLimitAsync(chatId))
            {
                await SendDailyLimitExceededMessage(chatId);
                return;
            }
            
            botUserState.State = RegistrationState.WaitingForEmail;
            botUserState.Role = role;
            botUserState.LastActivity = DateTime.UtcNow;
        }
        
        await _redis.SetAsync($"{chatId.ToString()}-employee", botUserState);
        
        await SendMessage(chatId, "📧 Введите ваш email:");
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

        var userState = await _redis.GetAsync<BotUserState>($"{chatId.ToString()}-employee");
        if (!await IsUserEnableAsync(userState, chatId)) return;
        
        userState.State = RegistrationState.WaitingForFirstName;
        userState.Email = email;

        await _redis.SetAsync($"{chatId.ToString()}-employee", userState);
        
        await SendMessage(chatId, "✅ Email принят!\n\n👤 Теперь введите ваше имя:");
    }

    private async Task ProcessFirstName(long chatId, string firstName)
    {
        if (string.IsNullOrWhiteSpace(firstName) || firstName.Length < 2)
        {
            await SendMessage(chatId, "❌ Имя должно содержать хотя бы 2 символа. Пожалуйста, введите ваше имя:");
            return;
        }

        var userState = await _redis.GetAsync<BotUserState>($"{chatId.ToString()}-employee");
        if (!await IsUserEnableAsync(userState, chatId)) return;
        
        userState.State = RegistrationState.WaitingForLastName;
        userState.FirstName = firstName;
        
        await _redis.SetAsync($"{chatId.ToString()}-employee", userState);
        
        await SendMessage(chatId, "✅ Имя принято!\n\n👤 Теперь введите вашу фамилию:");
    }

    private async Task ProcessLastName(long chatId, string lastName)
    {
        if (string.IsNullOrWhiteSpace(lastName) || lastName.Length < 2)
        {
            await SendMessage(chatId, "❌ Фамилия должна содержать хотя бы 2 символа. Пожалуйста, введите вашу фамилию:");
            return;
        }
        
        var userState = await _redis.GetAsync<BotUserState>($"{chatId.ToString()}-employee");
        if (!await IsUserEnableAsync(userState, chatId)) return;
        
        userState.State = RegistrationState.WaitingForPassword;
        userState.LastName = lastName;

        await _redis.SetAsync($"{chatId.ToString()}-employee", userState);
        
        await SendMessage(chatId, 
            "✅ Фамилия принята!\n\n" +
            "🔐 Теперь придумайте пароль (минимум 6 символов):");
    }

    private async Task ProcessPassword(long chatId, string password, string userId)
    {
        if (string.IsNullOrWhiteSpace(password) || password.Length < 6)
        {
            await SendMessage(chatId, "❌ Пароль должен содержать минимум 6 символов. Пожалуйста, придумайте пароль:");
            return;
        }

        var userState = await _redis.GetAsync<BotUserState>($"{chatId.ToString()}-employee");
        if (!await IsUserEnableAsync(userState, chatId)) return;
        
        userState.State = RegistrationState.Completed;
        userState.Password = password;
        
        await _redis.SetAsync($"{chatId.ToString()}-employee", userState);
        
        await SendMessage(chatId, "✅ Пароль принят!");
        await ProcessRegistration(chatId, userId);
    }

    private async Task ProcessRegistration(long chatId, string userId)
    {
        var userState = await _redis.GetAsync<BotUserState>($"{chatId.ToString()}-employee");
        if (!await IsUserEnableAsync(userState, chatId)) return;
        
        // Финальная проверка лимита перед регистрацией
        if (!await CheckDailyLimitAsync(chatId))
        {
            await SendDailyLimitExceededMessage(chatId);
            userState.State = RegistrationState.Start;
            await _redis.SetAsync($"{chatId.ToString()}-employee", userState);
            return;
        }
        
        // Регистрируем пользователя
        var registerRequest = new RegisterRequest()
        {
            Email = userState.Email!,
            Password = userState.Password!,
            ConfirmPassword = userState.Password!,
            FirstName = userState.FirstName!,
            LastName = userState.LastName!,
            Role = userState.Role,
            TelegramId = userId.ToLower(),
            ChatId = chatId.ToString()
        };
        
        var result = await _apiService.RegisterAsync(registerRequest);

        if (result.Success)
        {
            await SendMessage(chatId,
                $"🎉 Регистрация успешно завершена!\n\n" +
                $"📧 Email: {userState.Email}\n" +
                $"👤 Имя: {userState.FirstName} {userState.LastName}\n" +
                $"🎯 Роль: {userState.Role}\n\n" +
                $"📱 Вы можете войти в мобильное приложение СКФЭТ с вашими учетными данными после проверки администрации.\n\n" +
                $"🔐 Логин: {userState.Email}\n" +
                $"🔑 Пароль: {userState.Password}\n\n" +
                $"⚠️ Сохраните эти данные в надежном месте!");

            // Увеличиваем счетчик и обновляем дату
            userState.AccountsCount++;
            userState.LastAccountCreationDate = DateTime.UtcNow;
        }
        else
        {
            var message = $"❌ {result.Message}";
            await SendMessage(chatId, message);
            // Сбрасываем состояние
            userState.State = RegistrationState.Start;
        }
        // Сохраняем состояние
        await _redis.SetAsync($"{chatId.ToString()}-employee", userState);
    }

    public async Task OnUserIsActiveEvent(UserIsActiveMessage message)
    {
        try
        {
            var chatId = long.Parse(message.ChatId);
            
            // Проверяем, существует ли пользователь в Redis
            var userState = await _redis.GetAsync<BotUserState>($"{message.ChatId}-employee");
            if (userState == null)
            {
                _logger.LogWarning($"Пользователь с ChatId: {message.ChatId} не найден в Redis");
                return;
            }
            
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

    private async Task<bool> CheckDailyLimitAsync(long chatId)
    {
        var userState = await _redis.GetAsync<BotUserState>($"{chatId.ToString()}-employee");
    
        if (userState == null)
            return true; // Если состояния нет, значит аккаунты еще не создавались
    
        var today = DateTime.UtcNow.Date;
        var lastCreationDate = userState.LastAccountCreationDate.Date;
    
        // Если последнее создание было не сегодня, сбрасываем счетчик
        if (lastCreationDate < today)
        {
            userState.AccountsCount = 0;
            userState.LastAccountCreationDate = DateTime.UtcNow;
            await _redis.SetAsync($"{chatId.ToString()}-employee", userState);
            return true;
        }
    
        // Проверяем лимит
        return userState.AccountsCount < 3;
    }
    
    private async Task<bool> IsUserEnableAsync(BotUserState? userState, long chatId)
    {
        if (userState == null || userState?.State == RegistrationState.Expired)
        {
            await SendUserRegistrationExpireMessage(chatId);
            return false;
        }
        return true;
    }
    
    private async Task SendStartMessage(long chatId)
    {
        //await _redis.RemoveAsync($"{chatId.ToString()}-employee");
        var userState = await _redis.GetAsync<BotUserState>($"{chatId.ToString()}-employee");
        var accountsCreated = userState?.AccountsCount ?? 0;
        var today = DateTime.UtcNow.Date;
        var lastCreationDate = userState?.LastAccountCreationDate.Date ?? DateTime.MinValue;
    
        // Сбрасываем счетчик если это новый день
        if (userState != null && lastCreationDate < today)
        {
            userState.AccountsCount = 0;
            userState.LastAccountCreationDate = DateTime.UtcNow;
            await _redis.SetAsync($"{chatId.ToString()}-employee", userState);
            accountsCreated = 0;
        }
    
        var message = "👋 Добро пожаловать в telegram-бот для сотрудников!\n\n" +
                      $"📊 Аккаунтов создано сегодня: {accountsCreated}/3\n\n" +
                      "Выберите способ регистрации:\n" +
                      "/registerAsTeacher - зарегистрироваться как преподаватель\n" +
                      "/registerAsAdministrator - зарегистрироваться как администратор\n";

        await SendMessage(chatId, message);
    }

    private async Task SendUserRegistrationExpireMessage(long chatId)
    {
        var message = "⏰ Ваша сессия регистрации истекла из-за неактивности.\n" +
                      "Для начала новой регистрации используйте команду /start";

        await SendMessage(chatId, message);
    }
    
    private async Task SendDailyLimitExceededMessage(long chatId)
    {
        var userState = await _redis.GetAsync<BotUserState>($"{chatId.ToString()}-employee");
        var accountsCreated = userState?.AccountsCount ?? 0;
    
        var message = $"🚫 Превышен дневной лимит!\n\n" +
                      $"Вы создали {accountsCreated}/3 аккаунтов сегодня.\n" +
                      $"Новые регистрации будут доступны после 03:00.\n\n" +
                      $"⌛ Текущее время: {DateTime.UtcNow.AddHours(3):HH:mm}";
    
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
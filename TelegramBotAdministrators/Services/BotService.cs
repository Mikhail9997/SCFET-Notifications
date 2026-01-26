using System.Reflection.Metadata.Ecma335;
using Application.Services;
using Microsoft.Extensions.Logging;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;
using TelegramBotAdministrators.Handlers;
using TelegramBotAdministrators.Models;

namespace TelegramBotAdministrators.Services;

public class BotService:IBotMessageSender
{
    private readonly TelegramBotClient  _botClient;
    private readonly ILogger<BotService> _logger;
    private readonly IApiService _apiService;
    private readonly RedisService _redis;
    private readonly LoginHandler _loginHandler;
    private readonly GroupCreationHandler _groupCreationHandler;
    private readonly ITokenService _tokenService;
    
    public BotService(string botToken, ILogger<BotService> logger, IApiService apiService, RedisService redis, LoginHandler loginHandler, GroupCreationHandler groupCreationHandler, ITokenService tokenService)
    {
        _botClient = new TelegramBotClient(botToken);
        _logger = logger;
        _apiService = apiService;
        _redis = redis;
        _loginHandler = loginHandler;
        _groupCreationHandler = groupCreationHandler;
        _tokenService = tokenService;

        _loginHandler.SendMessage += SendMessage;
        _groupCreationHandler.SendMessage += SendMessage;
    }
    
    public async Task StartAsync()
    {
        _botClient.StartReceiving(HandleUpdateAsync, HandleErrorAsync);
            
        var me = await _botClient.GetMe();
        _logger.LogInformation($"Bot @{me.Username} started successfully!");
    }

    private async Task HandleUpdateAsync(ITelegramBotClient botClient, Update update,
        CancellationToken cancellationToken)
    {
        try
        {
            long? chatId = GetChatId(update);

            // Проверяем авторизацию
            var result = await TestValidAccessTokenAsync(chatId.Value);
            // Игнорируем запрос, если что-то не так
            if (!result) return;
            
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
            
            // Проверяем авторизован ли пользователь
            bool isAuthenticated = false;
            if (await _redis.ExistsAsync(chatId.ToString()))
            {
                var userState = await _redis.GetAsync<BotUserState>(chatId.ToString());
                
                isAuthenticated = userState != null && userState.IsAuthenticated;
            }
            
            if (message.Text.StartsWith("/"))
            {
                await HandleCommandAsync(message, isAuthenticated);
            }
            else
            {
                await HandleTextMessageAsync(message, isAuthenticated);
            }
        }
    }
    
    private async Task HandleCommandAsync(Message message, bool isAuthenticated)
    {
        var chatId = message.Chat.Id;
        var command = message.Text;

        switch (command)
        {
            case "/start":
                await SendStartMessage(chatId, isAuthenticated);
                break;
                
            case "/login":
                if (isAuthenticated) 
                    await SendAlreadyAuthenticatedMessage(chatId);
                else 
                    await _loginHandler.StartLoginProcess(chatId);
                break;
                
            case "/logout":
                if (isAuthenticated)
                    await LogoutUser(chatId);
                else
                    await SendAlreadyLogoutMessage(chatId);
                break;
                
            case "/profile":
                if (isAuthenticated)
                    await ShowProfile(chatId);
                else
                    await SendNotAuthenticatedMessage(chatId);
                break;
            
            case "/createGroup":
                if (isAuthenticated)
                    await _groupCreationHandler.StartGroupCreation(chatId);
                else
                    await SendNotAuthenticatedMessage(chatId);
                break;
            
            default:
                await SendMessage(chatId, "Неизвестная команда");
                break;
        }
    }
    
    private async Task HandleTextMessageAsync(Message message, bool isAuthenticated)
    {
        var chatId = message.Chat.Id;
        var text = message.Text;

        // Если пользователь не в процессе логина или создания группы
        if (!await _redis.ExistsAsync(chatId.ToString()))
        {
            await SendMessage(chatId, "Для использования бота необходимо авторизоваться. Используйте команду /login");
            return;
        }
        var userState = await _redis.GetAsync<BotUserState>(chatId.ToString());
        var state = userState?.State ?? null;
        
        // Если пользователь в процессе логина
        if (state != null && state != LoginState.Completed)
        {
            await _loginHandler.HandleTextMessageAsync(chatId, text, userState.State);
        }
        //Если пользователь в процессе создания группы
        else if(userState?.GroupState != null)
        {
            await _groupCreationHandler.HandleTextMessageAsync(chatId, text, userState);
        }
    }
    
    private async Task LogoutUser(long chatId)
    {
        try
        {
            if (await _redis.ExistsAsync(chatId.ToString()))
            {
                var currentUser = await _redis.GetAsync<BotUserState>(chatId.ToString());
                var requestResult = await _apiService.LogoutAsync(currentUser.UserId.Value, currentUser?.AccessToken ?? "");

                if (requestResult.Data)
                {
                    // Очищаем сессию
                    await _redis.RemoveAsync(chatId.ToString());
                    await SendMessage(chatId, "✅ Вы успешно вышли из системы.");
                }
                else
                {
                    switch (requestResult.Code)
                    {
                        case 400:
                            // очищаем кеш если пользователь не найден
                            await _redis.RemoveAsync(chatId.ToString());
                            await SendMessage(chatId, "✅ Вы успешно вышли из системы.");
                            break;
                        default:
                            await SendMessage(chatId, "❌ Произошла неизвестная ошибка");
                            break;
                    }
                }
            }
            else
            {
                await SendMessage(chatId, "❌ Вы не авторизованы.");
            }
        }
        catch(Exception ex)
        {
            await SendMessage(chatId, "❌ Произошла неизвестная ошибка, снова войдите в аккаунт");
            Console.WriteLine($"Error during logout {ex.Message}");
            // Очищаем сессию
            await _redis.RemoveAsync(chatId.ToString());
            await SendMessage(chatId, "✅ Вы успешно вышли из системы.");
        }
    }   
        
    private async Task ShowProfile(long chatId)
    {
        var token = (await _redis.GetAsync<BotUserState>(chatId.ToString()))?.AccessToken;
        var requestResult = await _apiService.GetProfileAsync(token ?? "");
        var userData = requestResult.Data;
        // если сервер вернул данные профиля
        if (userData != null)
        {
            var profileMessage = $"👤 Ваш профиль:\n\n" +
                                 $"📧 Email: {userData.Email}\n" +
                                 $"👤 Имя: {userData.FirstName}\n" +
                                 $"👤 Фамилия: {userData.LastName}\n" +
                                 $"🎯 Роль: {userData.Role}\n" +
                                 $"🆔 Chat ID: {chatId}";
            
            await SendMessage(chatId, profileMessage);
            return;
        }
        
        switch (requestResult.Code)
        {
            case 404:
                // если не удалось найти профиль в базе данных
                // очищаем кеш
                await _redis.RemoveAsync(chatId.ToString());
                await SendMessage(chatId, "❌ Текущей учетной записи не существует.");
                break;
            default:
                await SendMessage(chatId, "❌ Не удалось загрузить данные профиля.");
                break;
        }
    }
    
    private async Task SendNotAuthenticatedMessage(long chatId)
    {
        await SendMessage(chatId, "❌ Вы не авторизованы. Используйте команду /login для входа в систему.");
    }
    
    private async Task SendAlreadyAuthenticatedMessage(long chatId)
    {
        await SendMessage(chatId, "❌ Вы уже авторизованы. Используйте команду /logout для выхода из системы.");
    }
    
    private async Task SendAlreadyLogoutMessage(long chatId)
    {
        await SendMessage(chatId, "❌ Вы еще не вошли в систему. Используйте команду /login для входа в систему.");
    }
    
    public async Task OnUserRegisterEvent(UserRegisterMessage message)
    {
        try
        {
            // Получаем всех авторизованных администраторов
            var admins = await _apiService.GetAdministratorsAsync();
        
            if (!admins.Any())
            {
                _logger.LogWarning("No admins registered to receive notifications");
                return;
            }

            var userInfo = $"🆕 Новый пользователь зарегистрирован:\n\n" +
                           $"👤 Имя: {message.FirstName}\n" +
                           $"👤 Фамилия: {message.LastName}\n" +
                           $"\ud83c\udfaf Роль: {message.Role}\n" +
                           $"📧 Email: {message.Email}\n" +
                           $"🆔 User ID: {message.UserId}";

            // Создаем inline клавиатуру с кнопками
            var inlineKeyboard = new InlineKeyboardMarkup(new[]
            {
                new[]
                {
                    InlineKeyboardButton.WithCallbackData(
                        "✅ Активировать", 
                        $"activate_{message.UserId}")
                },
                new[]
                {
                    InlineKeyboardButton.WithCallbackData(
                        "❌ Отклонить", 
                        $"reject_{message.UserId}")
                }
            });

            // Отправляем сообщение всем администраторам
            foreach (var admin in admins)
            {
                if (long.TryParse(admin.ChatId, out var chatId) && admin.IsActive)
                {
                    await SendMessage(chatId, userInfo, inlineKeyboard);
                }
            }

            _logger.LogInformation("User register notification sent to {AdminCount} admins for user {UserId}", 
                admins.Count, message.UserId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending user register notification for user {UserId}", message.UserId);
        }
    }
    
    private async Task HandleCallbackQueryAsync(CallbackQuery callbackQuery)
    {
        var callbackData = callbackQuery.Data;
        var chatId = callbackQuery.Message.Chat.Id;
        var messageId = callbackQuery.Message.MessageId;
        string? token = string.Empty;

        // Проверяем авторизацию администратора
        if (!await _redis.ExistsAsync(chatId.ToString()) || !(await _redis.GetAsync<BotUserState>(chatId.ToString())).IsAuthenticated)
        {
            await _botClient.AnswerCallbackQuery(
                callbackQuery.Id,
                "❌ Для выполнения действий необходимо авторизоваться");
            return;
        }

        token = (await _redis.GetAsync<BotUserState>(chatId.ToString()))?.AccessToken;
        
        if (callbackData.StartsWith("activate_"))
        {
            await HandleActivateUser(callbackQuery, callbackData, chatId, messageId, token ?? "");
        }
        else if (callbackData.StartsWith("reject_"))
        {
            await HandleRejectUser(callbackQuery, callbackData, chatId, messageId, token ?? "");
        }
    }
    
    private async Task HandleActivateUser(CallbackQuery callbackQuery, string callbackData, long chatId, int messageId, string token)
    {
        var userIdString = callbackData.Substring("activate_".Length);
        
        if (Guid.TryParse(userIdString, out var userId))
        {
            var result = await _apiService.ActivateUser(userId, token);
        
            if (result.Success)
            {
                var currentUser = await _redis.GetAsync<BotUserState>(chatId.ToString());
                var originalText = callbackQuery.Message.Text;
                var updatedText = $"{originalText}\n\n✅ ПОЛЬЗОВАТЕЛЬ АКТИВИРОВАН\n👤 Модератор: Email {currentUser?.Email ?? ""}";
            
                await _botClient.EditMessageText(
                    chatId: chatId,
                    messageId: messageId,
                    text: updatedText,
                    replyMarkup: null);

                await _botClient.AnswerCallbackQuery(
                    callbackQuery.Id,
                    "Пользователь успешно активирован");
            
                _logger.LogInformation("User {UserId} activated by admin {AdminId}", userId, chatId);
            }
            else
            {
                await _botClient.AnswerCallbackQuery(
                    callbackQuery.Id,
                    $"Ошибка при активации пользователя. {result.Message}");
            }
        }
    }
    
    private async Task HandleRejectUser(CallbackQuery callbackQuery, string callbackData, long chatId, int messageId, string token)
    {
        var userIdString = callbackData.Substring("reject_".Length);
        if (Guid.TryParse(userIdString, out var userId))
        {
            var result = await _apiService.RemoveUser(userId, token);
        
            if (result.Success)
            {
                var currentUser = await _redis.GetAsync<BotUserState>(chatId.ToString());
                var originalText = callbackQuery.Message.Text;
                var updatedText = $"{originalText}\n\n❌ ПОЛЬЗОВАТЕЛЬ ОТКЛОНЕН" + $"\n📧 Модератор" + $": Email {currentUser?.Email ?? ""}";
            
                await _botClient.EditMessageText(
                    chatId: chatId,
                    messageId: messageId,
                    text: updatedText,
                    replyMarkup: null);

                await _botClient.AnswerCallbackQuery(
                    callbackQuery.Id,
                    "Пользователь отклонен");
            
                _logger.LogInformation("User {UserId} rejected by admin {AdminId}", userId, chatId);
            }
            else
            {
                await _botClient.AnswerCallbackQuery(
                    callbackQuery.Id,
                    $"Ошибка при отклонении пользователя. {result.Message}");
            }
        }
    }

    private async Task<bool> TestValidAccessTokenAsync(long chatIdLong)
    {
        string chatId = chatIdLong.ToString();
        var botUserState = await _redis.GetAsync<BotUserState>(chatId);
        
        // Если пользователь еще не авторизован
        if (botUserState == null || botUserState?.State == null ||botUserState?.State != LoginState.Completed)
        {
            return true;
        }
        if (botUserState.IsRefreshing)
        {
            string text = "❌ Вы делали слишком много запросов. Пожалуйста, подождите и повторите запрос позже";
            await SendMessage(chatIdLong, text);
            return false;
        }

        string? accessToken = botUserState?.AccessToken;
        string? refreshToken = botUserState?.RefreshToken;
        string? newAccessToken = await _tokenService.GetValidAccessTokenAsync(accessToken, refreshToken, chatId);
        
        // Если токен получен
        if (newAccessToken != null)
        {
            return true;
        }
        
        var botUserStateFinish = await _redis.GetAsync<BotUserState>(chatId);
        
        // Если не удалось обновить токены и это не связано с отсутствием интернет соединения
        if (botUserStateFinish != null && 
            (string.IsNullOrEmpty(botUserStateFinish.AccessToken) ||
            string.IsNullOrEmpty(botUserStateFinish.RefreshToken)))
        {
            // Очищаем кеш
            await _redis.RemoveAsync(chatId);
            await SendMessage(chatIdLong, $"❌ Ошибка авторизации. Сессия истекла, войдите снова");
        }
        // Если не удалось обновить токены по причине отсутствия интернет соединения
        else
        {
            await SendMessage(chatIdLong, $"❌ Отсутствует соединение с сервером");
        }
        return false;
    }
    
    private async Task SendStartMessage(long chatId, bool isAuthenticated)
    {
        var message = "👋 Добро пожаловать в бот модерации!\n\n";
        
        if (isAuthenticated)
        {
            message += "✅ Вы авторизованы как администратор\n" +
                       "Теперь вы будете получать уведомления о новых пользователях для модерации.\n\n" +
                       "Доступные команды:\n" +
                       "/profile - Показать профиль\n" +
                       "/createGroup - Создать группу\n" +
                       "/logout - Выйти";
        }
        else
        {
            message += "Для работы с ботом необходимо авторизоваться.\n\n" +
                       "Доступные команды:\n" +
                       "/login - Авторизация\n";
        }
        
        await SendMessage(chatId, message);
    }
    
    public async Task SendMessage(long chatId, string message, ReplyMarkup? replyMarkup = null)
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

    private long? GetChatId(Update update)
    {
        long? chatId = null;
        switch (update.Type)
        {
            case UpdateType.Message:
                chatId = update?.Message?.Chat.Id;
                break;
            case UpdateType.CallbackQuery:
                chatId = update?.CallbackQuery?.Message?.Chat.Id;
                break;
        }

        return chatId;
    }
    
    private Task HandleErrorAsync(ITelegramBotClient arg1, Exception exception, CancellationToken arg3)
    {
        _logger.LogError(exception, "Telegram Bot error");
        return Task.CompletedTask;
    }
}
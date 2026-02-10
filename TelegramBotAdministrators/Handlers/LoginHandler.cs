using Application.Services;
using Microsoft.Extensions.Logging;
using Telegram.Bot;
using Telegram.Bot.Types.ReplyMarkups;
using TelegramBotAdministrators.Models;
using TelegramBotAdministrators.Services;

namespace TelegramBotAdministrators.Handlers;

public class LoginHandler
{
    private readonly ILogger<LoginHandler> _logger;
    private readonly IApiService _apiService;
    private readonly RedisService _redis;
    public Func<long,string,ReplyMarkup?,Task> SendMessage;

    public LoginHandler(ILogger<LoginHandler> logger, IApiService apiService, RedisService redis)
    {
        _logger = logger;
        _apiService = apiService;
        _redis = redis;
    }
    
    public async Task StartLoginProcess(long chatId)
    {
        var botUserState = new BotUserState()
        {
            State = LoginState.WaitingForEmail 
        };
        await _redis.SetAsync(chatId.ToString(), botUserState);
        
        await SendMessage.Invoke(chatId, "📧 Введите ваш email:", null);
    }

    public async Task HandleTextMessageAsync(long chatId, string text, LoginState state)
    {
        switch (state)
        {
            case LoginState.WaitingForEmail:
                await ProcessLoginEmail(chatId, text);
                break;
            case LoginState.WaitingForPassword:
                await ProcessLoginPassword(chatId, text);
                break;
            case LoginState.Completed:
                await SendMessage.Invoke(chatId, "я тебя не понимаю", null);
                break;
            default:
                await SendMessage.Invoke(chatId, "Для использования бота необходимо авторизоваться. Используйте команду /login", null);
                break;
        }
    }
    
    private async Task ProcessLoginEmail(long chatId, string email)
    {
        var userState = await _redis.GetAsync<BotUserState>(chatId.ToString());
        userState.State = LoginState.WaitingForPassword;
        userState.Email = email;
        
        await _redis.SetAsync(chatId.ToString(), userState);
        
        await SendMessage.Invoke(chatId, "🔐 Введите ваш пароль:", null);
    }
    
    private async Task ProcessLoginPassword(long chatId, string password)
    {
        try
        {
            var email = (await _redis.GetAsync<BotUserState>(chatId.ToString()))?.Email;
            var userState = await _redis.GetAsync<BotUserState>(chatId.ToString());
            userState.Password = password;
            
            // Выполняем логин
            var loginDto = new LoginDto
            {
                Email = email,
                Password = password,
                ChatId = chatId.ToString()
            };
            
            var authResult = await _apiService.LoginAsync(loginDto);
            // если успешно
            if (authResult != null && authResult.Success && authResult.Data?.Role == "Administrator")
            {
                var data = authResult.Data;
                userState.IsAuthenticated = true;
                userState.AccessToken = data.AccessToken;
                userState.RefreshToken = data.RefreshToken;
                userState.UserId = data.UserId;
                
                var welcomeMessage = $"✅ Авторизация успешна!\n\n" +
                                   $"👤 Добро пожаловать, {data.FirstName} {data.LastName}!\n" +
                                   $"📧 Email: {data.Email}\n" +
                                   $"🎯 Роль: {data.Role}\n\n" +
                                   $"Теперь вы будете получать уведомления о новых пользователях для модерации.\n\n" +
                                   $"Доступные команды:\n" +
                                   "/profile - Показать профиль\n" +
                                   "/createGroup - Создать группу\n" + 
                                   "/removeGroup - Удалить группу\n" + 
                                   "/logout - Выйти";
                
                await SendMessage.Invoke(chatId, welcomeMessage, null);
                
                _logger.LogInformation("Admin logged in: {Email} (ChatId: {ChatId})", data.Email, chatId);
                userState.State = LoginState.Completed;
                // сохраняем в redis до истечения срока refresh токена валидации
                await _redis.SetAsync(chatId.ToString(), userState, TimeSpan.FromDays(data.AuthPeriod));
            }
            else
            {
                //удаляем из redis
                await _redis.RemoveAsync(chatId.ToString());
                await SendMessage.Invoke(chatId, $"❌ Ошибка авторизации. {authResult?.Message ?? "Неизвестная ошибка"}", null);
            }
            
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during login process for chat {ChatId}", chatId);
            //удаляем из redis
            await _redis.RemoveAsync(chatId.ToString());
            await SendMessage.Invoke(chatId, "❌ Произошла ошибка при авторизации. Попробуйте снова.", null);
        }
    }
}
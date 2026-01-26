using System.IdentityModel.Tokens.Jwt;
using System.Text;
using System.Text.Json;
using Application.Services;
using Microsoft.Extensions.Configuration;
using TelegramBotAdministrators.Models;

namespace TelegramBotAdministrators.Services;

public interface ITokenService
{
    Task<string?> GetValidAccessTokenAsync(string accessToken, string refreshToken, string chatId);
    Task<TokenData?> RefreshTokenAsync(string accessToken, string refreshToken, string chatId);
    Task SaveAsync(BotUserState userState, TokenData? tokenData, string chatId);
    Task ClearTokensAsync(string chatId);
    event Action OnTokensRefreshed;
    event Action OnTokensInvalid;
}

public class TokenService : ITokenService
{
    private readonly RedisService _redis;
    private readonly HttpClient _httpClient;
    
    private readonly string _baseUrl;

    public TokenService(RedisService redis, IConfiguration configuration)
    {
        _redis = redis;
        _httpClient = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(10)
        };
        _baseUrl = configuration["Api:BaseUrl"] ?? "";
    }
    
    public async Task<string?> GetValidAccessTokenAsync(string accessToken, string refreshToken, string chatId)
    {
        if (string.IsNullOrEmpty(accessToken))
            return null;

        // Проверяем, не истек ли токен
        try
        {
            var botUserState = await _redis.GetAsync<BotUserState>(chatId);
            if (botUserState == null) return null;
            
            var handler = new JwtSecurityTokenHandler();
            if (handler.CanReadToken(accessToken))
            {
                var jwtToken = handler.ReadJwtToken(accessToken);
                var expires = jwtToken.ValidTo;

                // Если токен еще валиден более 5 минут, возвращаем его
                if (expires > DateTime.UtcNow.AddMinutes(5))
                    return accessToken;

                // Если токен скоро истечет, пробуем обновить
                if (expires > DateTime.UtcNow)
                {
                    var tokenData = await RefreshTokenAsync(accessToken,refreshToken, chatId);
                    await SaveAsync(botUserState, tokenData, chatId);
                    if (tokenData != null)
                    {
                        return tokenData.AccessToken;
                    }
                }
                else
                {
                    // Токен уже истек
                    var tokenData = await RefreshTokenAsync(accessToken, refreshToken, chatId);
                    await SaveAsync(botUserState, tokenData, chatId);
                    if (tokenData != null)
                    {
                        return tokenData.AccessToken;
                    }
                }
            }
        }
        catch
        {
            // Не удалось прочитать токен
        }

        return null;
    }

    public async Task<TokenData?> RefreshTokenAsync(string accessToken, string refreshToken, string chatId)
    {
        var botUserState = await _redis.GetAsync<BotUserState>(chatId);
        if (botUserState != null)
        {
            botUserState.IsRefreshing = true;
        }
        await _redis.SetAsync(chatId, botUserState);
        
        try
        {
            if (string.IsNullOrEmpty(accessToken) || string.IsNullOrEmpty(refreshToken))
            {
                await ClearTokensAsync(chatId);
                return null;
            }

            Console.WriteLine("Starting token refresh...");

            var refreshRequest = new
            {
                AccessToken = accessToken,
                RefreshToken = refreshToken
            };

            var json = JsonSerializer.Serialize(refreshRequest);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await _httpClient.PostAsync($"{_baseUrl}/auth/refresh-token", content);

            if (response.IsSuccessStatusCode)
            {
                var responseContent = await response.Content.ReadAsStringAsync();
                var tokenData = JsonSerializer.Deserialize<TokenData>(responseContent, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                if (tokenData != null)
                {
                    Console.WriteLine("Token refresh successful");

                    // Успех
                    if (botUserState != null)
                    {
                        return tokenData;
                    }
                }
            }
            else if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
            {
                Console.WriteLine("Refresh token is invalid");
                await ClearTokensAsync(chatId);
                OnTokensInvalid?.Invoke();
            }
            else
            {
                Console.WriteLine($"Token refresh failed: {response.StatusCode}");
            }

            return null;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Token refresh error: {ex.Message}");
            return null;
        }
        finally
        {
        }
    }

    public async Task SaveAsync(BotUserState? botUserState, TokenData? tokenData, string chatId)
    {
        if (tokenData != null)
        {
            // если успешно изменили токены, сохраняем в кеш на срок действия refreshToken
            var expiresIn = tokenData.ExpiresIn;
            botUserState.AccessToken = tokenData.AccessToken;
            botUserState.RefreshToken = tokenData.RefreshToken;
            botUserState.IsRefreshing = false;
            await _redis.SetAsync(chatId, botUserState, TimeSpan.FromDays(expiresIn));
        }
        // Если не удалось обновить токены и это связано с отсутствием интернет соединения или ошибки сервера
        // Убираем refresh ожидание
        else if (botUserState != null && 
                 (!string.IsNullOrEmpty(botUserState.AccessToken) ||
                  !string.IsNullOrEmpty(botUserState.RefreshToken)))
        {
            botUserState.IsRefreshing = false;
            await _redis.SetAsync(chatId, botUserState);
        }
    }

    public async Task ClearTokensAsync(string chatId)
    {
        var botUserState = await _redis.GetAsync<BotUserState>(chatId);
        if (botUserState != null)
        {
            botUserState.AccessToken = null;
            botUserState.RefreshToken = null;
            botUserState.IsRefreshing = false;
            await _redis.SetAsync(chatId, botUserState);
        }
    }

    public event Action? OnTokensRefreshed;
    public event Action? OnTokensInvalid;
}
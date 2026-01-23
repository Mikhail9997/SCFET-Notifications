using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Application.Services;
using TelegramBotAdministrators.Models;

namespace TelegramBotAdministrators.Services;

public interface IApiService
{
    Task<AuthResponse<LoginResponseDto>> LoginAsync(LoginDto loginDto);
    Task<RequestResult<UserProfileDto?>> GetProfileAsync(string token);
    Task<int> TestAuthorization(string token);
    Task<GroupCreateResponse> CreateGroup(string token, GroupDto groupDto);
    Task<List<User>> GetAdministratorsAsync();
    Task<UserActivateDto> ActivateUser(Guid userId, string token);
    Task<UserActivateDto> DeactivateUser(Guid userId, string token);
    Task<UserActivateDto> RemoveUser(Guid userId, string token);
    Task<RequestResult<bool>> LogoutAsync(Guid userId, string token);
}

public class ApiService:IApiService
{
    private readonly HttpClient _httpClient;
    private readonly string _baseUrl;
    private readonly RedisService _redis;

    public ApiService(string baseUrl, RedisService redis)
    {
        _baseUrl = baseUrl;
        _redis = redis;
        _httpClient = new HttpClient();
        _httpClient.Timeout = TimeSpan.FromSeconds(30);
    }

    private void AddAuthHeader(string token)
    {
        _httpClient.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", token);
    }
    
    public async Task<AuthResponse<LoginResponseDto>> LoginAsync(LoginDto loginDto)
    {
        try
        {
            var response = await _httpClient.PostAsJsonAsync($"{_baseUrl}/auth/login", loginDto);
            var content = await response.Content.ReadAsStringAsync();
            
            if (response.IsSuccessStatusCode)
            {
                var authResult = JsonSerializer.Deserialize<AuthResponse<LoginResponseDto>>(
                    content, 
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                return authResult;
            }
            return JsonSerializer.Deserialize<AuthResponse<LoginResponseDto>>(content, new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) 
                   ?? new AuthResponse<LoginResponseDto>()
            {
                Message = "Произошла неизвестная ошибка",
                Success = false
            };
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error during login: {ex.Message}");
            return new AuthResponse<LoginResponseDto>()
            {
                Message = "Произошла неизвестная ошибка",
                Success = false
            };
        }
    }

    public async Task<RequestResult<UserProfileDto?>> GetProfileAsync(string token)
    {
        AddAuthHeader(token);
        try
        {
            var response = await _httpClient.GetAsync($"{_baseUrl}/users/profile");

            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync();
                var userProfileResult = JsonSerializer.Deserialize<UserProfileDto>(
                    content, 
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                
                return new RequestResult<UserProfileDto?>{Data = userProfileResult, Code = (int)response.StatusCode};
            }
            return new RequestResult<UserProfileDto?>{Data = null, Code = (int)response.StatusCode};
        }
        catch(Exception ex)
        {
            Console.WriteLine($"Error during get profile: {ex.Message}");
        }
        return new RequestResult<UserProfileDto?>{Data = null, Code = 500};;
    }

    public async Task<int> TestAuthorization(string token)
    {
        AddAuthHeader(token);
        try
        {
            var response = await _httpClient.GetAsync($"{_baseUrl}/auth/test-authorization");

            return (int)response.StatusCode;
        }
        catch(Exception ex)
        {
            Console.WriteLine($"Error during test authorization : {ex.Message}");
        }

        return 503; // сервис недоступен;
    }

    public async Task<GroupCreateResponse> CreateGroup(string token, GroupDto groupDto)
    {
        try
        {
            AddAuthHeader(token);

            var response = await _httpClient.PostAsJsonAsync($"{_baseUrl}/groups", groupDto);
            var content = await response.Content.ReadAsStringAsync();
            
            if (!string.IsNullOrEmpty(content))
            {
                var data = JsonSerializer.Deserialize<GroupCreateResponse>(content, 
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                return data;
            }
        }
        catch(Exception ex)
        {
            Console.WriteLine($"Create group error: {ex.Message}");
        }

        return new GroupCreateResponse{Message = "Произошла неизвестная ошибка. Попробуйте снова", Success = false};
    }

    public async Task<List<User>> GetAdministratorsAsync()
    {
        try
        {
            var response = await _httpClient.GetAsync($"{_baseUrl}/users/administrators-common");

            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync();

                return JsonSerializer.Deserialize<List<User>>(content, new JsonSerializerOptions
                       {
                           PropertyNameCaseInsensitive = true
                       })
                       ?? new List<User>();
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Get administrators error: {ex.Message}");
        }
        return new List<User>();
    }

    public async Task<UserActivateDto> ActivateUser(Guid userId, string token)
    {
        try
        {
            AddAuthHeader(token);
            var response = await _httpClient.PutAsync($"{_baseUrl}/users/{userId}/activate", null);
            var contentJson = await response.Content.ReadAsStringAsync();
            if (!string.IsNullOrEmpty(contentJson))
            {
                var content = JsonSerializer.Deserialize<UserActivateDto>(contentJson, new JsonSerializerOptions()
                {
                    PropertyNameCaseInsensitive = true
                });
                return content ?? new UserActivateDto() { Message = "Произошла неизвестная ошибка", Success = false};
            }
        }
        catch(Exception ex)
        {
            Console.WriteLine($"Error during activate User: {ex.Message}");
        }

        return new UserActivateDto() { Message = "Произошла неизвестная ошибка", Success = false};
    }

    public async Task<UserActivateDto> DeactivateUser(Guid userId, string token)
    {
        try
        {
            AddAuthHeader(token);
            var response = await _httpClient.PutAsync($"{_baseUrl}/users/{userId}/deactivate", null);
            var contentJson = await response.Content.ReadAsStringAsync();
            if (!string.IsNullOrEmpty(contentJson))
            {
                var content = JsonSerializer.Deserialize<UserActivateDto>(contentJson, new JsonSerializerOptions()
                {
                    PropertyNameCaseInsensitive = true
                });
                return content ?? new UserActivateDto() { Message = "Произошла неизвестная ошибка", Success = false};
            }
        }
        catch(Exception ex)
        {
            Console.WriteLine($"Error during deactivate User: {ex.Message}");
        }

        return new UserActivateDto() { Message = "Произошла неизвестная ошибка", Success = false};
    }

    public async Task<UserActivateDto> RemoveUser(Guid userId, string token)
    {
        try
        {
            AddAuthHeader(token);
            var response = await _httpClient.DeleteAsync($"{_baseUrl}/users/{userId}/remove");
            var contentJson = await response.Content.ReadAsStringAsync();
            if (!string.IsNullOrEmpty(contentJson))
            {
                var content = JsonSerializer.Deserialize<UserActivateDto>(contentJson, new JsonSerializerOptions()
                {
                    PropertyNameCaseInsensitive = true
                });
                return content ?? new UserActivateDto() { Message = "Произошла неизвестная ошибка", Success = false};
            }
        }
        catch(Exception ex)
        {
            Console.WriteLine($"Error during remove User: {ex.Message}");
        }

        return new UserActivateDto() { Message = "Произошла неизвестная ошибка", Success = false};
    }

    public async Task<RequestResult<bool>> LogoutAsync(Guid userId, string token)
    {
        try
        {
            AddAuthHeader(token);
            var response = await _httpClient.PutAsync($"{_baseUrl}/auth/{userId}/logout", null);

            if (response.IsSuccessStatusCode)
            {
                return new RequestResult<bool>
                {
                    Data = true,
                    Code = (int)response.StatusCode
                };
            }
            return new RequestResult<bool>
            {
                Data = false,
                Code = (int)response.StatusCode
            };
        }
        catch(Exception ex)
        {
            Console.WriteLine($"Error during logout User: {ex.Message}");
        }

        return new RequestResult<bool>
        {
            Data = false,
            Code = 500
        };
    }
}
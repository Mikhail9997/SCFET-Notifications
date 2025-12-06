using System.Text.Json;
using System.Text.Json.Serialization;
using TelegramBotEmployee.Models;

namespace TelegramBotEmployee.Services;

public class ApiService
{
    private readonly HttpClient _httpClient;
    private readonly string _baseUrl;

    public ApiService(string baseUrl)
    {
        _baseUrl = baseUrl;
        _httpClient = new HttpClient();
        _httpClient.Timeout = TimeSpan.FromSeconds(30);
    }

    public async Task<ResponseDto> RegisterAsync(RegisterRequest request)
    {
        var responseError = new ResponseDto()
        {
            Message="Произошла неизвестная ошибка при регистрации",
            Success = false
        };
        try
        {
            var json = JsonSerializer.Serialize(request);
            var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");
            ResponseDto? responseDto;
            
            var response = await _httpClient.PostAsync($"{_baseUrl}/auth/register-employee", content);
            var data = await response.Content.ReadAsStringAsync();
            if (response.IsSuccessStatusCode)
            {
                responseDto = JsonSerializer.Deserialize<ResponseDto>(data, new JsonSerializerOptions()
                {
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                    PropertyNameCaseInsensitive = true
                });
                return responseDto ?? responseError;
            }
            responseDto = JsonSerializer.Deserialize<ResponseDto>(data, new JsonSerializerOptions()
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                PropertyNameCaseInsensitive = true
            });
            return responseDto ?? responseError;

        }
        catch(Exception ex)
        {
            Console.WriteLine($"Error during registration: {ex.Message}");
            return responseError;
        }
    }
    
    public async Task<bool> CheckEmailExistsAsync(string email)
    {
        try
        {
            var response = await _httpClient.GetAsync($"{_baseUrl}/auth/check-email-exist/{email}");

            if (response.IsSuccessStatusCode)
            {
                return true;
            }

            return false;
        }
        catch(Exception ex)
        {
            return false;
        }
    }
}
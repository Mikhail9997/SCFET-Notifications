using System.Text.Json;
using System.Text.Json.Serialization;
using Newtonsoft.Json;
using TelegramBot.Models;
using JsonSerializer = System.Text.Json.JsonSerializer;

namespace TelegramBot.Services;

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

    public async Task<List<GroupResponse>?> GetGroupsAsync()
    {
        try
        {
            var response = await _httpClient.GetAsync($"{_baseUrl}/groups");
            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync();
                return JsonConvert.DeserializeObject<List<GroupResponse>>(content);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error getting groups: {ex.Message}");
        }

        return null;
    }

    public async Task<RegistrationResult> RegisterStudentAsync(RegisterRequest request)
    {
        try
        {
            var json = JsonConvert.SerializeObject(request);
            var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");

            var response = await _httpClient.PostAsync($"{_baseUrl}/auth/register", content);
            if (response.IsSuccessStatusCode)
            {
                return RegistrationResult.Success;
            }
            else
            {
                var errorContent = await response.Content.ReadAsStringAsync();

                var registerError = JsonSerializer.Deserialize<RegisterError>(errorContent, new JsonSerializerOptions
                {
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                    PropertyNameCaseInsensitive = true
                });
                Console.WriteLine($"Registration failed: {errorContent}");
                
                return registerError?.Type switch
                {
                    RegistrationResult.DeviceTokenAlreadyExists => registerError.Type,
                    RegistrationResult.DeviceTokenNullError => registerError.Type,
                    _ => RegistrationResult.UnknownError
                };
            }
        }
        catch(Exception ex)
        {
            Console.WriteLine($"Error during registration: {ex.Message}");
            return RegistrationResult.UnknownError;
        }
    }

    public async Task<List<User>> GetStudents()
    {
        try
        {
            var response = await _httpClient.GetAsync($"{_baseUrl}/users/students-common");

            if (response.IsSuccessStatusCode)
            {
                string content = await response.Content.ReadAsStringAsync();

                return JsonSerializer.Deserialize<List<User>>(content, new JsonSerializerOptions()
                {
                    PropertyNameCaseInsensitive = true
                }) ?? new List<User>();
            }
        }
        catch(Exception ex)
        {
            Console.WriteLine($"Error during GetStudents: {ex.Message}");
        }
        return new List<User>();
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
    
    public async Task<bool> CheckPhoneNumberExistsAsync(string phoneNumber)
    {
        try
        {
            var response = await _httpClient.GetAsync($"{_baseUrl}/auth/check-phone-number-exist/{phoneNumber}");

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
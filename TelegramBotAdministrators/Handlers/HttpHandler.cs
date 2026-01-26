using Application.Services;
using Microsoft.Extensions.Configuration;

namespace TelegramBotAdministrators.Handlers;

public class HttpHandler:DelegatingHandler
{
    private readonly RedisService _redis;
    private readonly IConfiguration _configuration;
    
    private readonly HashSet<string> _excludedPaths = new()
    {
        "/api/auth/login",
        "/api/auth/refresh-token",
        "/api/auth/register",
        "/api/auth/register-employee",
        "/api/auth/check-email-exist"
    };
    private string _baseUrl;

    public HttpHandler(RedisService redis, IConfiguration configuration)
    {
        _redis = redis;
        _configuration = configuration;

        _baseUrl = configuration["Api:BaseUrl"] ?? "";
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
         return await base.SendAsync(request, cancellationToken);
    }
    
    private bool IsExcludedPath(string path)
    {
        return _excludedPaths.Any(excluded =>
            path.Equals(excluded, StringComparison.OrdinalIgnoreCase) ||
            path.StartsWith(excluded + "/", StringComparison.OrdinalIgnoreCase));
    }
}
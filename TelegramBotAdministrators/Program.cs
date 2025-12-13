
using Application.Services;
using dotenv.net;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;
using TelegramBotAdministrators.Handlers;
using TelegramBotAdministrators.Services;
using KafkaConsumerService = TelegramBotAdministrators.Services.KafkaConsumerService;

// Получаем путь к исполняемой директории
var executionPath = AppContext.BaseDirectory;
// Поднимаемся на 3 уровня вверх к корню проекта
var projectPath = Directory.GetParent(executionPath)?.Parent?.Parent?.Parent?.Parent?.FullName;
var envPath = Path.Combine(projectPath ?? executionPath, ".env");

// Загружаем .env файл
if (File.Exists(envPath))
{
    DotEnv.Load(options: new DotEnvOptions(
        envFilePaths: new[] { envPath },
        ignoreExceptions: false
    ));
}
else
{
    Console.WriteLine($"Warning: .env file not found at {envPath}");
}

var host = Host.CreateDefaultBuilder(args)
    .ConfigureAppConfiguration((context, config) =>
    {
        config.AddJsonFile("appsettings.json", optional: false, reloadOnChange: true);
        config.AddEnvironmentVariables();
    })
    .ConfigureServices((context, services) =>
    {
        var configuration = context.Configuration;

        services.AddSingleton<IConnectionMultiplexer>(provider =>
        {
            string connectionString = configuration["Redis:ConnectionStrings"]
                ?? throw new ArgumentException("redis ConnectionStrings is not configured");;
            return ConnectionMultiplexer.Connect(connectionString);
        });
        services.AddSingleton<RedisService>();
        services.AddSingleton<IApiService, ApiService>(provider =>
        {
            var apiBaseUrl = configuration["Api:BaseUrl"] 
                             ?? "http://localhost:5050";

            var redis = provider.GetRequiredService<RedisService>();
            return new ApiService(apiBaseUrl, redis);
        });
        services.AddSingleton<LoginHandler>();
        services.AddSingleton<GroupCreationHandler>();
        
        services.AddSingleton<BotService>(provider =>
        {
            var botToken = configuration["TelegramBot:Token"]
                           ?? throw new ArgumentException("Telegram bot token is not configured");

            var apiService =  provider.GetRequiredService<IApiService>();
            var logger = provider.GetRequiredService<ILogger<BotService>>();
            var redis = provider.GetRequiredService<RedisService>();
            var loginHandler = provider.GetRequiredService<LoginHandler>();
            var groupCreationHandler = provider.GetRequiredService<GroupCreationHandler>();
                        
            return new BotService(botToken, logger, apiService, redis, loginHandler, groupCreationHandler);
        });
        
        services.AddHostedService<BotWorker>();
        services.AddHostedService<KafkaConsumerService>();
    })
    .Build();

    await host.RunAsync();

public class BotWorker : IHostedService
{
    private readonly BotService _botService;
    private readonly ILogger<BotWorker> _logger;

    public BotWorker(BotService botService, ILogger<BotWorker> logger)
    {
        _botService = botService;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Starting Telegram Bot Worker...");
        await _botService.StartAsync();
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Stopping Telegram Bot Worker...");
        return Task.CompletedTask;
    }
}
    
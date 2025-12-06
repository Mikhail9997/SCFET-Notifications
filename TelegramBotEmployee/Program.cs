
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;
using TelegramBotEmployee.Services;

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
                                      ?? throw new ArgumentException("redis ConnectionStrings is not configured");
            ;
            return ConnectionMultiplexer.Connect(connectionString);
        });
        services.AddSingleton<RedisCache>();

        services.AddSingleton<ApiService>(provider =>
        {
            var apiBaseUrl = configuration["Api:BaseUrl"]
                             ?? "http://localhost:5050";

            var redis = provider.GetRequiredService<RedisCache>();
            return new ApiService(apiBaseUrl);
        });

        services.AddSingleton<BotService>(provider =>
        {
            var botToken = configuration["TelegramBot:Token"]
                           ?? throw new ArgumentException("Telegram bot token is not configured");

            var apiService = provider.GetRequiredService<ApiService>();
            var logger = provider.GetRequiredService<ILogger<BotService>>();
            var redis = provider.GetRequiredService<RedisCache>();

            return new BotService(botToken, logger, apiService, redis);
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
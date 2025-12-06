// See https://aka.ms/new-console-template for more information

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using TelegramBot.Services;


var host = Host.CreateDefaultBuilder(args)
    .ConfigureAppConfiguration((context, config) =>
    {
        config.AddJsonFile("appsettings.json", optional: false, reloadOnChange: true);
        config.AddEnvironmentVariables();
    })
    .ConfigureServices((context, services) =>
    {
        var configuration = context.Configuration;

        services.AddSingleton<BotService>(provider =>
        {
            var botToken = configuration["TelegramBot:Token"] 
                           ?? throw new ArgumentException("Telegram bot token is not configured");
                        
            var apiBaseUrl = configuration["Api:BaseUrl"] 
                             ?? "http://localhost:5050";
                        
            var logger = provider.GetRequiredService<ILogger<BotService>>();
                        
            return new BotService(botToken, apiBaseUrl, logger);
        });
        
        services.AddHostedService<BotWorker>();
        services.AddHostedService<KafkaConsumerService>();
    })
    .ConfigureLogging(logging =>
    {
        logging.ClearProviders();
        logging.AddConsole();
        logging.SetMinimumLevel(LogLevel.Information);
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

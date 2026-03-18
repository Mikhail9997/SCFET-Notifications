using System.Text.Json;
using Confluent.Kafka;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using TelegramBotEmployee.Models;

namespace TelegramBotEmployee.Services;

public class KafkaConsumerService:BackgroundService
{
    private readonly IConsumer<Ignore, string> _consumer;
    private readonly ILogger<KafkaConsumerService> _logger;
    private readonly BotService _botService;
    private readonly string[] _topics;

    public KafkaConsumerService(
        ILogger<KafkaConsumerService> logger,
        IConfiguration configuration,
        BotService botService)
    {
        _logger = logger;
        _botService = botService;
        string userIsActiveTopic = configuration["Kafka:Topics:UserIsActive"] ?? "notifications.userIsActiveChange";
        string systemNotificationsTopic = configuration["Kafka:Topics:SystemNotifications"] ?? "system-notifications";

        _topics = [userIsActiveTopic, systemNotificationsTopic];

        var consumerConfig = new ConsumerConfig
        {
            BootstrapServers = configuration["Kafka:BootstrapServers"],
            GroupId = configuration["Kafka:GroupId"],
            AutoOffsetReset = AutoOffsetReset.Earliest,
            EnableAutoCommit = false
        };

        _consumer = new ConsumerBuilder<Ignore, string>(consumerConfig).Build();
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await Task.Delay(5000, stoppingToken);
        
        _consumer.Subscribe(_topics);
        _logger.LogInformation("Kafka Consumer Service started");

        // Запускаем потребление
        var consumingTask = Task.Run(() => StartConsuming(stoppingToken), stoppingToken);
    
        await consumingTask;
    }
    
    private async void StartConsuming(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var consumeResult = _consumer.Consume(TimeSpan.FromMilliseconds(1000));
            
                if (consumeResult != null)
                {
                    if (consumeResult.Message?.Value != null)
                    {
                        if (consumeResult.Topic == _topics[0])
                        {
                            await ProcessUserIsActiveAsync(consumeResult.Message.Value);
                        }
                        else if (consumeResult.Topic == _topics[1])
                        {
                            await ProcessSystemNotificationAsync(consumeResult.Message.Value);
                        }
                        else
                        {
                            _logger.LogWarning("Unknown topic: {Topic}", consumeResult.Topic);
                        }
                    }
                    _consumer.Commit(consumeResult);
                }
            }
            catch (ConsumeException ex)
            {
                _logger.LogError(ex, "Error consuming Kafka message: {Error}", ex.Error.Reason);
                Thread.Sleep(5000);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error in Kafka consumer");
                Thread.Sleep(5000);
            }
        }
    
        _consumer.Close();
    }
    
    private async Task ProcessUserIsActiveAsync(string messageJson)
    {
        try
        {
            var message = JsonSerializer.Deserialize<UserIsActiveMessage>(messageJson);
            if (message == null)
            {
                _logger.LogWarning("Failed to deserialize Kafka message: {Message}", messageJson);
                return;
            }

            _logger.LogInformation("Processing user is active event {UserId}", 
                message.UserId);

            await _botService.OnUserIsActiveEvent(message);
            _logger.LogInformation("Successfully processed user is active event {UserId}", message.UserId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing Kafka user register message");
        }
    }

    private async Task ProcessSystemNotificationAsync(string messageJson)
    {
        try
        {
            var message = JsonSerializer.Deserialize<MaintenanceNotificationMessage>(messageJson);
            if (message == null)
            {
                _logger.LogWarning("Failed to deserialize Kafka message: {Message}", messageJson);
                return;
            }

            _logger.LogInformation("Processing System Notification event");

            await _botService.SendMaintenanceNotificationAsync(message);
            _logger.LogInformation("Successfully processed System Notification event");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing System Notification message");
        }
    }
    
    public override void Dispose()
    {
        _consumer?.Dispose();
        base.Dispose();
    }
}
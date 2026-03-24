using System.Text.Json;
using Application.DTOs;
using Application.Hubs;
using Application.Messages.Kafka;
using Confluent.Kafka;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Application.Services;

    public class KafkaConsumerService : BackgroundService
    {
        private readonly IConsumer<Ignore, string> _consumer;
        private readonly ILogger<KafkaConsumerService> _logger;
        private readonly IServiceProvider _serviceProvider;
        private readonly IConfiguration _configuration;
        private readonly string _topic;

        public KafkaConsumerService(
            ILogger<KafkaConsumerService> logger,
            IServiceProvider serviceProvider,
            IConfiguration configuration)
        {
            _logger = logger;
            _serviceProvider = serviceProvider;
            _configuration = configuration;
            _topic = configuration["Kafka:Topics:Notifications"] ?? "notifications.all";

            var consumerConfig = new ConsumerConfig
            {
                BootstrapServers = configuration["Kafka:BootstrapServers"],
                GroupId = "notification-consumers",
                AutoOffsetReset = AutoOffsetReset.Earliest,
                EnableAutoCommit = false
            };

            _consumer = new ConsumerBuilder<Ignore, string>(consumerConfig).Build();
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            await Task.Delay(5000, stoppingToken);
    
            _consumer.Subscribe(_topic);
            _logger.LogInformation("Kafka Consumer Service started");

            // Запускаем потребление
            var consumingTask = Task.Run(() => StartConsuming(stoppingToken), stoppingToken);
    
            await consumingTask;
        }

        private void StartConsuming(CancellationToken stoppingToken)
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
                            _ = ProcessNotificationAsync(consumeResult.Message.Value);
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
        
        private async Task ProcessNotificationAsync(string messageJson)
        {
            try
            {
                var notificationMessage = JsonSerializer.Deserialize<NotificationKafkaMessage>(messageJson);
                if (notificationMessage == null)
                {
                    _logger.LogWarning("Failed to deserialize Kafka message: {Message}", messageJson);
                    return;
                }

                _logger.LogInformation("Processing notification {NotificationId} for {RecipientCount} recipients", 
                    notificationMessage.NotificationId, notificationMessage.RecipientUserIds.Count);

                using var scope = _serviceProvider.CreateScope();
                var hubContext = scope.ServiceProvider.GetRequiredService<IHubContext<NotificationHub>>();

                // Отправляем уведомление каждому целевому пользователю
                foreach (var userId in notificationMessage.RecipientUserIds)
                {
                    try
                    {
                        await hubContext.Clients.Group($"user_{userId}")
                            .SendAsync("ReceiveNotification", new NotificationDto
                            {
                                Id = notificationMessage.NotificationId,
                                Title = notificationMessage.Title,
                                Message = notificationMessage.Message,
                                Type = notificationMessage.Type.ToString(),
                                SenderName = notificationMessage.SenderName,
                                SenderRole = notificationMessage.SenderRole,
                                SenderId = notificationMessage.SenderId,
                                IsPersonal = notificationMessage.RecipientUserIds.Count == 1,
                                CreatedAt = notificationMessage.CreatedAt,
                                IsRead = false,
                                ImageUrl = !string.IsNullOrEmpty(notificationMessage.ImageUrl) ? $"{_configuration["CloudPud:Ip"]}{notificationMessage.ImageUrl}" : null
                            });
                        
                        _logger.LogDebug("Notification sent to user {UserId}", userId);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Failed to send notification to user {UserId}", userId);
                    }
                }

                _logger.LogInformation("Successfully processed notification {NotificationId}", notificationMessage.NotificationId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing Kafka notification message");
            }
        }

        public override void Dispose()
        {
            _consumer?.Dispose();
            base.Dispose();
        }
    }
    
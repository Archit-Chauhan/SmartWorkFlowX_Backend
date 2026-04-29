using Azure.Messaging.ServiceBus;
using Microsoft.EntityFrameworkCore;
using SmartWorkFlowX.Application.Services;
using SmartWorkFlowX.Infrastructure.Data;
using System.Text.Json;

namespace SmartWorkFlowX.Api.Workers
{
    public class BulkNotificationWorker : BackgroundService
    {
        private readonly ServiceBusClient _client;
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<BulkNotificationWorker> _logger;
        private readonly string _queueName;
        private ServiceBusProcessor _processor;

        public BulkNotificationWorker(
            ServiceBusClient client,
            IServiceProvider serviceProvider,
            ILogger<BulkNotificationWorker> logger,
            IConfiguration config)
        {
            _client = client;
            _serviceProvider = serviceProvider;
            _logger = logger;
            _queueName = config["AzureServiceBus:BulkNotificationQueue"] ?? "bulk-notifications";
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("BulkNotificationWorker is starting.");

            try
            {
                _processor = _client.CreateProcessor(_queueName, new ServiceBusProcessorOptions());

                _processor.ProcessMessageAsync += MessageHandler;
                _processor.ProcessErrorAsync += ErrorHandler;

                await _processor.StartProcessingAsync(stoppingToken);

                await Task.Delay(Timeout.Infinite, stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to start Service Bus Processor for Queue.");
            }
        }

        private async Task MessageHandler(ProcessMessageEventArgs args)
        {
            string body = args.Message.Body.ToString();
            
            try
            {
                var payload = JsonSerializer.Deserialize<JsonElement>(body);
                int? targetRoleId = payload.GetProperty("TargetRoleId").ValueKind == JsonValueKind.Null ? null : payload.GetProperty("TargetRoleId").GetInt32();
                string messageText = payload.GetProperty("Message").GetString();
                int senderId = payload.GetProperty("SenderId").GetInt32();

                using var scope = _serviceProvider.CreateScope();
                var dbContext = scope.ServiceProvider.GetRequiredService<SmartWorkflowXDbContext>();
                var notificationService = scope.ServiceProvider.GetRequiredService<INotificationService>();

                // Build query
                var query = dbContext.Users.AsQueryable();
                
                // If a specific role is selected, filter. Otherwise broadcast to all.
                if (targetRoleId.HasValue && targetRoleId.Value > 0)
                {
                    query = query.Where(u => u.RoleId == targetRoleId.Value);
                }
                
                // Exclude the sender themselves
                query = query.Where(u => u.UserId != senderId);

                var targetUserIds = await query.Select(u => u.UserId).ToListAsync();

                foreach (var userId in targetUserIds)
                {
                    await notificationService.SendNotificationAsync(userId, $"Broadcast: {messageText}");
                }

                _logger.LogInformation($"Bulk notification sent to {targetUserIds.Count} users.");

                await args.CompleteMessageAsync(args.Message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing bulk notification message");
            }
        }

        private Task ErrorHandler(ProcessErrorEventArgs args)
        {
            _logger.LogError(args.Exception, "Service Bus Queue Error");
            return Task.CompletedTask;
        }

        public override async Task StopAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("BulkNotificationWorker is stopping.");
            if (_processor != null)
            {
                await _processor.StopProcessingAsync(stoppingToken);
                await _processor.DisposeAsync();
            }
            await base.StopAsync(stoppingToken);
        }
    }
}

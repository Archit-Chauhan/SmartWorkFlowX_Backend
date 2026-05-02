using Azure.Messaging.ServiceBus;
using Microsoft.EntityFrameworkCore;
using SmartWorkFlowX.Application.Services;
using SmartWorkFlowX.Infrastructure.Data;
using System.Text.Json;

namespace SmartWorkFlowX.Api.Workers
{
    public class WorkflowNotificationWorker : BackgroundService
    {
        private readonly ServiceBusClient _client;
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<WorkflowNotificationWorker> _logger;
        private readonly string _topicName;
        private readonly string _subscriptionName = "notification-subscription";
        private ServiceBusProcessor _processor;

        public WorkflowNotificationWorker(
            ServiceBusClient client,
            IServiceProvider serviceProvider,
            ILogger<WorkflowNotificationWorker> logger,
            IConfiguration config)
        {
            _client = client;
            _serviceProvider = serviceProvider;
            _logger = logger;
            _topicName = config["AzureServiceBus:TopicName"] ?? "workflow-events";
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("WorkflowNotificationWorker is starting.");

            try
            {
                _processor = _client.CreateProcessor(_topicName, _subscriptionName, new ServiceBusProcessorOptions());

                _processor.ProcessMessageAsync += MessageHandler;
                _processor.ProcessErrorAsync += ErrorHandler;

                await _processor.StartProcessingAsync(stoppingToken);

                await Task.Delay(Timeout.Infinite, stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to start Service Bus Processor.");
            }
        }

        private async Task MessageHandler(ProcessMessageEventArgs args)
        {
            string body = args.Message.Body.ToString();
            
            try
            {
                var eventData = JsonSerializer.Deserialize<SmartWorkFlowX.Application.Dtos.SystemEventMessage>(body, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                if (eventData == null || string.IsNullOrWhiteSpace(eventData.NotificationMessage))
                {
                    await args.CompleteMessageAsync(args.Message);
                    return;
                }

                using var scope = _serviceProvider.CreateScope();
                var dbContext = scope.ServiceProvider.GetRequiredService<SmartWorkflowXDbContext>();
                var notificationService = scope.ServiceProvider.GetRequiredService<INotificationService>();

                if (eventData.TargetUserId.HasValue)
                {
                    // Targeted notification (e.g. Task Assigned)
                    await notificationService.SendNotificationAsync(eventData.TargetUserId.Value, eventData.NotificationMessage);
                }
                else if (eventData.EntityName == "Workflows")
                {
                    // Broadcast notification to Managers (e.g. Workflow Created)
                    var managerRole = await dbContext.Roles.FirstOrDefaultAsync(r => r.RoleName == "Manager");
                    if (managerRole != null)
                    {
                        var managerIds = await dbContext.Users
                            .Where(u => u.RoleId == managerRole.RoleId)
                            .Select(u => u.UserId)
                            .ToListAsync();

                        foreach (var managerId in managerIds)
                        {
                            await notificationService.SendNotificationAsync(managerId, eventData.NotificationMessage);
                        }
                    }
                }

                await args.CompleteMessageAsync(args.Message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing message");
            }
        }

        private Task ErrorHandler(ProcessErrorEventArgs args)
        {
            _logger.LogError(args.Exception, "Service Bus Error");
            return Task.CompletedTask;
        }

        public override async Task StopAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("WorkflowNotificationWorker is stopping.");
            if (_processor != null)
            {
                await _processor.StopProcessingAsync(stoppingToken);
                await _processor.DisposeAsync();
            }
            await base.StopAsync(stoppingToken);
        }
    }
}

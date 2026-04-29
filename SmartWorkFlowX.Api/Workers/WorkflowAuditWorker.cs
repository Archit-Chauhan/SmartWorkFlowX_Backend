using Azure.Messaging.ServiceBus;
using SmartWorkFlowX.Application.Services;
using SmartWorkFlowX.Domain.Entities;
using SmartWorkFlowX.Domain.Repositories;
using System.Text.Json;

namespace SmartWorkFlowX.Api.Workers
{
    public class WorkflowAuditWorker : BackgroundService
    {
        private readonly ServiceBusClient _client;
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<WorkflowAuditWorker> _logger;
        private readonly string _topicName;
        private readonly string _subscriptionName = "audit-subscription";
        private ServiceBusProcessor _processor;

        public WorkflowAuditWorker(
            ServiceBusClient client,
            IServiceProvider serviceProvider,
            ILogger<WorkflowAuditWorker> logger,
            IConfiguration config)
        {
            _client = client;
            _serviceProvider = serviceProvider;
            _logger = logger;
            _topicName = config["AzureServiceBus:TopicName"] ?? "workflow-events";
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("WorkflowAuditWorker is starting.");

            try
            {
                _processor = _client.CreateProcessor(_topicName, _subscriptionName, new ServiceBusProcessorOptions());

                _processor.ProcessMessageAsync += MessageHandler;
                _processor.ProcessErrorAsync += ErrorHandler;

                await _processor.StartProcessingAsync(stoppingToken);

                // Wait until the app shuts down
                await Task.Delay(Timeout.Infinite, stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to start Service Bus Processor. Ensure the Topic and Subscription exist in Azure.");
            }
        }

        private async Task MessageHandler(ProcessMessageEventArgs args)
        {
            string body = args.Message.Body.ToString();
            _logger.LogInformation($"Received message: {body}");

            try
            {
                var eventData = JsonSerializer.Deserialize<JsonElement>(body);
                int workflowId = eventData.GetProperty("WorkflowId").GetInt32();
                string action = eventData.GetProperty("Action").GetString();
                string title = eventData.GetProperty("WorkflowTitle").GetString();
                int actedBy = eventData.GetProperty("ActedByUserId").GetInt32();

                // Create a scope to resolve scoped services like repositories
                using var scope = _serviceProvider.CreateScope();
                var auditRepo = scope.ServiceProvider.GetRequiredService<IAuditLogRepository>();

                await auditRepo.AddAsync(new AuditLog
                {
                    UserId = actedBy,
                    Action = $"Workflow Event Broadcast: '{title}' was {action}.",
                    EntityName = "Workflows",
                    Timestamp = DateTime.UtcNow
                });
                
                await auditRepo.SaveAsync();

                // Complete the message. message is deleted from the subscription. 
                await args.CompleteMessageAsync(args.Message);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing message");
                // Message will be abandoned and redelivered
            }
        }

        private Task ErrorHandler(ProcessErrorEventArgs args)
        {
            _logger.LogError(args.Exception, "Service Bus Error");
            return Task.CompletedTask;
        }

        public override async Task StopAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("WorkflowAuditWorker is stopping.");
            if (_processor != null)
            {
                await _processor.StopProcessingAsync(stoppingToken);
                await _processor.DisposeAsync();
            }
            await base.StopAsync(stoppingToken);
        }
    }
}

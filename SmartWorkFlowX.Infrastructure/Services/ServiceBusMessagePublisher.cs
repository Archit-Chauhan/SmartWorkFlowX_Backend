using Azure.Messaging.ServiceBus;
using Microsoft.Extensions.Configuration;
using SmartWorkFlowX.Application.Services;
using System.Text.Json;
using System.Threading.Tasks;

namespace SmartWorkFlowX.Infrastructure.Services
{
    public class ServiceBusMessagePublisher : IMessagePublisher
    {
        private readonly ServiceBusClient _client;
        private readonly string _topicName;
        private readonly string _queueName;

        public ServiceBusMessagePublisher(ServiceBusClient client, IConfiguration config)
        {
            _client = client;
            _topicName = config["AzureServiceBus:TopicName"] ?? "workflow-events";
            _queueName = config["AzureServiceBus:BulkNotificationQueue"] ?? "bulk-notifications";
        }

        public async Task PublishWorkflowEventAsync(int workflowId, string action, string workflowTitle, int actedByUserId)
        {
            var sender = _client.CreateSender(_topicName);
            
            var payload = new
            {
                WorkflowId = workflowId,
                Action = action, // e.g., "Activated", "Deactivated"
                WorkflowTitle = workflowTitle,
                ActedByUserId = actedByUserId,
                Timestamp = DateTime.UtcNow
            };

            var message = new ServiceBusMessage(JsonSerializer.Serialize(payload))
            {
                Subject = "WorkflowEvent",
                ContentType = "application/json"
            };

            await sender.SendMessageAsync(message);
        }

        public async Task PublishBulkNotificationAsync(int? targetRoleId, string messageText, int senderId)
        {
            var sender = _client.CreateSender(_queueName);
            
            var payload = new
            {
                TargetRoleId = targetRoleId,
                Message = messageText,
                SenderId = senderId,
                Timestamp = DateTime.UtcNow
            };

            var message = new ServiceBusMessage(JsonSerializer.Serialize(payload))
            {
                Subject = "BulkNotification",
                ContentType = "application/json"
            };

            await sender.SendMessageAsync(message);
        }
    }
}

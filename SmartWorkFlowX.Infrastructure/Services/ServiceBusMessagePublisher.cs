using Azure.Messaging.ServiceBus;
using Microsoft.Extensions.Configuration;
using SmartWorkFlowX.Application.Services;
using System.Text.Json;

public class ServiceBusMessagePublisher : IMessagePublisher
{
    private readonly ServiceBusClient _client;
    private readonly string _queueName;

    public ServiceBusMessagePublisher(ServiceBusClient client, IConfiguration config)
    {
        _client = client;
        _queueName = config["AzureServiceBus:BulkNotificationQueue"] ?? "bulk-notifications";
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

    public async Task PublishSystemEventAsync(SmartWorkFlowX.Application.Dtos.SystemEventMessage eventMessage)
    {
        // For System Events, we will publish to the 'workflow-events' topic
        // Wait, I need access to the topic name from config. Let's hardcode it to "workflow-events" for simplicity since it's the standard, or use a new sender.
        // Actually, let's create a sender for "workflow-events".
        var topicSender = _client.CreateSender("workflow-events");
        
        var message = new ServiceBusMessage(JsonSerializer.Serialize(eventMessage))
        {
            Subject = eventMessage.EventType,
            ContentType = "application/json"
        };
        
        await topicSender.SendMessageAsync(message);
    }
}
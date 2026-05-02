using SmartWorkFlowX.Application.Services;

namespace SmartWorkFlowX.Infrastructure.Services;

/// <summary>
/// A no-op fallback publisher used when Azure Service Bus is not configured.
/// Allows the application to boot and serve requests without Service Bus.
/// </summary>
public class NoOpMessagePublisher : IMessagePublisher
{
    public Task PublishBulkNotificationAsync(int? targetRoleId, string message, int senderId)
    {
        // Intentionally a no-op — Service Bus is not configured in this environment.
        return Task.CompletedTask;
    }

    public Task PublishSystemEventAsync(SmartWorkFlowX.Application.Dtos.SystemEventMessage eventMessage)
    {
        return Task.CompletedTask;
    }
}

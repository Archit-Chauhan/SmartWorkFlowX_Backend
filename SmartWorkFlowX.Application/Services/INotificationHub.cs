namespace SmartWorkFlowX.Application.Services
{
    /// <summary>
    /// Typed interface for SignalR clients receiving notifications.
    /// Defined in Domain so Infrastructure can reference it without
    /// depending on the Api project.
    /// </summary>
    public interface INotificationClient
    {
        Task ReceiveNotification(object notification);
    }

    /// <summary>
    /// Marker hub interface for use with IHubContext in Infrastructure.
    /// </summary>
    public interface INotificationHub { }
}



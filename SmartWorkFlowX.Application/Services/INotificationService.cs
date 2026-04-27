namespace SmartWorkFlowX.Application.Services
{
    /// <summary>
    /// Contract for sending and pushing notifications (DB + SignalR).
    /// </summary>
    public interface INotificationService
    {
        Task SendNotificationAsync(int userId, string message);
    }
}



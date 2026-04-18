namespace SmartWorkFlowX.Application.Interface
{
    public interface INotificationService
    {
        Task SendNotificationAsync(int userId, string message);
    }
}
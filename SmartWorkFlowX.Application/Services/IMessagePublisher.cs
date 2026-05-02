using System.Threading.Tasks;

namespace SmartWorkFlowX.Application.Services
{
    public interface IMessagePublisher
    {
        Task PublishBulkNotificationAsync(int? targetRoleId, string message, int senderId);
        Task PublishSystemEventAsync(SmartWorkFlowX.Application.Dtos.SystemEventMessage eventMessage);
    }
}

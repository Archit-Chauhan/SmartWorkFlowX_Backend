using System.Threading.Tasks;

namespace SmartWorkFlowX.Application.Services
{
    public interface IMessagePublisher
    {
        Task PublishWorkflowEventAsync(int workflowId, string action, string workflowTitle, int actedByUserId);
        Task PublishBulkNotificationAsync(int? targetRoleId, string message, int senderId);
    }
}

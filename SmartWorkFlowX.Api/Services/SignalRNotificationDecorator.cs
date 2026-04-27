using Microsoft.AspNetCore.SignalR;
using SmartWorkFlowX.Api.Hubs;
using SmartWorkFlowX.Application.Services;

namespace SmartWorkFlowX.Api.Services
{
    /// <summary>
    /// Decorator that wraps the base INotificationService (DB persistence)
    /// and additionally pushes real-time notifications via SignalR.
    /// Lives in the Api layer so it can freely reference NotificationHub.
    /// </summary>
    public class SignalRNotificationDecorator : INotificationService
    {
        private readonly INotificationService _inner;
        private readonly IHubContext<NotificationHub, INotificationClient> _hubContext;

        public SignalRNotificationDecorator(
            INotificationService inner,
            IHubContext<NotificationHub, INotificationClient> hubContext)
        {
            _inner = inner;
            _hubContext = hubContext;
        }

        public async Task SendNotificationAsync(int userId, string message)
        {
            // 1. Persist to DB via the inner (Infrastructure) service
            await _inner.SendNotificationAsync(userId, message);

            // 2. Real-time push via SignalR to user's personal group
            await _hubContext.Clients
                .Group($"user_{userId}")
                .ReceiveNotification(new
                {
                    UserId = userId,
                    Message = message,
                    IsRead = false,
                    CreatedAt = DateTime.UtcNow
                });
        }
    }
}



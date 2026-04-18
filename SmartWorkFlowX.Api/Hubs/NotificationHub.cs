using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using SmartWorkFlowX.Application.Interface;
using System.Security.Claims;

namespace SmartWorkFlowX.Api.Hubs
{
    [Authorize]
    public class NotificationHub : Hub<INotificationClient>, INotificationHub
    {
        /// <summary>
        /// When a client connects, they are added to a personal group
        /// named "user_{userId}" so we can push targeted notifications.
        /// The client must provide the JWT token via query string (?access_token=)
        /// or Authorization header, depending on transport.
        /// </summary>
        public override async Task OnConnectedAsync()
        {
            var userId = Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (userId != null)
                await Groups.AddToGroupAsync(Context.ConnectionId, $"user_{userId}");

            await base.OnConnectedAsync();
        }

        public override async Task OnDisconnectedAsync(Exception? exception)
        {
            var userId = Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (userId != null)
                await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"user_{userId}");

            await base.OnDisconnectedAsync(exception);
        }
    }
}

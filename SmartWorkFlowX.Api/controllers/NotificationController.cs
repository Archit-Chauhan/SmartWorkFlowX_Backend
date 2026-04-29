using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmartWorkFlowX.Application.Services;
using System.Security.Claims;

namespace SmartWorkFlowX.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class NotificationController : ControllerBase
    {
        private readonly INotificationQueryService _notificationQueryService;
        private readonly IMessagePublisher _messagePublisher;

        public NotificationController(INotificationQueryService notificationQueryService, IMessagePublisher messagePublisher)
        {
            _notificationQueryService = notificationQueryService;
            _messagePublisher = messagePublisher;
        }

        public class BroadcastRequest
        {
            public int? RoleId { get; set; }
            public string Message { get; set; } = string.Empty;
        }

        // GET: api/Notification
        [HttpGet]
        public async Task<IActionResult> GetMyNotifications(
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 20)
        {
            var (data, total) = await _notificationQueryService.GetPagedAsync(GetUserId(), page, pageSize);
            return Ok(new { total, page, pageSize, data });
        }

        // GET: api/Notification/unread-count
        [HttpGet("unread-count")]
        public async Task<IActionResult> GetUnreadCount()
        {
            var unreadCount = await _notificationQueryService.GetUnreadCountAsync(GetUserId());
            return Ok(new { unreadCount });
        }

        // PUT: api/Notification/{id}/read
        [HttpPut("{id}/read")]
        public async Task<IActionResult> MarkAsRead(int id)
        {
            await _notificationQueryService.MarkAsReadAsync(id, GetUserId());
            return Ok(new { message = "Notification marked as read." });
        }

        // PUT: api/Notification/read-all
        [HttpPut("read-all")]
        public async Task<IActionResult> MarkAllAsRead()
        {
            await _notificationQueryService.MarkAllAsReadAsync(GetUserId());
            return Ok(new { message = "All notifications marked as read." });
        }

        // POST: api/Notification/broadcast
        [HttpPost("broadcast")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> BroadcastNotification([FromBody] BroadcastRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.Message))
            {
                return BadRequest(new { message = "Message is required." });
            }

            await _messagePublisher.PublishBulkNotificationAsync(request.RoleId, request.Message, GetUserId());
            return Ok(new { message = "Broadcast notification queued successfully." });
        }

        private int GetUserId()
            => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
    }
}



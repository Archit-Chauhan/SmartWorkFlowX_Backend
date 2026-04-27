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

        public NotificationController(INotificationQueryService notificationQueryService)
        {
            _notificationQueryService = notificationQueryService;
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

        private int GetUserId()
            => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
    }
}



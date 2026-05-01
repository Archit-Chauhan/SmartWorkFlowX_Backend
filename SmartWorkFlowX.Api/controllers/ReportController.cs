using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SmartWorkFlowX.Application.Services;

namespace SmartWorkFlowX.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class ReportController : ControllerBase
    {
        private readonly IReportService _reportService;

        public ReportController(IReportService reportService)
        {
            _reportService = reportService;
        }

        // GET: api/Report/analytics — available to ALL authenticated users (Dashboard home page)
        [HttpGet("analytics")]
        public async Task<IActionResult> GetAnalytics()
            => Ok(await _reportService.GetAnalyticsAsync());

        // GET: api/Report/audit-logs — Admin & Auditor only
        [HttpGet("audit-logs")]
        [Authorize(Roles = "Admin,Auditor")]
        public async Task<IActionResult> GetAuditLogs(
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 10)
        {
            return Ok(await _reportService.GetAuditLogsAsync(page, pageSize));
        }

        // GET: api/Report/overdue-tasks
        [HttpGet("overdue-tasks")]
        [Authorize(Roles = "Admin,Manager,Auditor")]
        public async Task<IActionResult> GetOverdueTasks()
            => Ok(await _reportService.GetOverdueTasksAsync());
    }
}


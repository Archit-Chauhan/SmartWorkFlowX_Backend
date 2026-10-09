using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using SmartWorkFlowX.Application.Dtos;
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

        // GET: api/Report/dashboard — any signed-in user. WHAT they get (task scope, sections) is decided on the server
        // from their role; the response lists the permissions granted so the UI renders only those sections.
        [HttpGet("dashboard")]
        public async Task<IActionResult> GetDashboard([FromQuery] DashboardRequest request)
            => Ok(await _reportService.GetDashboardAsync(request, GetUserId(), GetRole()));

        // GET: api/Report/dashboard/export — the same filters and scope as a CSV file; every export is audit-logged
        [HttpGet("dashboard/export")]
        public async Task<IActionResult> ExportDashboard([FromQuery] DashboardRequest request)
        {
            var result = await _reportService.ExportDashboardTasksAsync(request, GetUserId(), GetRole());
            // UTF-8 byte-order mark so Excel opens non-ASCII text correctly
            var bytes = System.Text.Encoding.UTF8.GetPreamble()
                .Concat(System.Text.Encoding.UTF8.GetBytes(result.Content))
                .ToArray();
            return File(bytes, "text/csv", result.FileName);
        }

        private int GetUserId()
            => int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

        private string? GetRole()
            => User.FindFirstValue(ClaimTypes.Role);

        // GET: api/Report/audit-logs — Admin & Auditor only
        [HttpGet("audit-logs")]
        [Authorize(Roles = "Admin,Auditor")]
        public async Task<IActionResult> GetAuditLogs(
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 10,
            [FromQuery] string? search = null)
        {
            return Ok(await _reportService.GetAuditLogsAsync(page, pageSize, search));
        }

        // GET: api/Report/audit-logs/export — Admin & Auditor only
        [HttpGet("audit-logs/export")]
        [Authorize(Roles = "Admin,Auditor")]
        public async Task<IActionResult> ExportAuditLogs([FromQuery] string? search = null)
        {
            var logs = await _reportService.GetAllAuditLogsAsync(search);
            var csv = new System.Text.StringBuilder();
            csv.AppendLine("UserName,Action,EntityName,Timestamp");
            foreach (var l in logs)
            {
                csv.AppendLine($"{EscapeCsv(l.UserName)},{EscapeCsv(l.Action)},{EscapeCsv(l.EntityName)},{l.Timestamp:yyyy-MM-ddTHH:mm:ssZ}");
            }
            return File(System.Text.Encoding.UTF8.GetBytes(csv.ToString()), "text/csv", "audit-logs.csv");
        }

        private static string EscapeCsv(string value)
            => value.Contains(',') || value.Contains('"') || value.Contains('\n')
                ? $"\"{value.Replace("\"", "\"\"")}\""
                : value;

        // GET: api/Report/overdue-tasks
        [HttpGet("overdue-tasks")]
        [Authorize(Roles = "Admin,Manager,Auditor")]
        public async Task<IActionResult> GetOverdueTasks()
            => Ok(await _reportService.GetOverdueTasksAsync());
    }
}


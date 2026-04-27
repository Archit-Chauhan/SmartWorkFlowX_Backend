using Microsoft.EntityFrameworkCore;
using SmartWorkFlowX.Application.Dtos;
using SmartWorkFlowX.Domain.Entities;
using SmartWorkFlowX.Domain.Repositories;
using SmartWorkFlowX.Infrastructure.Data;

namespace SmartWorkFlowX.Infrastructure.Repositories
{
    public class EfAuditLogRepository : IAuditLogRepository
    {
        private readonly SmartWorkflowXDbContext _context;
        public EfAuditLogRepository(SmartWorkflowXDbContext context) => _context = context;

        public async Task AddAsync(AuditLog log)
            => await _context.AuditLogs.AddAsync(log);

        public async Task<(List<AuditLog> Items, int Total)> GetPagedWithUserAsync(int page, int pageSize)
        {
            var total = await _context.AuditLogs.CountAsync();

            var items = await _context.AuditLogs
                .Include(l => l.User)
                .OrderByDescending(l => l.Timestamp)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            return (items, total);
        }

        public async Task SaveAsync()
            => await _context.SaveChangesAsync();
    }
}


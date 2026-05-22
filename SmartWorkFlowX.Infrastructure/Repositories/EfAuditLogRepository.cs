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

        public async Task<(List<AuditLog> Items, int Total)> GetPagedWithUserAsync(int page, int pageSize, string? search = null)
        {
            var query = _context.AuditLogs.Include(l => l.User).AsQueryable();
            if (!string.IsNullOrWhiteSpace(search))
                query = query.Where(l => l.Action.Contains(search) || l.EntityName.Contains(search) || (l.User != null && l.User.Name.Contains(search)));
            var total = await query.CountAsync();
            var items = await query
                .OrderByDescending(l => l.Timestamp)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            return (items, total);
        }

        public async Task<List<AuditLog>> GetAllWithUserAsync(string? search = null)
        {
            var query = _context.AuditLogs.Include(l => l.User).AsQueryable();
            if (!string.IsNullOrWhiteSpace(search))
                query = query.Where(l => l.Action.Contains(search) || l.EntityName.Contains(search) || (l.User != null && l.User.Name.Contains(search)));
            return await query.OrderByDescending(l => l.Timestamp).ToListAsync();
        }

        public async Task SaveAsync()
            => await _context.SaveChangesAsync();
    }
}


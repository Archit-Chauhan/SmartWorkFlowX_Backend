using Microsoft.EntityFrameworkCore;
using SmartWorkFlowX.Domain.Entities;
using SmartWorkFlowX.Domain.Repositories;
using SmartWorkFlowX.Infrastructure.Data;

namespace SmartWorkFlowX.Infrastructure.Repositories
{
    public class EfUserRepository : IUserRepository
    {
        private readonly SmartWorkflowXDbContext _context;
        public EfUserRepository(SmartWorkflowXDbContext context) => _context = context;

        public async Task<User?> GetByIdAsync(int userId)
            => await _context.Users.FindAsync(userId);

        public async Task<User?> GetByEmailWithRoleAsync(string email)
            => await _context.Users
                .IgnoreQueryFilters()
                .Include(u => u.Role)
                .Where(u => u.Email == email)
                .OrderBy(u => u.IsDeleted)  // active (false=0) before deleted (true=1)
                .FirstOrDefaultAsync();

        public async Task<User?> GetByIdIncludingDeletedAsync(int userId)
            => await _context.Users
                .IgnoreQueryFilters()
                .Include(u => u.Role)
                .FirstOrDefaultAsync(u => u.UserId == userId);

        // Users matching search + role (soft-deleted included; the Users filter is ignored on purpose).
        private IQueryable<User> SearchAndRole(string? search, int? roleId)
        {
            IQueryable<User> query = _context.Users.IgnoreQueryFilters();
            if (!string.IsNullOrWhiteSpace(search))
                query = query.Where(u => u.Name.Contains(search) || u.Email.Contains(search));
            if (roleId.HasValue)
            {
                int rid = roleId.Value;
                query = query.Where(u => u.RoleId == rid);
            }
            return query;
        }

        private static IQueryable<User> WithStatus(IQueryable<User> query, string status)
        {
            if (status == UserListQuery.StatusActive) return query.Where(u => !u.IsDeleted);
            if (status == UserListQuery.StatusDeactivated) return query.Where(u => u.IsDeleted);
            return query;
        }

        public async Task<IEnumerable<User>> GetAllWithRolesAsync(string? search, string status, int? roleId)
        {
            var query = WithStatus(SearchAndRole(search, roleId), status).Include(u => u.Role);
            return await query.OrderBy(u => u.IsDeleted).ThenBy(u => u.UserId).ToListAsync();
        }

        public async Task<UserListPage> GetUserListAsync(UserListQuery q)
        {
            IQueryable<User> baseQuery = SearchAndRole(q.Search, q.RoleId);

            // One grouped COUNT: SELECT IsDeleted, COUNT(*) ... GROUP BY IsDeleted (search + role applied, status ignored).
            var grouped = await baseQuery
                .GroupBy(u => u.IsDeleted)
                .Select(g => new { IsDeleted = g.Key, Count = g.Count() })
                .ToListAsync();

            int active = 0;
            int deactivated = 0;
            foreach (var row in grouped)
            {
                if (row.IsDeleted) deactivated = row.Count;
                else active = row.Count;
            }

            var counts = new UserListCounts { Active = active, Deactivated = deactivated, All = active + deactivated };

            int total = counts.All;
            if (q.Status == UserListQuery.StatusActive) total = active;
            else if (q.Status == UserListQuery.StatusDeactivated) total = deactivated;

            IQueryable<User> filtered = WithStatus(baseQuery, q.Status).Include(u => u.Role);
            IOrderedQueryable<User> ordered = ApplySort(filtered, q.Sort, q.Descending);

            var users = await ordered
                .ThenBy(u => u.UserId)
                .Skip((q.Page - 1) * q.Limit)
                .Take(q.Limit)
                .ToListAsync();

            return new UserListPage { Users = users, Total = total, Counts = counts };
        }

        private IOrderedQueryable<User> ApplySort(IQueryable<User> query, string sort, bool descending)
        {
            switch (sort)
            {
                case UserListQuery.SortRole:
                    return descending
                        ? query.OrderByDescending(u => u.Role == null ? "" : u.Role.RoleName)
                        : query.OrderBy(u => u.Role == null ? "" : u.Role.RoleName);

                case UserListQuery.SortStatus:
                    // IsDeleted false (active) sorts before true (deactivated) when ascending.
                    return descending
                        ? query.OrderByDescending(u => u.IsDeleted)
                        : query.OrderBy(u => u.IsDeleted);

                case UserListQuery.SortOpen:
                    // Correlated COUNT in ORDER BY. IgnoreQueryFilters also switches off the task
                    // soft-delete filter inside this query, so !t.IsDeleted is repeated explicitly.
                    return descending
                        ? query.OrderByDescending(u => _context.Tasks.Count(t =>
                            t.AssignedTo == u.UserId && !t.IsDeleted &&
                            (t.Status == AllTasksCounts.StatusPending || t.Status == AllTasksCounts.StatusInProgress)))
                        : query.OrderBy(u => _context.Tasks.Count(t =>
                            t.AssignedTo == u.UserId && !t.IsDeleted &&
                            (t.Status == AllTasksCounts.StatusPending || t.Status == AllTasksCounts.StatusInProgress)));

                case UserListQuery.SortAdded:
                    return descending
                        ? query.OrderByDescending(u => u.CreatedAt)
                        : query.OrderBy(u => u.CreatedAt);

                default:
                    return descending
                        ? query.OrderByDescending(u => u.Name)
                        : query.OrderBy(u => u.Name);
            }
        }

        public async Task<Dictionary<int, int>> GetOpenTaskCountsAsync(IReadOnlyCollection<int> userIds)
        {
            var result = new Dictionary<int, int>();
            if (userIds.Count == 0) return result;

            List<int?> ids = userIds.Select(i => (int?)i).ToList();

            // One grouped query for the whole page (the Tasks soft-delete filter applies here).
            var rows = await _context.Tasks
                .Where(t => ids.Contains(t.AssignedTo) &&
                            (t.Status == AllTasksCounts.StatusPending || t.Status == AllTasksCounts.StatusInProgress))
                .GroupBy(t => t.AssignedTo)
                .Select(g => new { UserId = g.Key, Count = g.Count() })
                .ToListAsync();

            foreach (var row in rows)
            {
                if (row.UserId.HasValue) result[row.UserId.Value] = row.Count;
            }
            return result;
        }

        public async Task<int> CountActiveAdminsAsync()
            => await _context.Users.CountAsync(u => u.Role != null && u.Role.RoleName == "Admin");

        public async Task<bool> EmailExistsAsync(string email)
            => await _context.Users.IgnoreQueryFilters().AnyAsync(u => u.Email == email);

        public async Task AddAsync(User user)
            => await _context.Users.AddAsync(user);

        public Task UpdateAsync(User user)
        {
            _context.Users.Update(user);
            return Task.CompletedTask;
        }

        public async Task SoftDeleteAsync(int userId)
        {
            var user = await _context.Users.FindAsync(userId);
            if (user != null)
            {
                user.IsDeleted = true;
                user.DeletedAt = DateTime.UtcNow;
            }
        }

        public async Task RestoreAsync(int userId)
        {
            var user = await _context.Users.FindAsync(userId);
            if (user != null)
            {
                user.IsDeleted = false;
                user.DeletedAt = null;
            }
        }

        public async Task SaveAsync()
            => await _context.SaveChangesAsync();
    }
}

using SmartWorkFlowX.Domain.Repositories;

namespace SmartWorkFlowX.Application.Services
{
    /// <summary>
    /// Pure validation/normalisation of the Admin "Manage Users" query string (no database).
    /// Invalid input throws ArgumentException, which the exception middleware maps to 400.
    /// </summary>
    public static class UserListQueryParser
    {
        public const int DefaultLimit = 10;

        public const string StatusMessage = "Status must be all, active or deactivated.";
        public const string SortMessage = "Sort must be one of: name, role, status, open, added.";
        public const string DirMessage = "Direction must be asc or desc.";

        public static UserListQuery Parse(int page, int limit, string? search, string? status, int? roleId, string? sort, string? dir)
        {
            if (page < 1) page = 1;
            if (limit < 1) limit = DefaultLimit;

            string? cleanSearch = null;
            if (!string.IsNullOrWhiteSpace(search))
                cleanSearch = search.Trim();

            // Check order matches the message order in the contract: status, sort, dir.
            string cleanStatus = ParseStatus(status);
            string cleanSort = Choice(sort, new[] { "name", "role", "status", "open", "added" }, SortMessage) ?? UserListQuery.SortName;
            string cleanDir = Choice(dir, new[] { "asc", "desc" }, DirMessage) ?? "asc";

            return new UserListQuery
            {
                Search = cleanSearch,
                Status = cleanStatus,
                RoleId = roleId,
                Sort = cleanSort,
                Descending = cleanDir == "desc",
                Page = page,
                Limit = limit
            };
        }

        /// <summary>Validates the status filter; null or blank means "all".</summary>
        public static string ParseStatus(string? status)
        {
            return Choice(status, new[] { "all", "active", "deactivated" }, StatusMessage) ?? UserListQuery.StatusAll;
        }

        private static string? Choice(string? value, string[] allowed, string message)
        {
            if (string.IsNullOrWhiteSpace(value)) return null;
            string trimmed = value.Trim();
            foreach (string option in allowed)
            {
                if (string.Equals(option, trimmed, StringComparison.Ordinal)) return option;
            }
            throw new ArgumentException(message);
        }
    }
}

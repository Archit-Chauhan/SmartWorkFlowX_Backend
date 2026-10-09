using SmartWorkFlowX.Application.Dtos;
using SmartWorkFlowX.Domain.Repositories;

namespace SmartWorkFlowX.Application.Services
{
    /// <summary>
    /// Pure validation/normalisation of the All Tasks query string (no database, no clock).
    /// Invalid input throws ArgumentException, which the exception middleware maps to 400.
    /// </summary>
    public static class AllTasksQueryParser
    {
        public const int DefaultLimit = 20;
        public const int MaxLimit = 100;
        public const int MaxSearchLength = 100;

        private static readonly string[] AllStatuses =
            { "Pending", "In Progress", "Completed", "Rejected", "Cancelled" };
        private static readonly string[] Priorities = { "Low", "Medium", "High" };
        private static readonly string[] Groups = { "all", "open", "completed", "closed" };
        private static readonly string[] Sorts = { "due", "created", "priority", "title", "workflow", "assignee", "status" };
        private static readonly string[] Dirs = { "asc", "desc" };

        public static AllTasksQuery Parse(AllTasksRequest request, DateTime now)
        {
            int page = request.Page ?? 1;
            if (page < 1)
                throw new ArgumentException("'page' must be 1 or greater.");

            int limit = request.Limit ?? DefaultLimit;
            if (limit < 1) limit = 1;
            if (limit > MaxLimit) limit = MaxLimit;

            string? search = null;
            if (!string.IsNullOrWhiteSpace(request.Q))
            {
                search = request.Q.Trim();
                if (search.Length > MaxSearchLength)
                    throw new ArgumentException("'q' must be at most " + MaxSearchLength + " characters.");
            }

            string group = Choice(request.Group, Groups, "group") ?? "all";
            string? status = Choice(request.Status, AllStatuses, "status");
            string? priority = Choice(request.Priority, Priorities, "priority");
            string sort = Choice(request.Sort, Sorts, "sort") ?? "due";
            string dir = Choice(request.Dir, Dirs, "dir") ?? "asc";

            return new AllTasksQuery
            {
                Search = search,
                Statuses = ResolveStatuses(group, status),
                Priority = priority,
                CategoryId = request.CategoryId,
                AssignedTo = request.AssignedTo,
                Overdue = request.Overdue ?? false,
                Sort = sort,
                Descending = dir == "desc",
                Page = page,
                Limit = limit,
                Now = now
            };
        }

        /// <summary>The statuses of a tab (group), optionally narrowed to one exact status (AND).</summary>
        public static List<string> ResolveStatuses(string group, string? status)
        {
            string[] fromGroup;
            switch (group)
            {
                case "open": fromGroup = AllTasksCounts.OpenStatuses; break;
                case "completed": fromGroup = AllTasksCounts.CompletedStatuses; break;
                case "closed": fromGroup = AllTasksCounts.ClosedStatuses; break;
                default: fromGroup = AllStatuses; break;
            }

            var result = new List<string>();
            foreach (string s in fromGroup)
            {
                if (status == null || s == status) result.Add(s);
            }
            return result;
        }

        /// <summary>
        /// Escapes the LIKE wildcards in user text so it is matched literally.
        /// Use with the escape character "\" and wrap the result in % ... %.
        /// </summary>
        public static string EscapeLike(string text)
        {
            return text
                .Replace("\\", "\\\\")
                .Replace("%", "\\%")
                .Replace("_", "\\_")
                .Replace("[", "\\[");
        }

        private static string? Choice(string? value, string[] allowed, string name)
        {
            if (string.IsNullOrWhiteSpace(value)) return null;
            string trimmed = value.Trim();
            foreach (string option in allowed)
            {
                if (string.Equals(option, trimmed, StringComparison.Ordinal)) return option;
            }
            throw new ArgumentException("Unknown " + name + " value.");
        }
    }
}

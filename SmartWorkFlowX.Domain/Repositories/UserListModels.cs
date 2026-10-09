using SmartWorkFlowX.Domain.Entities;

namespace SmartWorkFlowX.Domain.Repositories
{
    /// <summary>
    /// A validated, normalised query for the Admin "Manage Users" list.
    /// Built by the Application layer (UserListQueryParser); executed by the repository.
    /// </summary>
    public class UserListQuery
    {
        public const string StatusAll = "all";
        public const string StatusActive = "active";
        public const string StatusDeactivated = "deactivated";

        public const string SortName = "name";
        public const string SortRole = "role";
        public const string SortStatus = "status";
        public const string SortOpen = "open";
        public const string SortAdded = "added";

        /// <summary>Trimmed search text (name or e-mail), or null for no search.</summary>
        public string? Search { get; set; }

        /// <summary>all | active | deactivated</summary>
        public string Status { get; set; } = StatusAll;

        public int? RoleId { get; set; }

        /// <summary>name | role | status | open | added</summary>
        public string Sort { get; set; } = SortName;
        public bool Descending { get; set; }

        public int Page { get; set; } = 1;
        public int Limit { get; set; } = 10;
    }

    /// <summary>Status chip counts: rows matching search + role, ignoring the status filter.</summary>
    public class UserListCounts
    {
        public int All { get; set; }
        public int Active { get; set; }
        public int Deactivated { get; set; }
    }

    /// <summary>What the repository returns for one page of users.</summary>
    public class UserListPage
    {
        /// <summary>The page's users (Role loaded).</summary>
        public List<User> Users { get; set; } = new List<User>();

        /// <summary>Rows matching search + role + status.</summary>
        public int Total { get; set; }

        public UserListCounts Counts { get; set; } = new UserListCounts();
    }
}

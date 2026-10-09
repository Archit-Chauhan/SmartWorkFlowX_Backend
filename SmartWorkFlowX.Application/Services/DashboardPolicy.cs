namespace SmartWorkFlowX.Application.Services
{
    /// <summary>Permission names returned to the client. The UI renders only what is listed.</summary>
    public static class DashboardPermissions
    {
        public const string Workload = "workload";
        public const string AssigneeFilter = "assignee-filter";
        public const string Activity = "activity";
        public const string OrgTotals = "org-totals";
        public const string ExportTasks = "export-tasks";
    }

    /// <summary>
    /// Who may see what on the dashboard (docs/DASHBOARD_SPEC.md in the frontend repo, "Permissions").
    /// Admin: everything. Manager: everything except audit activity. Auditor: read-only, no user/workflow totals.
    /// Employee: own tasks only. Any other role gets the least-privilege set.
    /// </summary>
    public static class DashboardPolicy
    {
        public static List<string> PermissionsForRole(string? role)
        {
            switch (role)
            {
                case "Admin":
                    return new List<string>
                    {
                        DashboardPermissions.Workload,
                        DashboardPermissions.AssigneeFilter,
                        DashboardPermissions.Activity,
                        DashboardPermissions.OrgTotals,
                        DashboardPermissions.ExportTasks
                    };
                case "Manager":
                    return new List<string>
                    {
                        DashboardPermissions.Workload,
                        DashboardPermissions.AssigneeFilter,
                        DashboardPermissions.OrgTotals,
                        DashboardPermissions.ExportTasks
                    };
                case "Auditor":
                    return new List<string>
                    {
                        DashboardPermissions.Workload,
                        DashboardPermissions.AssigneeFilter,
                        DashboardPermissions.Activity,
                        DashboardPermissions.ExportTasks
                    };
                default:
                    return new List<string> { DashboardPermissions.ExportTasks };
            }
        }

        /// <summary>True when the role sees every task; false when the numbers are limited to the user's own tasks.</summary>
        public static bool SeesAllTasks(string? role)
        {
            return role == "Admin" || role == "Manager" || role == "Auditor";
        }
    }
}

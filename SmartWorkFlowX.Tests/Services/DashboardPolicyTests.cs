using SmartWorkFlowX.Application.Services;

namespace SmartWorkFlowX.Tests.Services
{
    public class DashboardPolicyTests
    {
        private static List<string> Sorted(string? role)
        {
            return DashboardPolicy.PermissionsForRole(role).OrderBy(p => p, StringComparer.Ordinal).ToList();
        }

        [Fact(DisplayName = "TC-D49: Permission names are the strings the client expects")]
        public void Permissions_HaveExpectedNames()
        {
            Assert.Equal("workload", DashboardPermissions.Workload);
            Assert.Equal("assignee-filter", DashboardPermissions.AssigneeFilter);
            Assert.Equal("activity", DashboardPermissions.Activity);
            Assert.Equal("org-totals", DashboardPermissions.OrgTotals);
            Assert.Equal("export-tasks", DashboardPermissions.ExportTasks);
        }

        [Fact(DisplayName = "TC-D50: Admin has every dashboard permission")]
        public void PermissionsForRole_Admin()
        {
            Assert.Equal(
                new List<string> { "activity", "assignee-filter", "export-tasks", "org-totals", "workload" },
                Sorted("Admin"));
        }

        [Fact(DisplayName = "TC-D51: Manager has everything except audit activity")]
        public void PermissionsForRole_Manager()
        {
            Assert.Equal(
                new List<string> { "assignee-filter", "export-tasks", "org-totals", "workload" },
                Sorted("Manager"));
        }

        [Fact(DisplayName = "TC-D52: Auditor has activity and workload but no org totals")]
        public void PermissionsForRole_Auditor()
        {
            Assert.Equal(
                new List<string> { "activity", "assignee-filter", "export-tasks", "workload" },
                Sorted("Auditor"));
        }

        [Fact(DisplayName = "TC-D53: Employee can only export their own tasks")]
        public void PermissionsForRole_Employee()
        {
            Assert.Equal(new List<string> { "export-tasks" }, DashboardPolicy.PermissionsForRole("Employee"));
        }

        [Fact(DisplayName = "TC-D54: Unknown, empty or null roles fall back to least privilege")]
        public void PermissionsForRole_UnknownRole_LeastPrivilege()
        {
            Assert.Equal(new List<string> { "export-tasks" }, DashboardPolicy.PermissionsForRole("Contractor"));
            Assert.Equal(new List<string> { "export-tasks" }, DashboardPolicy.PermissionsForRole(null));
            Assert.Equal(new List<string> { "export-tasks" }, DashboardPolicy.PermissionsForRole(string.Empty));
        }

        [Fact(DisplayName = "TC-D55: Role names are case-sensitive, so a lower-case admin gets least privilege")]
        public void PermissionsForRole_IsCaseSensitive()
        {
            Assert.Equal(new List<string> { "export-tasks" }, DashboardPolicy.PermissionsForRole("admin"));
        }

        [Fact(DisplayName = "TC-D56: Each call returns a fresh list so callers cannot corrupt the policy")]
        public void PermissionsForRole_ReturnsFreshList()
        {
            List<string> first = DashboardPolicy.PermissionsForRole("Admin");
            first.Clear();

            Assert.Equal(5, DashboardPolicy.PermissionsForRole("Admin").Count);
        }

        [Fact(DisplayName = "TC-D57: Admin, Manager and Auditor see all tasks")]
        public void SeesAllTasks_PrivilegedRoles()
        {
            Assert.True(DashboardPolicy.SeesAllTasks("Admin"));
            Assert.True(DashboardPolicy.SeesAllTasks("Manager"));
            Assert.True(DashboardPolicy.SeesAllTasks("Auditor"));
        }

        [Fact(DisplayName = "TC-D58: Employee and unknown roles are limited to their own tasks")]
        public void SeesAllTasks_OtherRoles()
        {
            Assert.False(DashboardPolicy.SeesAllTasks("Employee"));
            Assert.False(DashboardPolicy.SeesAllTasks("Contractor"));
            Assert.False(DashboardPolicy.SeesAllTasks(null));
            Assert.False(DashboardPolicy.SeesAllTasks("admin"));
        }
    }
}

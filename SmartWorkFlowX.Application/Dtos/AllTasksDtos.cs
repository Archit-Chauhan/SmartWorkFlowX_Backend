using SmartWorkFlowX.Domain.Repositories;

namespace SmartWorkFlowX.Application.Dtos
{
    /// <summary>Raw (unvalidated) query-string values of GET /api/Task/all when "page" is present.</summary>
    public class AllTasksRequest
    {
        public int? Page { get; set; }
        public int? Limit { get; set; }
        public string? Q { get; set; }
        public string? Group { get; set; }
        public string? Status { get; set; }
        public string? Priority { get; set; }
        public int? CategoryId { get; set; }
        public int? AssignedTo { get; set; }
        public bool? Overdue { get; set; }
        public string? Sort { get; set; }
        public string? Dir { get; set; }
    }

    /// <summary>Paged response of GET /api/Task/all: the usual paging envelope plus the tab counts.</summary>
    public class AllTasksPagedResponse : PaginatedList<AllTasksRow>
    {
        public AllTasksCounts Counts { get; set; } = new AllTasksCounts();
    }
}

namespace SmartWorkFlowX.Domain.Common
{
    /// <summary>
    /// Marks an entity as soft-deletable.
    /// EF Core will intercept Remove() calls and set IsDeleted instead of issuing a DELETE statement.
    /// A Global Query Filter ensures soft-deleted records are automatically excluded from all queries.
    /// </summary>
    public interface ISoftDeletable
    {
        bool IsDeleted { get; set; }
        DateTime? DeletedAt { get; set; }
    }
}

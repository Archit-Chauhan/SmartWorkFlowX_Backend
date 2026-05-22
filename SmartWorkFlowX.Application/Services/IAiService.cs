namespace SmartWorkFlowX.Application.Services
{
    public interface IAiService
    {
        Task<string> FormalizeDescriptionAsync(string rawText, string context = "task");
    }
}

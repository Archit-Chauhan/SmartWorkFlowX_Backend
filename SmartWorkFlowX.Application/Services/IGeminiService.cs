namespace SmartWorkFlowX.Application.Services
{
    public interface IGeminiService
    {
        Task<string> FormalizeDescriptionAsync(string rawText);
    }
}

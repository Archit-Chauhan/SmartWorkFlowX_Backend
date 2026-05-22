using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using SmartWorkFlowX.Application.Services;

namespace SmartWorkFlowX.Infrastructure.Services
{
    public class GroqService : IAiService
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly string _apiKey;

        private const string Endpoint = "https://api.groq.com/openai/v1/chat/completions";
        private const string Model = "llama-3.1-8b-instant";

        private const string SystemPrompt =
            "You are a professional project manager. Rewrite the user's rough task description into a clear, concise, and professional task description. " +
            "Keep it factual and action-oriented. Use plain text only — no markdown, no bullet points, no headers. Return only the rewritten description, nothing else.";

        public GroqService(IHttpClientFactory httpClientFactory, IConfiguration configuration)
        {
            _httpClientFactory = httpClientFactory;
            _apiKey = configuration["Groq:ApiKey"]
                ?? throw new InvalidOperationException("Groq:ApiKey is not configured.");
        }

        public async Task<string> FormalizeDescriptionAsync(string rawText)
        {
            var client = _httpClientFactory.CreateClient();
            client.DefaultRequestHeaders.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _apiKey);

            var body = new
            {
                model = Model,
                messages = new[]
                {
                    new { role = "system", content = SystemPrompt },
                    new { role = "user", content = rawText }
                }
            };

            var response = await client.PostAsJsonAsync(Endpoint, body);
            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(json);

            return doc.RootElement
                .GetProperty("choices")[0]
                .GetProperty("message")
                .GetProperty("content")
                .GetString() ?? rawText;
        }
    }
}

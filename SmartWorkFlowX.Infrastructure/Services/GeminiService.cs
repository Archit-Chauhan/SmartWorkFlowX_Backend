using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using SmartWorkFlowX.Application.Services;

namespace SmartWorkFlowX.Infrastructure.Services
{
    public class GeminiService : IGeminiService
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly string _apiKey;

        private const string Endpoint =
            "https://generativelanguage.googleapis.com/v1beta/models/gemini-1.5-flash:generateContent";

        private const string SystemPrompt =
            "You are a professional project manager. Rewrite the following rough task description into a clear, concise, and professional task description. " +
            "Keep it factual and action-oriented. Use plain text only — no markdown, no bullet points, no headers. Return only the rewritten description, nothing else.";

        public GeminiService(IHttpClientFactory httpClientFactory, IConfiguration configuration)
        {
            _httpClientFactory = httpClientFactory;
            _apiKey = configuration["Gemini:ApiKey"]
                ?? throw new InvalidOperationException("Gemini:ApiKey is not configured.");
        }

        public async Task<string> FormalizeDescriptionAsync(string rawText)
        {
            var client = _httpClientFactory.CreateClient();

            var body = new
            {
                contents = new[]
                {
                    new
                    {
                        parts = new[]
                        {
                            new { text = $"{SystemPrompt}\n\nRaw description: {rawText}" }
                        }
                    }
                }
            };

            var response = await client.PostAsJsonAsync($"{Endpoint}?key={_apiKey}", body);
            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(json);

            return doc.RootElement
                .GetProperty("candidates")[0]
                .GetProperty("content")
                .GetProperty("parts")[0]
                .GetProperty("text")
                .GetString() ?? rawText;
        }
    }
}

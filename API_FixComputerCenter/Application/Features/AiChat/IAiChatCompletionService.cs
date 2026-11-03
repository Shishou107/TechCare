using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace API_FixComputerCenter.Application.Features.AiChat;

public record AiChatMessage(string Role, string Content);

public interface IAiChatCompletionService
{
    bool HasApiKey { get; }
    string ModelName { get; }
    Task<string?> CompleteChatAsync(IEnumerable<AiChatMessage> messages, CancellationToken ct = default);
}

public class OpenAICompatibleChatCompletionService : IAiChatCompletionService
{
    private readonly HttpClient _http;
    private readonly IConfiguration _config;
    private readonly ILogger<OpenAICompatibleChatCompletionService> _logger;
    private readonly string? _apiKey;
    private readonly string _endpoint;
    private readonly string _model;

    public bool HasApiKey => !string.IsNullOrWhiteSpace(_apiKey);
    public string ModelName => _model;

    public OpenAICompatibleChatCompletionService(
        HttpClient http,
        IConfiguration config,
        ILogger<OpenAICompatibleChatCompletionService> logger)
    {
        _http = http;
        _config = config;
        _logger = logger;

        _apiKey = Environment.GetEnvironmentVariable("TECHCARE_AI_API_KEY")
                  ?? Environment.GetEnvironmentVariable("OPENAI_API_KEY")
                  ?? _config["Ai:ApiKey"];

        var baseUrl = Environment.GetEnvironmentVariable("TECHCARE_AI_BASE_URL")
                      ?? _config["Ai:BaseUrl"]
                      ?? "https://api.openai.com/v1";

        _endpoint = baseUrl.TrimEnd('/') + "/chat/completions";
        _model = Environment.GetEnvironmentVariable("TECHCARE_AI_MODEL")
                 ?? _config["Ai:Model"]
                 ?? "gpt-4o-mini";
    }

    public async Task<string?> CompleteChatAsync(IEnumerable<AiChatMessage> messages, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(_apiKey))
        {
            return null;
        }

        try
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(TimeSpan.FromSeconds(30));

            using var req = new HttpRequestMessage(HttpMethod.Post, _endpoint);
            req.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _apiKey);

            var msgsList = new List<object>();
            foreach (var m in messages)
            {
                msgsList.Add(new { role = m.Role, content = m.Content });
            }

            var payload = new
            {
                model = _model,
                messages = msgsList,
                temperature = 0.2,
                max_tokens = 500
            };

            req.Content = JsonContent.Create(payload);

            using var res = await _http.SendAsync(req, cts.Token);
            if (!res.IsSuccessStatusCode)
            {
                var errorBody = await res.Content.ReadAsStringAsync(cts.Token);
                _logger.LogWarning("LLM Chat Completion HTTP {Status}: {Body}", res.StatusCode, errorBody);
                return null;
            }

            using var stream = await res.Content.ReadAsStreamAsync(cts.Token);
            using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: cts.Token);
            var choices = doc.RootElement.GetProperty("choices");
            if (choices.GetArrayLength() > 0)
            {
                var choice = choices[0];
                if (choice.TryGetProperty("message", out var msgProp) &&
                    msgProp.TryGetProperty("content", out var contentProp))
                {
                    return contentProp.GetString();
                }
            }

            return null;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Exception calling LLM completion, falling back to extractive RAG mode.");
            return null;
        }
    }
}


using System;
using System.Net.Http;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace API_FixComputerCenter.Application.Features.AiChat;

public interface IEmbeddingService
{
    Task<float[]> GenerateEmbeddingAsync(string text, CancellationToken ct = default);
    byte[] VectorToBytes(float[] vector);
    float[] BytesToVector(byte[] bytes);
    double CosineSimilarity(float[] vectorA, float[] vectorB);
}

public class EmbeddingService : IEmbeddingService
{
    private readonly HttpClient _http;
    private readonly IConfiguration _config;
    private readonly ILogger<EmbeddingService> _logger;
    private readonly string? _apiKey;
    private readonly string _endpoint;
    private readonly string _model;

    public EmbeddingService(HttpClient http, IConfiguration config, ILogger<EmbeddingService> logger)
    {
        _http = http;
        _config = config;
        _logger = logger;

        _apiKey = Environment.GetEnvironmentVariable("TECHCARE_AI_API_KEY") 
                  ?? _config["Ai:ApiKey"];
        _endpoint = Environment.GetEnvironmentVariable("TECHCARE_AI_EMBEDDING_URL") 
                    ?? _config["Ai:EmbeddingUrl"] 
                    ?? "https://api.openai.com/v1/embeddings";
        _model = _config["Ai:EmbeddingModel"] ?? "text-embedding-3-small";
    }

    public async Task<float[]> GenerateEmbeddingAsync(string text, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return new float[128];
        }

        // If API key is available, call OpenAI-compatible embedding API
        if (!string.IsNullOrWhiteSpace(_apiKey))
        {
            try
            {
                using var req = new HttpRequestMessage(HttpMethod.Post, _endpoint);
                req.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _apiKey);
                var payload = new
                {
                    model = _model,
                    input = text
                };
                req.Content = JsonContent.Create(payload);

                using var res = await _http.SendAsync(req, ct);
                if (res.IsSuccessStatusCode)
                {
                    using var stream = await res.Content.ReadAsStreamAsync(ct);
                    using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: ct);
                    var dataArr = doc.RootElement.GetProperty("data");
                    if (dataArr.GetArrayLength() > 0)
                    {
                        var embeddingArr = dataArr[0].GetProperty("embedding");
                        var result = new float[embeddingArr.GetArrayLength()];
                        int idx = 0;
                        foreach (var val in embeddingArr.EnumerateArray())
                        {
                            result[idx++] = val.GetSingle();
                        }
                        return result;
                    }
                }
                else
                {
                    _logger.LogWarning("Embedding API call returned status {Status}. Falling back to deterministic local embedding.", res.StatusCode);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to call external embedding API. Falling back to deterministic local embedding.");
            }
        }

        // Fallback: 128-dimensional local semantic frequency hash embedding (no API key required)
        return GenerateLocalSemanticVector(text);
    }

    private static float[] GenerateLocalSemanticVector(string text)
    {
        const int dims = 128;
        var vector = new float[dims];
        var tokens = text.ToLowerInvariant().Split(new[] { ' ', '\r', '\n', '\t', ',', '.', ';', ':', '!', '?', '-', '(', ')' }, StringSplitOptions.RemoveEmptyEntries);
        if (tokens.Length == 0) return vector;

        foreach (var t in tokens)
        {
            var hash = (uint)t.GetHashCode();
            int idx = (int)(hash % dims);
            vector[idx] += 1.0f;
            int idx2 = (int)((hash / dims) % dims);
            vector[idx2] += 0.5f;
        }

        // Normalize vector to unit length
        double sumSq = 0;
        for (int i = 0; i < dims; i++) sumSq += vector[i] * vector[i];
        if (sumSq > 0)
        {
            float mag = (float)Math.Sqrt(sumSq);
            for (int i = 0; i < dims; i++) vector[i] /= mag;
        }

        return vector;
    }

    public byte[] VectorToBytes(float[] vector)
    {
        var bytes = new byte[vector.Length * 4];
        Buffer.BlockCopy(vector, 0, bytes, 0, bytes.Length);
        return bytes;
    }

    public float[] BytesToVector(byte[] bytes)
    {
        if (bytes == null || bytes.Length == 0) return Array.Empty<float>();
        var vector = new float[bytes.Length / 4];
        Buffer.BlockCopy(bytes, 0, vector, 0, bytes.Length);
        return vector;
    }

    public double CosineSimilarity(float[] vectorA, float[] vectorB)
    {
        if (vectorA == null || vectorB == null || vectorA.Length == 0 || vectorB.Length == 0)
            return 0.0;

        int len = Math.Min(vectorA.Length, vectorB.Length);
        double dotProduct = 0.0;
        double normA = 0.0;
        double normB = 0.0;

        for (int i = 0; i < len; i++)
        {
            dotProduct += vectorA[i] * vectorB[i];
            normA += vectorA[i] * vectorA[i];
            normB += vectorB[i] * vectorB[i];
        }

        if (normA <= 0.0 || normB <= 0.0) return 0.0;
        return dotProduct / (Math.Sqrt(normA) * Math.Sqrt(normB));
    }
}


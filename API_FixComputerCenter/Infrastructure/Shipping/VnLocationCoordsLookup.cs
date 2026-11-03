using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace API_FixComputerCenter.Infrastructure.Shipping;

public class LocationCoordItem
{
    public decimal Lat { get; set; }
    public decimal Lng { get; set; }
}

public static class VnLocationCoordsLookup
{
    private static readonly Dictionary<string, (decimal Lat, decimal Lng)> _coords = new();
    private static readonly List<string> _sortedKeys = new();

    static VnLocationCoordsLookup()
    {
        LoadCoords();
    }

    private static void LoadCoords()
    {
        try
        {
            var baseDir = AppContext.BaseDirectory;
            var pathsToTry = new[]
            {
                Path.Combine(baseDir, "Infrastructure", "Shipping", "vn-location-coords.json"),
                Path.Combine(baseDir, "vn-location-coords.json"),
                Path.Combine(Directory.GetCurrentDirectory(), "Infrastructure", "Shipping", "vn-location-coords.json"),
                Path.Combine(Directory.GetCurrentDirectory(), "TechCare", "API_FixComputerCenter", "Infrastructure", "Shipping", "vn-location-coords.json"),
            };

            string? foundPath = pathsToTry.FirstOrDefault(File.Exists);
            if (foundPath != null)
            {
                var json = File.ReadAllText(foundPath);
                using var doc = JsonDocument.Parse(json);
                foreach (var prop in doc.RootElement.EnumerateObject())
                {
                    var lat = prop.Value.GetProperty("lat").GetDecimal();
                    var lng = prop.Value.GetProperty("lng").GetDecimal();
                    var key = NormalizeText(prop.Name);
                    _coords[key] = (lat, lng);
                }

                _sortedKeys.AddRange(_coords.Keys.OrderByDescending(k => k.Length));
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[VnLocationCoordsLookup] Error loading coordinates: {ex.Message}");
        }
    }

    public static string NormalizeText(string? input)
    {
        if (string.IsNullOrWhiteSpace(input)) return string.Empty;

        // Replace 'đ' and 'Đ'
        var normalized = input.Replace("đ", "d").Replace("Đ", "d");

        // Form D decomposition and remove non-spacing marks
        var formD = normalized.Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder();
        foreach (var ch in formD)
        {
            var uc = CharUnicodeInfo.GetUnicodeCategory(ch);
            if (uc != UnicodeCategory.NonSpacingMark)
            {
                sb.Append(ch);
            }
        }

        var result = sb.ToString().Normalize(NormalizationForm.FormC).ToLowerInvariant();
        result = Regex.Replace(result, @"[^\w\s-]", " ");
        return Regex.Replace(result, @"\s+", " ").Trim();
    }

    public static (decimal? Lat, decimal? Lng) Lookup(string? locationText)
    {
        if (string.IsNullOrWhiteSpace(locationText)) return (null, null);

        var norm = NormalizeText(locationText);
        if (string.IsNullOrEmpty(norm)) return (null, null);

        // 1. Direct match
        if (_coords.TryGetValue(norm, out var direct))
        {
            return (direct.Lat, direct.Lng);
        }

        // 2. Substring match (longest key first)
        foreach (var key in _sortedKeys)
        {
            // Match as whole word or phrase
            if (norm == key || norm.Contains(key))
            {
                var found = _coords[key];
                return (found.Lat, found.Lng);
            }
        }

        return (null, null);
    }

    public static async Task<(decimal? Lat, decimal? Lng)> GeocodeAddressAsync(HttpClient http, string address, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(address)) return (null, null);

        try
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(TimeSpan.FromSeconds(5));

            var url = $"https://nominatim.openstreetmap.org/search?format=json&limit=1&countrycodes=vn&q={Uri.EscapeDataString(address)}";
            using var req = new HttpRequestMessage(HttpMethod.Get, url);
            req.Headers.UserAgent.ParseAdd("TechCare-App/1.0 (contact@techcare.vn)");

            using var res = await http.SendAsync(req, cts.Token);
            if (res.IsSuccessStatusCode)
            {
                using var stream = await res.Content.ReadAsStreamAsync(cts.Token);
                using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: cts.Token);
                if (doc.RootElement.ValueKind == JsonValueKind.Array && doc.RootElement.GetArrayLength() > 0)
                {
                    var item = doc.RootElement[0];
                    if (item.TryGetProperty("lat", out var latProp) && item.TryGetProperty("lon", out var lonProp))
                    {
                        if (decimal.TryParse(latProp.GetString(), NumberStyles.Any, CultureInfo.InvariantCulture, out var lat) &&
                            decimal.TryParse(lonProp.GetString(), NumberStyles.Any, CultureInfo.InvariantCulture, out var lng))
                        {
                            return (lat, lng);
                        }
                    }
                }
            }
        }
        catch
        {
            // Ignore error or timeout, proceed to fallback
        }

        // Fallback: lookup in static database
        return Lookup(address);
    }
}


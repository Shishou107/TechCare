using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace API_FixComputerCenter.Application.Features.Shipping;

public class GhnOptions
{
    public string BaseUrl { get; set; } = "https://dev-online-gateway.ghn.vn/shiip/public-api";
    public string? Token { get; set; }
    public string? ShopId { get; set; }
    public string? WebhookSecret { get; set; }
    public int FromDistrictId { get; set; } = 1542; // Cầu Giấy, Hà Nội
    public string FromWardCode { get; set; } = "1B1507"; // Dịch Vọng Hậu
    public int ServiceTypeId { get; set; } = 2; // Chuẩn Express
    public int DefaultWeightGramLaptop { get; set; } = 2500;
}

public class GhnShippingProvider : IShippingProvider
{
    private readonly HttpClient _http;
    private readonly GhnOptions _opts;
    private readonly IMemoryCache _cache;
    private readonly ILogger<GhnShippingProvider> _logger;

    public string CarrierCode => "GHN";

    public GhnShippingProvider(
        HttpClient http,
        IOptions<GhnOptions> opts,
        IMemoryCache cache,
        ILogger<GhnShippingProvider> logger)
    {
        _http = http;
        _opts = opts.Value;
        _cache = cache;
        _logger = logger;

        // Resolve runtime credentials from environment variables if not in config
        var envToken = Environment.GetEnvironmentVariable("TECHCARE_GHN_TOKEN");
        if (!string.IsNullOrWhiteSpace(envToken)) _opts.Token = envToken;

        var envShopId = Environment.GetEnvironmentVariable("TECHCARE_GHN_SHOP_ID");
        if (!string.IsNullOrWhiteSpace(envShopId)) _opts.ShopId = envShopId;

        var envWebhookSecret = Environment.GetEnvironmentVariable("TECHCARE_GHN_WEBHOOK_SECRET");
        if (!string.IsNullOrWhiteSpace(envWebhookSecret)) _opts.WebhookSecret = envWebhookSecret;
    }

    private bool IsConfigured =>
        !string.IsNullOrWhiteSpace(_opts.Token) &&
        !string.IsNullOrWhiteSpace(_opts.ShopId);

    public async Task<ShippingFeePreviewResult> PreviewFeeAsync(ShippingFeePreviewRequest request, CancellationToken ct = default)
    {
        int fromDistrict = request.FromDistrictId ?? _opts.FromDistrictId;
        int weight = request.WeightGram > 0 ? request.WeightGram : _opts.DefaultWeightGramLaptop;

        if (!IsConfigured)
        {
            _logger.LogWarning("GHN credentials not configured. Running PreviewFee in Mock Mode.");
            return new ShippingFeePreviewResult
            {
                CarrierCode = CarrierCode,
                FeeAmount = 35000,
                ExpectedDeliveryTime = DateTime.UtcNow.AddDays(2),
                Note = "Phí tham khảo (Chế độ mô phỏng Staging GHN)"
            };
        }

        try
        {
            var reqMsg = new HttpRequestMessage(HttpMethod.Post, $"{_opts.BaseUrl}/v2/shipping-order/fee");
            reqMsg.Headers.Add("Token", _opts.Token);
            reqMsg.Headers.Add("ShopId", _opts.ShopId);

            var payload = new
            {
                from_district_id = fromDistrict,
                to_district_id = request.ToDistrictId,
                to_ward_code = request.ToWardCode,
                weight = weight,
                service_type_id = _opts.ServiceTypeId,
                insurance_value = (int)Math.Min(request.InsuredValue, 5_000_000m)
            };

            reqMsg.Content = JsonContent.Create(payload);
            var res = await _http.SendAsync(reqMsg, ct);
            if (res.IsSuccessStatusCode)
            {
                var doc = await res.Content.ReadFromJsonAsync<JsonDocument>(cancellationToken: ct);
                var data = doc?.RootElement.GetProperty("data");
                decimal total = data?.GetProperty("total").GetDecimal() ?? 35000;
                return new ShippingFeePreviewResult
                {
                    CarrierCode = CarrierCode,
                    FeeAmount = total,
                    ExpectedDeliveryTime = DateTime.UtcNow.AddDays(2),
                    Note = "Phí cước tính thực tế từ GHN Express API"
                };
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to call GHN Fee Preview. Falling back to default estimate.");
        }

        return new ShippingFeePreviewResult
        {
            CarrierCode = CarrierCode,
            FeeAmount = 35000,
            ExpectedDeliveryTime = DateTime.UtcNow.AddDays(3),
            Note = "Phí cước dự tính 35.000đ (GHN Express)"
        };
    }

    public async Task<CreateShipmentResult> CreateOrderAsync(CreateShipmentRequest request, CancellationToken ct = default)
    {
        if (!IsConfigured)
        {
            _logger.LogWarning("GHN credentials not configured. Creating order in Mock Mode for {ClientCode}", request.ClientOrderCode);
            string mockWaybill = $"GHN{DateTime.UtcNow:yyMMdd}{Random.Shared.Next(100000, 999999)}";
            return new CreateShipmentResult
            {
                Success = true,
                CarrierCode = CarrierCode,
                WaybillCode = mockWaybill,
                InitialStatus = "CREATED",
                FeeAmount = 35000,
                ExpectedDeliveryAtUtc = DateTime.UtcNow.AddDays(2),
                RawResponse = $"{{\"code\":200,\"message\":\"MOCK_SUCCESS\",\"data\":{{\"order_code\":\"{mockWaybill}\"}}}}"
            };
        }

        try
        {
            var reqMsg = new HttpRequestMessage(HttpMethod.Post, $"{_opts.BaseUrl}/v2/shipping-order/create");
            reqMsg.Headers.Add("Token", _opts.Token);
            reqMsg.Headers.Add("ShopId", _opts.ShopId);

            int toDistrict = int.TryParse(request.ReceiverDistrictCode, out var td) ? td : _opts.FromDistrictId;
            string toWard = request.ReceiverWardCode ?? _opts.FromWardCode;

            var payload = new
            {
                payment_type_id = request.FeePayer == "RECEIVER" ? 2 : 1,
                note = request.Note ?? "Hàng laptop linh kiện, đóng bọc chống sốc kỹ.",
                required_note = "KHONGCHOXEMHANG",
                client_order_code = request.ClientOrderCode,
                to_name = request.ReceiverName,
                to_phone = request.ReceiverPhone,
                to_address = request.ReceiverAddress,
                to_ward_code = toWard,
                to_district_id = toDistrict,
                cod_amount = (int)request.CodAmount,
                content = "Thiet bi may tinh / laptop TechCare",
                weight = request.WeightGram > 0 ? request.WeightGram : _opts.DefaultWeightGramLaptop,
                insurance_value = (int)Math.Min(request.InsuredValue, 5_000_000m),
                service_type_id = _opts.ServiceTypeId,
                items = new[]
                {
                    new
                    {
                        name = "Laptop / Linh kien sua chua TechCare",
                        quantity = 1,
                        weight = request.WeightGram > 0 ? request.WeightGram : _opts.DefaultWeightGramLaptop
                    }
                }
            };

            reqMsg.Content = JsonContent.Create(payload);
            var res = await _http.SendAsync(reqMsg, ct);
            var resBody = await res.Content.ReadAsStringAsync(ct);

            if (res.IsSuccessStatusCode)
            {
                using var doc = JsonDocument.Parse(resBody);
                var data = doc.RootElement.GetProperty("data");
                string orderCode = data.GetProperty("order_code").GetString() ?? string.Empty;
                decimal fee = data.TryGetProperty("total_fee", out var tf) ? tf.GetDecimal() : 35000;

                DateTime? expected = null;
                if (data.TryGetProperty("expected_delivery_time", out var expEl) && expEl.ValueKind == JsonValueKind.String)
                {
                    if (DateTime.TryParse(expEl.GetString(), out var dt)) expected = dt.ToUniversalTime();
                }

                return new CreateShipmentResult
                {
                    Success = true,
                    CarrierCode = CarrierCode,
                    WaybillCode = orderCode,
                    InitialStatus = "CREATED",
                    FeeAmount = fee,
                    ExpectedDeliveryAtUtc = expected ?? DateTime.UtcNow.AddDays(2),
                    RawResponse = resBody
                };
            }

            return new CreateShipmentResult
            {
                Success = false,
                ErrorMessage = $"GHN Create Error ({res.StatusCode}): {resBody}",
                RawResponse = resBody
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception calling GHN Create Order");
            return new CreateShipmentResult
            {
                Success = false,
                ErrorMessage = ex.Message
            };
        }
    }

    public async Task<CancelShipmentResult> CancelOrderAsync(string waybillCode, CancellationToken ct = default)
    {
        if (!IsConfigured)
        {
            return new CancelShipmentResult { Success = true, Message = "Mock order cancelled" };
        }

        try
        {
            var reqMsg = new HttpRequestMessage(HttpMethod.Post, $"{_opts.BaseUrl}/v2/switch-status/cancel");
            reqMsg.Headers.Add("Token", _opts.Token);
            reqMsg.Headers.Add("ShopId", _opts.ShopId);
            reqMsg.Content = JsonContent.Create(new { order_codes = new[] { waybillCode } });

            var res = await _http.SendAsync(reqMsg, ct);
            return new CancelShipmentResult
            {
                Success = res.IsSuccessStatusCode,
                Message = await res.Content.ReadAsStringAsync(ct)
            };
        }
        catch (Exception ex)
        {
            return new CancelShipmentResult { Success = false, Message = ex.Message };
        }
    }

    public Task<string> GetPrintUrlAsync(string waybillCode, CancellationToken ct = default)
    {
        // GHN Print Partner URL
        return Task.FromResult($"https://dev-online-gateway.ghn.vn/shiip/public-api/v2/a5/gen-token?order_code={waybillCode}");
    }

    public string MapProviderStatus(string providerStatus)
    {
        if (string.IsNullOrWhiteSpace(providerStatus)) return "IN_TRANSIT";
        string s = providerStatus.Trim().ToLowerInvariant();

        return s switch
        {
            "ready_to_pick" => "CREATED",
            "picking" => "PICKING",
            "picked" or "storing" or "sorting" => "PICKED",
            "transporting" => "IN_TRANSIT",
            "delivering" => "OUT_FOR_DELIVERY",
            "delivered" => "DELIVERED",
            "delivery_fail" => "DELIVERY_FAILED",
            "waiting_to_return" or "return" or "return_transporting" or "return_sorting" => "RETURNING",
            "returned" => "RETURNED",
            "cancel" => "CANCELLED",
            "damage" or "lost" or "exception" => "EXCEPTION",
            _ => "IN_TRANSIT"
        };
    }

    public async Task<IReadOnlyList<GhnLocationItem>> GetProvincesAsync(CancellationToken ct = default)
    {
        return await _cache.GetOrCreateAsync("ghn_provinces", async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(24);
            if (!IsConfigured) return GetMockProvinces();

            try
            {
                var req = new HttpRequestMessage(HttpMethod.Get, $"{_opts.BaseUrl}/master-data/province");
                req.Headers.Add("Token", _opts.Token);
                var res = await _http.SendAsync(req, ct);
                if (res.IsSuccessStatusCode)
                {
                    using var doc = await res.Content.ReadFromJsonAsync<JsonDocument>(cancellationToken: ct);
                    var items = new List<GhnLocationItem>();
                    if (doc != null && doc.RootElement.TryGetProperty("data", out var dataArr))
                    {
                        foreach (var item in dataArr.EnumerateArray())
                        {
                            items.Add(new GhnLocationItem
                            {
                                Id = item.GetProperty("ProvinceID").GetInt32().ToString(),
                                Name = item.GetProperty("ProvinceName").GetString() ?? string.Empty,
                                Code = item.GetProperty("ProvinceID").GetInt32()
                            });
                        }
                    }
                    if (items.Count > 0) return items;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to fetch provinces from GHN. Using fallback.");
            }

            return GetMockProvinces();
        }) ?? GetMockProvinces();
    }

    public async Task<IReadOnlyList<GhnLocationItem>> GetDistrictsAsync(int provinceId, CancellationToken ct = default)
    {
        string cacheKey = $"ghn_districts_{provinceId}";
        return await _cache.GetOrCreateAsync(cacheKey, async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(24);
            if (!IsConfigured) return GetMockDistricts(provinceId);

            try
            {
                var req = new HttpRequestMessage(HttpMethod.Post, $"{_opts.BaseUrl}/master-data/district");
                req.Headers.Add("Token", _opts.Token);
                req.Content = JsonContent.Create(new { province_id = provinceId });
                var res = await _http.SendAsync(req, ct);
                if (res.IsSuccessStatusCode)
                {
                    using var doc = await res.Content.ReadFromJsonAsync<JsonDocument>(cancellationToken: ct);
                    var items = new List<GhnLocationItem>();
                    if (doc != null && doc.RootElement.TryGetProperty("data", out var dataArr))
                    {
                        foreach (var item in dataArr.EnumerateArray())
                        {
                            items.Add(new GhnLocationItem
                            {
                                Id = item.GetProperty("DistrictID").GetInt32().ToString(),
                                Name = item.GetProperty("DistrictName").GetString() ?? string.Empty,
                                Code = item.GetProperty("DistrictID").GetInt32()
                            });
                        }
                    }
                    if (items.Count > 0) return items;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to fetch districts from GHN for province {Pid}. Using fallback.", provinceId);
            }

            return GetMockDistricts(provinceId);
        }) ?? GetMockDistricts(provinceId);
    }

    public async Task<IReadOnlyList<GhnLocationItem>> GetWardsAsync(int districtId, CancellationToken ct = default)
    {
        string cacheKey = $"ghn_wards_{districtId}";
        return await _cache.GetOrCreateAsync(cacheKey, async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(24);
            if (!IsConfigured) return GetMockWards(districtId);

            try
            {
                var req = new HttpRequestMessage(HttpMethod.Post, $"{_opts.BaseUrl}/master-data/ward");
                req.Headers.Add("Token", _opts.Token);
                req.Content = JsonContent.Create(new { district_id = districtId });
                var res = await _http.SendAsync(req, ct);
                if (res.IsSuccessStatusCode)
                {
                    using var doc = await res.Content.ReadFromJsonAsync<JsonDocument>(cancellationToken: ct);
                    var items = new List<GhnLocationItem>();
                    if (doc != null && doc.RootElement.TryGetProperty("data", out var dataArr))
                    {
                        foreach (var item in dataArr.EnumerateArray())
                        {
                            items.Add(new GhnLocationItem
                            {
                                Id = item.GetProperty("WardCode").GetString() ?? string.Empty,
                                Name = item.GetProperty("WardName").GetString() ?? string.Empty
                            });
                        }
                    }
                    if (items.Count > 0) return items;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to fetch wards from GHN for district {Did}. Using fallback.", districtId);
            }

            return GetMockWards(districtId);
        }) ?? GetMockWards(districtId);
    }

    private static IReadOnlyList<GhnLocationItem> GetMockProvinces() => new[]
    {
        new GhnLocationItem { Id = "201", Code = 201, Name = "Hà Nội" },
        new GhnLocationItem { Id = "202", Code = 202, Name = "TP. Hồ Chí Minh" },
        new GhnLocationItem { Id = "203", Code = 203, Name = "Đà Nẵng" },
        new GhnLocationItem { Id = "204", Code = 204, Name = "Hải Phòng" },
        new GhnLocationItem { Id = "205", Code = 205, Name = "Cần Thơ" },
        new GhnLocationItem { Id = "206", Code = 206, Name = "Bình Dương" }
    };

    private static IReadOnlyList<GhnLocationItem> GetMockDistricts(int provinceId) => new[]
    {
        new GhnLocationItem { Id = "1542", Code = 1542, Name = "Quận Cầu Giấy" },
        new GhnLocationItem { Id = "1543", Code = 1543, Name = "Quận Đống Đa" },
        new GhnLocationItem { Id = "1544", Code = 1544, Name = "Quận Ba Đình" },
        new GhnLocationItem { Id = "1442", Code = 1442, Name = "Quận 1" },
        new GhnLocationItem { Id = "1443", Code = 1443, Name = "Quận 3" },
        new GhnLocationItem { Id = "1444", Code = 1444, Name = "Quận Bình Thạnh" }
    };

    private static IReadOnlyList<GhnLocationItem> GetMockWards(int districtId) => new[]
    {
        new GhnLocationItem { Id = "1B1507", Name = "Phường Dịch Vọng Hậu" },
        new GhnLocationItem { Id = "1B1508", Name = "Phường Mai Dịch" },
        new GhnLocationItem { Id = "1B1509", Name = "Phường Quan Hoa" },
        new GhnLocationItem { Id = "20101", Name = "Phường Bến Nghé" },
        new GhnLocationItem { Id = "20102", Name = "Phường Bến Thành" }
    };
}


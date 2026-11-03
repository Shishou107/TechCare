using System.IO;
using System.Linq;
using System.Security.Claims;
using System.Text.Json;
using API_FixComputerCenter.Application.Features.Shipping;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace API_FixComputerCenter.Endpoints;

public static class ShippingEndpoints
{
    public static void MapShippingEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/shipping").WithTags("Shipping");

        // 1. Public Fee Preview
        group.MapPost("/fee-preview", async (ShippingFeePreviewRequest req, IShippingService svc) =>
        {
            var result = await svc.PreviewFeeAsync(req);
            return Results.Ok(result);
        }).RequireRateLimiting("ShippingFeeRateLimit");

        // 2. Locations (Provinces, Districts, Wards)
        group.MapGet("/locations/provinces", async (IShippingProvider provider) =>
        {
            var provinces = await provider.GetProvincesAsync();
            return Results.Ok(provinces);
        });

        group.MapGet("/locations/districts/{provinceId:int}", async (int provinceId, IShippingProvider provider) =>
        {
            var districts = await provider.GetDistrictsAsync(provinceId);
            return Results.Ok(districts);
        });

        group.MapGet("/locations/wards/{districtId:int}", async (int districtId, IShippingProvider provider) =>
        {
            var wards = await provider.GetWardsAsync(districtId);
            return Results.Ok(wards);
        });

        // 3. Create Shipment (Protected - SHIPPING.CREATE)
        group.MapPost("/orders", async (CreateShipmentRequest req, HttpContext ctx, IShippingService svc) =>
        {
            long userId = 1;
            var claimSub = ctx.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (long.TryParse(claimSub, out var uid)) userId = uid;

            var result = await svc.CreateOrderAsync(req, userId);
            if (!result.Success) return Results.BadRequest(result);
            return Results.Ok(result);
        }).RequireAuthorization("Permission:SHIPPING.CREATE");

        // 4. Cancel Shipment (Protected - SHIPPING.MANAGE)
        group.MapPost("/orders/{id:long}/cancel", async (long id, HttpContext ctx, IShippingService svc) =>
        {
            long userId = 1;
            var claimSub = ctx.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (long.TryParse(claimSub, out var uid)) userId = uid;

            var result = await svc.CancelOrderAsync(id, userId);
            if (!result.Success) return Results.BadRequest(result);
            return Results.Ok(result);
        }).RequireAuthorization("Permission:SHIPPING.MANAGE");

        // 5. Print URL (Protected - SHIPPING.VIEW)
        group.MapGet("/orders/{id:long}/print-url", async (long id, IShippingService svc) =>
        {
            var url = await svc.GetPrintUrlAsync(id);
            return Results.Ok(new { printUrl = url });
        }).RequireAuthorization("Permission:SHIPPING.VIEW");

        // 6. Webhook GHN
        group.MapPost("/webhooks/ghn", async (HttpContext ctx, IShippingService svc) =>
        {
            string? secretHeader = ctx.Request.Headers["X-Ghn-Secret"].FirstOrDefault()
                                ?? ctx.Request.Headers["Token"].FirstOrDefault();

            using var reader = new StreamReader(ctx.Request.Body);
            var rawJson = await reader.ReadToEndAsync();

            var payload = new GhnWebhookPayload();
            try
            {
                using var doc = JsonDocument.Parse(rawJson);
                var root = doc.RootElement;
                payload.OrderCode = root.TryGetProperty("OrderCode", out var oc) ? oc.GetString() : root.TryGetProperty("order_code", out var oc2) ? oc2.GetString() : null;
                payload.ClientOrderCode = root.TryGetProperty("ClientOrderCode", out var coc) ? coc.GetString() : root.TryGetProperty("client_order_code", out var coc2) ? coc2.GetString() : null;
                payload.Status = root.TryGetProperty("Status", out var st) ? st.GetString() : root.TryGetProperty("status", out var st2) ? st2.GetString() : null;
                payload.Description = root.TryGetProperty("Description", out var desc) ? desc.GetString() : null;
                payload.Warehouse = root.TryGetProperty("Warehouse", out var wh) ? wh.GetString() : null;
                if (root.TryGetProperty("TotalFee", out var tf)) payload.TotalFee = tf.GetDecimal();
                payload.RawJson = rawJson;
            }
            catch {}

            var ok = await svc.HandleGhnWebhookAsync(payload, secretHeader);
            if (!ok) return Results.Unauthorized();
            return Results.Ok(new { success = true, message = "Webhook processed" });
        });
    }
}


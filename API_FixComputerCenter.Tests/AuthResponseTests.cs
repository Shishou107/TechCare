using System.Collections.Generic;
using System.Text.Json;
using API_FixComputerCenter.Application.Features.Auth;
using Xunit;

namespace API_FixComputerCenter.Tests;

public class AuthResponseTests
{
    private static readonly JsonSerializerOptions WebJsonOptions = new(JsonSerializerDefaults.Web);

    // TEST (R3): RefreshToken is suppressed in JSON when null (browser clients)
    [Fact]
    public void AuthResponse_WhenRefreshTokenIsNull_SuppressesRefreshTokenPropertyInJson()
    {
        var user = new UserProfileDto(1, "admin", "admin@techcare.vn", 1, new List<string> { "ADMIN" }, new List<string> { "SYSTEM.MANAGE" });
        var response = new AuthResponse(
            AccessToken: "header.payload.signature",
            RefreshToken: null,
            TokenType: "Bearer",
            ExpiresInSeconds: 900,
            User: user
        );

        var json = JsonSerializer.Serialize(response, WebJsonOptions);

        // Crucial security assertion (R3): "refreshToken" MUST NOT be present in JSON body
        Assert.DoesNotContain("refreshToken", json, System.StringComparison.OrdinalIgnoreCase);
        Assert.Contains("accessToken", json);
        Assert.Contains("tokenType", json);
        Assert.Contains("expiresInSeconds", json);
    }

    [Fact]
    public void AuthResponse_WhenRefreshTokenIsNotNull_IncludesRefreshTokenPropertyInJson()
    {
        var user = new UserProfileDto(2, "mobile_client", null, 1, new List<string> { "TECHNICIAN" }, new List<string>());
        var response = new AuthResponse(
            AccessToken: "header.payload.signature",
            RefreshToken: "non-browser-refresh-token-secret-123",
            TokenType: "Bearer",
            ExpiresInSeconds: 900,
            User: user
        );

        var json = JsonSerializer.Serialize(response, WebJsonOptions);

        // For non-browser clients (explicit mobile / external client), refreshToken is present
        Assert.Contains("refreshToken", json);
        Assert.Contains("non-browser-refresh-token-secret-123", json);
    }
}

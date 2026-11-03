using API_FixComputerCenter.Application.Features.Shipping;
using Xunit;

namespace API_FixComputerCenter.Tests;

public class ShippingStatusMappingTests
{
    [Theory]
    [InlineData("ready_to_pick", "CREATED")]
    [InlineData("picking", "PICKING")]
    [InlineData("picked", "PICKED")]
    [InlineData("storing", "PICKED")]
    [InlineData("transporting", "IN_TRANSIT")]
    [InlineData("delivering", "OUT_FOR_DELIVERY")]
    [InlineData("delivered", "DELIVERED")]
    [InlineData("delivery_fail", "DELIVERY_FAILED")]
    [InlineData("waiting_to_return", "RETURNING")]
    [InlineData("return", "RETURNING")]
    [InlineData("returned", "RETURNED")]
    [InlineData("cancel", "CANCELLED")]
    [InlineData("damage", "EXCEPTION")]
    [InlineData("lost", "EXCEPTION")]
    public void MapGhnStatus_AllKeyCases_MappedAccurately(string ghnStatus, string expectedDomainStatus)
    {
        var mapped = GhnShippingProvider.MapStatus(ghnStatus);
        Assert.Equal(expectedDomainStatus, mapped);
    }
}

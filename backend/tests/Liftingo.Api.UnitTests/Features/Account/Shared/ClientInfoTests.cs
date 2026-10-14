using Liftingo.Application.Features.Account.Shared;

namespace Liftingo.Api.UnitTests.Features.Account.Shared;

public class ClientInfoTests
{
    private const string ChromeOnWindows =
        "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/130.0.0.0 Safari/537.36";

    [Fact]
    public void Create_ValidDeviceIdHeader_UsesItWithTheAddressAndDeviceLabel()
    {
        var deviceId = Guid.NewGuid();

        ClientInfo client = ClientInfo.Create("203.0.113.5", ChromeOnWindows, deviceId.ToString());

        Assert.Equal(deviceId, client.DeviceId);
        Assert.Equal("203.0.113.5", client.IpAddress);
        Assert.Equal("Chrome, Windows, komputer", client.DeviceType);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("not-a-guid")]
    [InlineData("00000000-0000-0000-0000-000000000000")]
    public void Create_MissingOrMalformedDeviceIdHeader_CountsAsANewDevice(string? header)
    {
        ClientInfo first = ClientInfo.Create(null, ChromeOnWindows, header);
        ClientInfo second = ClientInfo.Create(null, ChromeOnWindows, header);

        Assert.NotEqual(Guid.Empty, first.DeviceId);
        Assert.NotEqual(first.DeviceId, second.DeviceId);
    }

    [Fact]
    public void Create_NoUserAgent_UsesTheUnknownLabels()
    {
        ClientInfo client = ClientInfo.Create(null, null, null);

        Assert.Equal("inna przeglądarka, inny system, komputer", client.DeviceType);
        Assert.Null(client.IpAddress);
    }
}
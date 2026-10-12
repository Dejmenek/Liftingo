using Liftingo.Application.Features.Account.Shared;

namespace Liftingo.Api.UnitTests.Features.Account;

public class DeviceDescriptorTests
{
    [Theory]
    [InlineData(
        "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/130.0.0.0 Safari/537.36",
        "Chrome, Windows, komputer")]
    [InlineData(
        "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/130.0.0.0 Safari/537.36 Edg/130.0.0.0",
        "Edge, Windows, komputer")]
    [InlineData(
        "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/130.0.0.0 Safari/537.36 OPR/115.0.0.0",
        "Opera, Windows, komputer")]
    [InlineData(
        "Mozilla/5.0 (Windows NT 10.0; Win64; x64; rv:132.0) Gecko/20100101 Firefox/132.0",
        "Firefox, Windows, komputer")]
    [InlineData(
        "Mozilla/5.0 (Macintosh; Intel Mac OS X 10_15_7) AppleWebKit/605.1.15 (KHTML, like Gecko) Version/18.0 Safari/605.1.15",
        "Safari, macOS, komputer")]
    [InlineData(
        "Mozilla/5.0 (X11; Linux x86_64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/130.0.0.0 Safari/537.36",
        "Chrome, Linux, komputer")]
    public void Describe_DesktopUserAgent_ReturnsBrowserOsAndComputer(string userAgent, string expected)
    {
        Assert.Equal(expected, DeviceDescriptor.Describe(userAgent));
    }

    [Theory]
    [InlineData(
        "Mozilla/5.0 (iPhone; CPU iPhone OS 18_0 like Mac OS X) AppleWebKit/605.1.15 (KHTML, like Gecko) Version/18.0 Mobile/15E148 Safari/604.1",
        "Safari, iOS, telefon")]
    [InlineData(
        "Mozilla/5.0 (Linux; Android 14; Pixel 8) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/130.0.0.0 Mobile Safari/537.36",
        "Chrome, Android, telefon")]
    [InlineData(
        "Mozilla/5.0 (Android 14; Mobile; rv:132.0) Gecko/132.0 Firefox/132.0",
        "Firefox, Android, telefon")]
    public void Describe_PhoneUserAgent_ReturnsTelefon(string userAgent, string expected)
    {
        Assert.Equal(expected, DeviceDescriptor.Describe(userAgent));
    }

    [Theory]
    [InlineData(
        "Mozilla/5.0 (iPad; CPU OS 18_0 like Mac OS X) AppleWebKit/605.1.15 (KHTML, like Gecko) Version/18.0 Mobile/15E148 Safari/604.1",
        "Safari, iOS, tablet")]
    [InlineData(
        "Mozilla/5.0 (Linux; Android 13; SM-X710) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/130.0.0.0 Safari/537.36",
        "Chrome, Android, tablet")]
    public void Describe_TabletUserAgent_ReturnsTablet(string userAgent, string expected)
    {
        Assert.Equal(expected, DeviceDescriptor.Describe(userAgent));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("curl/8.5.0")]
    public void Describe_UnknownUserAgent_ReturnsFallbackLabels(string? userAgent)
    {
        Assert.Equal("inna przeglądarka, inny system, komputer", DeviceDescriptor.Describe(userAgent));
    }

    [Fact]
    public void Describe_VeryLongUserAgent_StaysWithinStoredLength()
    {
        string result = DeviceDescriptor.Describe(new string('x', 5000) + " Firefox/1 Windows");

        Assert.True(result.Length <= 100);
    }
}

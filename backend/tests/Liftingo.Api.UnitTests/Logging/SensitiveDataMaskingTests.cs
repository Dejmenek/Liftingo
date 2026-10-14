using Liftingo.Api.Extensions;
using Serilog;
using Serilog.Core;
using Serilog.Events;
using System.Globalization;

namespace Liftingo.Api.UnitTests.Logging;

public class SensitiveDataMaskingTests
{
    private const string Mask = "***MASKED***";

    [Fact]
    public void Log_EmailAsProperty_MasksTheProperty()
    {
        LogEvent logEvent = Capture(logger => logger.Information("Sent to {Recipient}", "jan.kowalski@example.com"));

        Assert.DoesNotContain("example.com", PropertyText(logEvent, "Recipient"), StringComparison.Ordinal);
        Assert.Contains(Mask, PropertyText(logEvent, "Recipient"), StringComparison.Ordinal);
    }

    [Fact]
    public void Log_EmailInsideMessage_MasksTheRenderedMessage()
    {
        LogEvent logEvent = Capture(logger => logger.Information("Could not reach jan.kowalski@example.com today"));

        string message = logEvent.RenderMessage(CultureInfo.InvariantCulture);
        Assert.DoesNotContain("example.com", message, StringComparison.Ordinal);
        Assert.Contains(Mask, message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("203.0.113.42")]
    [InlineData("2001:db8:85a3::8a2e:370:7334")]
    [InlineData("2001:0db8:85a3:0000:0000:8a2e:0370:7334")]
    [InlineData("::1")]
    public void Log_IpAddressAsProperty_MasksTheProperty(string address)
    {
        LogEvent logEvent = Capture(logger => logger.Information("Request from {Client}", address));

        string text = PropertyText(logEvent, "Client");
        Assert.DoesNotContain(address, text, StringComparison.Ordinal);
        Assert.Contains(Mask, text, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("203.0.113.42")]
    [InlineData("2001:db8:85a3::8a2e:370:7334")]
    public void Log_IpAddressInsideMessage_MasksTheRenderedMessage(string address)
    {
        LogEvent logEvent = Capture(logger => logger.Information($"Request from {address} was refused"));

        string message = logEvent.RenderMessage(CultureInfo.InvariantCulture);
        Assert.DoesNotContain(address, message, StringComparison.Ordinal);
        Assert.Contains(Mask, message, StringComparison.Ordinal);
    }

    [Fact]
    public void Log_GuidUserIdTimeAndTimestamp_LeavesThemAlone()
    {
        var userId = Guid.NewGuid();

        LogEvent logEvent = Capture(logger => logger.Information(
            "User {UserId} locked until {Time} ({Timestamp})",
            userId.ToString(),
            "10:15:00",
            "2026-10-12T10:15:00.000+00:00"));

        string message = logEvent.RenderMessage(CultureInfo.InvariantCulture);
        Assert.Contains(userId.ToString(), message, StringComparison.Ordinal);
        Assert.Contains("10:15:00", message, StringComparison.Ordinal);
        Assert.Contains("2026-10-12T10:15:00.000+00:00", message, StringComparison.Ordinal);
        Assert.DoesNotContain(Mask, message, StringComparison.Ordinal);
    }

    private static LogEvent Capture(Action<Serilog.ILogger> write)
    {
        var sink = new CollectingSink();

        using Logger logger = new LoggerConfiguration()
            .MaskSensitiveData()
            .WriteTo.Sink(sink)
            .CreateLogger();

        write(logger);

        return Assert.Single(sink.Events);
    }

    private static string PropertyText(LogEvent logEvent, string name) =>
        ((ScalarValue)logEvent.Properties[name]).Value?.ToString() ?? string.Empty;

    private sealed class CollectingSink : ILogEventSink
    {
        public List<LogEvent> Events { get; } = [];

        public void Emit(LogEvent logEvent) => Events.Add(logEvent);
    }
}
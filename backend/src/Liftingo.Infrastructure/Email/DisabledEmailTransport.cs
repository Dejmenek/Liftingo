using Microsoft.Extensions.Logging;

namespace Liftingo.Infrastructure.Email;

/// <summary>
/// Used wherever no real transport exists yet: drops the message. The flow that asked for it still
/// succeeds. Logs the template and the user id, never the recipient (FR-028, FR-030).
/// </summary>
public sealed partial class DisabledEmailTransport(ILogger<DisabledEmailTransport> logger) : IEmailTransport
{
    public Task SendAsync(EmailMessage message, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(message);

        LogDropped(message.TemplateName, message.UserId);

        return Task.CompletedTask;
    }

    [LoggerMessage(
        Level = LogLevel.Warning,
        Message = "E-mail sending is disabled. Dropped e-mail {TemplateName} for user {UserId}.")]
    private partial void LogDropped(string templateName, Guid userId);
}

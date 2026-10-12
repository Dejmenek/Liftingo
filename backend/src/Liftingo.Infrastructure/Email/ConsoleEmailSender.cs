using Liftingo.Application.Common.Email;

using Microsoft.Extensions.Logging;

namespace Liftingo.Infrastructure.Email;

/// <summary>
/// Development only: writes the e-mail to the log so the developer can copy the link. The masking
/// enricher masks the recipient and any address in the body, so find the e-mail by subject and
/// template name.
/// </summary>
public sealed partial class ConsoleEmailSender(ILoggerFactory loggerFactory) : IEmailSender
{
    public const string LoggerCategory = "Liftingo.DevEmail";

    private readonly ILogger _logger = loggerFactory.CreateLogger(LoggerCategory);

    public Task SendAsync(EmailMessage message, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(message);

        LogEmail(message.TemplateName, message.To, message.Subject, message.TextBody);

        return Task.CompletedTask;
    }

    [LoggerMessage(
        Level = LogLevel.Information,
        Message = "E-mail {TemplateName} to {Recipient}\nSubject: {Subject}\n{TextBody}")]
    private partial void LogEmail(string templateName, string recipient, string subject, string textBody);
}

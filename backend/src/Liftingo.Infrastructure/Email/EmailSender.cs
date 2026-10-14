using Liftingo.Application.Common.Email;
using Microsoft.Extensions.Logging;
using System.Diagnostics.CodeAnalysis;

namespace Liftingo.Infrastructure.Email;

/// <summary>
/// Renders an e-mail record and hands it to the transport. A failure is logged with the template name and user
/// id only and never reaches the use case that sent the e-mail.
/// </summary>
public sealed partial class EmailSender(
    AccountEmailRenderer renderer,
    IEmailTransport transport,
    ILogger<EmailSender> logger) : IEmailSender
{
    [SuppressMessage(
        "Design",
        "CA1031:Do not catch general exception types",
        Justification = "A failed e-mail must never fail the flow that triggered it (FR-029); the failure is logged.")]
    public async Task SendAsync(OutgoingEmail email, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(email);

        try
        {
            await transport.SendAsync(renderer.Render(email), ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !ct.IsCancellationRequested)
        {
            // Template name and user id only: the exception is not logged, since a transport may put the recipient in it.
            LogSendFailed(ex.GetType().Name, AccountEmailRenderer.TemplateNameOf(email), email.UserId);
        }
    }

    [LoggerMessage(
        Level = LogLevel.Error,
        Message = "Sending the {TemplateName} e-mail for user {UserId} failed ({ExceptionType}).")]
    private partial void LogSendFailed(string exceptionType, string templateName, Guid userId);
}
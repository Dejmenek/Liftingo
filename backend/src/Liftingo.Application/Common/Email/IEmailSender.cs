namespace Liftingo.Application.Common.Email;

public interface IEmailSender
{
    Task SendAsync(EmailMessage message, CancellationToken ct);
}

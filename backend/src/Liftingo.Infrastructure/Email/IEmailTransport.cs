namespace Liftingo.Infrastructure.Email;

/// <summary>Delivers a rendered e-mail. Swapping the delivery mechanism means swapping this.</summary>
public interface IEmailTransport
{
    Task SendAsync(EmailMessage message, CancellationToken ct);
}
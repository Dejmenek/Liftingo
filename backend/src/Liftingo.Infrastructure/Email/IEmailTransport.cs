namespace Liftingo.Infrastructure.Email;

/// <summary>Delivers a rendered e-mail. Swapping the delivery mechanism means swapping this (FR-027).</summary>
public interface IEmailTransport
{
    Task SendAsync(EmailMessage message, CancellationToken ct);
}

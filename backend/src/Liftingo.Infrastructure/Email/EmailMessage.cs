namespace Liftingo.Infrastructure.Email;

/// <summary>A rendered e-mail, ready for an <see cref="IEmailTransport"/>.</summary>
/// <param name="To">The recipient address. Personal data: transports other than the development one never log it.</param>
/// <param name="TemplateName">Which e-mail this is, so a transport can log it without the recipient.</param>
/// <param name="UserId">The account the e-mail is about, for the same reason.</param>
public sealed record EmailMessage(
    string To,
    string Subject,
    string HtmlBody,
    string TextBody,
    string TemplateName,
    Guid UserId);
namespace Liftingo.Application.Common.Email;

/// <param name="To">The recipient address. Personal data: senders other than the development one never log it.</param>
/// <param name="Subject">The subject line.</param>
/// <param name="HtmlBody">The HTML body.</param>
/// <param name="TextBody">The plain-text body.</param>
/// <param name="TemplateName">Which e-mail this is, so a sender can log it without the recipient.</param>
/// <param name="UserId">The account the e-mail is about, for the same reason.</param>
public sealed record EmailMessage(
    string To,
    string Subject,
    string HtmlBody,
    string TextBody,
    string TemplateName,
    Guid UserId);

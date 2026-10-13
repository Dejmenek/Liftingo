namespace Liftingo.Application.Common.Email;

/// <summary>
/// One e-mail a use case wants sent, as data only: no subject, no body, no link. Infrastructure renders it.
/// Each module declares its own records deriving from this one.
/// </summary>
/// <param name="UserId">The account the e-mail is about. Senders log it instead of the recipient.</param>
/// <param name="To">The recipient address. Personal data: never logged outside the development output.</param>
public abstract record OutgoingEmail(Guid UserId, string To);

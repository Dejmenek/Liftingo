using Liftingo.Application.Common.Email;

namespace Liftingo.Application.Features.Account.Shared.Emails;

/// <summary>The verification link that doubles as the welcome e-mail.</summary>
public sealed record VerificationAndWelcomeEmail(Guid UserId, string To, string Token) : OutgoingEmail(UserId, To);
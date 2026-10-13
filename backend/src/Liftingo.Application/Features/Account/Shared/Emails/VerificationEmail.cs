using Liftingo.Application.Common.Email;

namespace Liftingo.Application.Features.Account.Shared.Emails;

/// <summary>The verification link without the welcome text, for a resend (FR-018).</summary>
public sealed record VerificationEmail(Guid UserId, string To, string Token) : OutgoingEmail(UserId, To);

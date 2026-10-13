using Liftingo.Application.Common.Email;

namespace Liftingo.Application.Features.Account.Shared.Emails;

/// <summary>The password reset link.</summary>
public sealed record PasswordResetEmail(Guid UserId, string To, string Token) : OutgoingEmail(UserId, To);

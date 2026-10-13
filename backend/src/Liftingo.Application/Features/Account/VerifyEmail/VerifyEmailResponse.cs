namespace Liftingo.Application.Features.Account.VerifyEmail;

/// <param name="Status"><see cref="Verified"/> or <see cref="AlreadyVerified"/>.</param>
public sealed record VerifyEmailResponse(string Status)
{
    public const string Verified = "verified";
    public const string AlreadyVerified = "alreadyVerified";
}

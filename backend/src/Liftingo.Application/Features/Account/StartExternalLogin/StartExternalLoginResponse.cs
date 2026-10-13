namespace Liftingo.Application.Features.Account.StartExternalLogin;

/// <param name="Scheme">The authentication scheme to challenge, e.g. <c>Google</c>.</param>
public sealed record StartExternalLoginResponse(string Scheme);

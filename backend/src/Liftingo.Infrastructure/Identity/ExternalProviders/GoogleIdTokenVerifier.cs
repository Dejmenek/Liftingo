using Google.Apis.Auth;
using Liftingo.Application.Common.Security;
using Microsoft.Extensions.Logging;
using System.Diagnostics.CodeAnalysis;

namespace Liftingo.Infrastructure.Identity.ExternalProviders;

/// <summary>
/// Checks a Google ID token: signature against Google's published keys, issuer, expiry and audience.
/// </summary>
internal sealed partial class GoogleIdTokenVerifier(
    GoogleProviderOptions options,
    ILogger<GoogleIdTokenVerifier> logger) : IProviderTokenVerifier
{
    public string LoginProvider => GoogleProviderOptions.LoginProvider;

    [SuppressMessage(
        "Design",
        "CA1031:Do not catch general exception types",
        Justification = "A token that cannot be verified for any reason is not verified; the failure is logged.")]
    public async Task<ExternalIdentity?> VerifyAsync(string token, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        GoogleJsonWebSignature.Payload payload;

        try
        {
            payload = await GoogleJsonWebSignature.ValidateAsync(token, CreateValidationSettings(options.ClientId));
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // The exception is not logged: its message can quote the token.
            LogNotVerified(LoginProvider, ex.GetType().Name);

            return null;
        }

        return ToIdentity(payload);
    }

    // Audience is what ties the token to this application. Without it, an ID token Google issued to any other
    // application would sign its owner in here.
    internal static GoogleJsonWebSignature.ValidationSettings CreateValidationSettings(string clientId) =>
        new() { Audience = [clientId] };

    // The library does not check email_verified. An unverified address counts as no address, so the user is asked
    // to type one, which then gets the usual verification e-mail.
    internal static ExternalIdentity ToIdentity(GoogleJsonWebSignature.Payload payload)
    {
        string? email = payload.EmailVerified ? payload.Email?.Trim() : null;

        return new ExternalIdentity(
            GoogleProviderOptions.LoginProvider,
            payload.Subject,
            string.IsNullOrEmpty(email) ? null : email,
            payload.Name);
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "A {Provider} token could not be verified ({ExceptionType}).")]
    private partial void LogNotVerified(string provider, string exceptionType);
}
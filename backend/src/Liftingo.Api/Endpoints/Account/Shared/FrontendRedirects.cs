using System.Globalization;

using Liftingo.Application.Common.Configuration;

using Microsoft.Extensions.Options;

namespace Liftingo.Api.Endpoints.Account.Shared;

/// <summary>The redirects the external-login callback answers with (the contract's callback table).</summary>
internal sealed class FrontendRedirects(IOptions<AppOptions> appOptions)
{
    public IResult Callback() => To("auth/callback");

    public IResult Consent() => To("auth/consent");

    public IResult External() => To("login?error=external");

    public IResult EmailConflict() => To("login?error=emailConflict");

    public IResult LockedOut(DateTimeOffset retryAfter) =>
        To($"login?error=lockedOut&retryAfter={Uri.EscapeDataString(retryAfter.ToString("O", CultureInfo.InvariantCulture))}");

    private IResult To(string pathAndQuery)
    {
        // FrontendBaseUrl is validated at startup (absolute https URL).
        string baseUrl = appOptions.Value.FrontendBaseUrl!.AbsoluteUri.TrimEnd('/');

        return Results.Redirect($"{baseUrl}/{pathAndQuery}");
    }
}

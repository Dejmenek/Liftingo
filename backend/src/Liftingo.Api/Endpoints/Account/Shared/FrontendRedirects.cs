using Liftingo.Application.Common.Configuration;
using Microsoft.Extensions.Options;
using System.Globalization;

namespace Liftingo.Api.Endpoints.Account.Shared;

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
        string baseUrl = appOptions.Value.FrontendBaseUrl!.AbsoluteUri.TrimEnd('/');

        return Results.Redirect($"{baseUrl}/{pathAndQuery}");
    }
}

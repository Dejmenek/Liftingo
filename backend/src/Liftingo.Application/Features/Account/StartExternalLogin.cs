using System.Globalization;

using Liftingo.Application.Common.Result;
using Liftingo.Application.Common.Routes;
using Liftingo.Application.Common.Security;
using Liftingo.Application.Features.Account.Shared;

using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Liftingo.Application.Features.Account;

// No request body: the provider is in the route and the device id in the query string. This is a browser
// navigation, not an XHR, so the answer is a redirect to the provider.

public sealed class StartExternalLogin : IEndpoint
{
    private static readonly Error s_providerUnavailable = Error.NotFound(
        "Auth.ProviderUnavailable",
        "Logowanie przez tego dostawcę jest niedostępne.");

    public static void Map(RouteGroupBuilder group) =>
        group.MapGet(RouteConsts.Auth.StartExternalLogin, Handle)
            .AllowAnonymous()
            .Produces(StatusCodes.Status302Found)
            .ProducesProblem(StatusCodes.Status404NotFound);

    // The device id is parsed by hand: a malformed value counts as a new device, like a bad X-Device-Id header (R6).
    private static IResult Handle(string provider, string? deviceId, IExternalProviderCatalog catalog)
    {
        string? scheme = catalog.FindScheme(provider);

        if (scheme is null)
        {
            return Result.Failure(s_providerUnavailable).ToProblem();
        }

        var properties = new AuthenticationProperties { RedirectUri = ExternalLoginPaths.Callback };

        if (Guid.TryParse(deviceId, CultureInfo.InvariantCulture, out Guid parsed) && parsed != Guid.Empty)
        {
            properties.Items[PendingExternalLogin.DeviceIdItem] = parsed.ToString();
        }

        return Results.Challenge(properties, [scheme]);
    }
}

using System.Globalization;

using Liftingo.Api.Endpoints.Account.Shared;
using Liftingo.Api.Http;
using Liftingo.Api.Security;
using Liftingo.Application.Common.Messaging;
using Liftingo.Application.Common.Result;
using Liftingo.Application.Features.Account.StartExternalLogin;

using Microsoft.AspNetCore.Authentication;

namespace Liftingo.Api.Endpoints.Account;

// No request body: the provider is in the route and the device id in the query string. This is a browser
// navigation, not an XHR, so the answer is a redirect to the provider.
internal sealed class StartExternalLoginEndpoint : IEndpoint
{
    public static void Map(RouteGroupBuilder group) =>
        group.MapGet(RouteConsts.Auth.StartExternalLogin, HandleAsync)
            .AllowAnonymous()
            .Produces(StatusCodes.Status302Found)
            .ProducesProblem(StatusCodes.Status404NotFound);

    private static async Task<IResult> HandleAsync(
        string provider,
        string? deviceId,
        IQueryHandler<StartExternalLoginQuery, StartExternalLoginResponse> handler,
        CancellationToken ct)
    {
        Result<StartExternalLoginResponse> result = await handler.HandleAsync(new StartExternalLoginQuery(provider), ct);

        if (result.IsFailure)
        {
            return result.ToProblem();
        }

        string scheme = result.Value.Scheme;

        // The items travel through the provider in the protected state and end up in the external cookie. The scheme
        // name is the Identity login provider, so the callback never has to guess it from a claims issuer (the X
        // package, for one, issues its claims as "Twitter").
        var properties = new AuthenticationProperties { RedirectUri = ExternalLoginPaths.Callback };
        properties.Items[ExternalLoginCookie.ProviderItem] = scheme;

        // The device id is parsed by hand: a malformed value counts as a new device, like a bad X-Device-Id header.
        if (Guid.TryParse(deviceId, CultureInfo.InvariantCulture, out Guid parsed) && parsed != Guid.Empty)
        {
            properties.Items[ExternalLoginCookie.DeviceIdItem] = parsed.ToString();
        }

        return Results.Challenge(properties, [scheme]);
    }
}

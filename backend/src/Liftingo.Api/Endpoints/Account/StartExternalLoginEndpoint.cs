using Liftingo.Api.Endpoints.Account.Shared;
using Liftingo.Api.Http;
using Liftingo.Api.Security;
using Liftingo.Application.Common.Messaging;
using Liftingo.Application.Common.Result;
using Liftingo.Application.Features.Account.StartExternalLogin;
using Microsoft.AspNetCore.Authentication;
using System.Globalization;

namespace Liftingo.Api.Endpoints.Account;

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

        var properties = new AuthenticationProperties { RedirectUri = ExternalLoginPaths.Callback };
        properties.Items[ExternalLoginCookie.ProviderItem] = scheme;

        if (Guid.TryParse(deviceId, CultureInfo.InvariantCulture, out Guid parsed) && parsed != Guid.Empty)
        {
            properties.Items[ExternalLoginCookie.DeviceIdItem] = parsed.ToString();
        }

        return Results.Challenge(properties, [scheme]);
    }
}

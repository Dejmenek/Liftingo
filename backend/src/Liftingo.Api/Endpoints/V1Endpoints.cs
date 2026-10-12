using Asp.Versioning;

using Liftingo.Application.Features.Account;

namespace Liftingo.Api.Endpoints;

internal static class V1Endpoints
{
    // The concrete path prefix of this version, for things that need it outside routing (cookie paths).
    public const string PathPrefix = "/api/v1";

    public static readonly ApiVersion Version = new(1, 0);

    public static RouteGroupBuilder MapV1Endpoints(this RouteGroupBuilder v1)
    {
        Register.Map(v1);
        GetCurrentAccount.Map(v1);
        Login.Map(v1);
        RefreshSession.Map(v1);
        Logout.Map(v1);
        LogoutAllDevices.Map(v1);
        VerifyEmail.Map(v1);
        ResendVerificationEmail.Map(v1);

        return v1;
    }
}

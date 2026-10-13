using Asp.Versioning;

using Liftingo.Api.Endpoints.Account;

namespace Liftingo.Api.Endpoints;

internal static class V1Endpoints
{
    // The concrete path prefix of this version, for things that need it outside routing (cookie paths).
    public const string PathPrefix = "/api/v1";

    public static readonly ApiVersion Version = new(1, 0);

    public static RouteGroupBuilder MapV1Endpoints(this RouteGroupBuilder v1)
    {
        RegisterEndpoint.Map(v1);
        GetCurrentAccountEndpoint.Map(v1);
        LoginEndpoint.Map(v1);
        RefreshSessionEndpoint.Map(v1);
        LogoutEndpoint.Map(v1);
        LogoutAllDevicesEndpoint.Map(v1);
        VerifyEmailEndpoint.Map(v1);
        ResendVerificationEmailEndpoint.Map(v1);
        ForgotPasswordEndpoint.Map(v1);
        ResetPasswordEndpoint.Map(v1);
        GetExternalProvidersEndpoint.Map(v1);
        StartExternalLoginEndpoint.Map(v1);
        HandleExternalLoginCallbackEndpoint.Map(v1);
        GetPendingExternalLoginEndpoint.Map(v1);
        CompleteExternalLoginEndpoint.Map(v1);
        CancelExternalLoginEndpoint.Map(v1);
        ConfirmProviderLinkEndpoint.Map(v1);

        return v1;
    }
}
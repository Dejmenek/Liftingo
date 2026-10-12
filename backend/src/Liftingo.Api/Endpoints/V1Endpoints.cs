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
        ForgotPassword.Map(v1);
        ResetPassword.Map(v1);
        GetExternalProviders.Map(v1);
        StartExternalLogin.Map(v1);
        HandleExternalLoginCallback.Map(v1);
        GetPendingExternalLogin.Map(v1);
        CompleteExternalLogin.Map(v1);
        CancelExternalLogin.Map(v1);
        ConfirmProviderLink.Map(v1);

        return v1;
    }
}
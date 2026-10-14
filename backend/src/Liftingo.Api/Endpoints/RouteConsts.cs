namespace Liftingo.Api.Endpoints;

public static class RouteConsts
{
    public static class Auth
    {
        public const string SessionPrefix = "/auth/session";

        public const string Register = "/auth/register";
        public const string Login = "/auth/login";
        public const string Me = "/auth/me";
        public const string RefreshSession = "/auth/session/refresh";
        public const string Logout = "/auth/session/logout";
        public const string LogoutAllDevices = "/auth/session/logout-all";
        public const string VerifyEmail = "/auth/email/verify";
        public const string ResendVerificationEmail = "/auth/email/resend-verification";
        public const string ForgotPassword = "/auth/password/forgot";
        public const string ResetPassword = "/auth/password/reset";
        public const string ExternalProviders = "/auth/external/providers";
        public const string ExternalLogin = "/auth/external/{provider}/login";
        public const string ConfirmProviderLink = "/auth/external/confirm-link";
    }
}
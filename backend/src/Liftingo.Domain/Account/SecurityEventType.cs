namespace Liftingo.Domain.Account;

public enum SecurityEventType
{
    Login,
    ExternalLogin,
    Logout,
    LogoutAllDevices,
    PasswordResetCompleted,
    ProviderLinked,
    LockoutTriggered,
    RefreshTokenReuse,
}

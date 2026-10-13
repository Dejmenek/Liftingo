namespace Liftingo.Api.Security;

internal static class AuthenticationSchemes
{
    // Short-lived encrypted cookie that holds the provider's result until the user gives consent.
    public const string External = "Liftingo.External";
}

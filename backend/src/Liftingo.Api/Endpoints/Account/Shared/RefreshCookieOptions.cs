namespace Liftingo.Api.Endpoints.Account.Shared;

/// <summary>Where the refresh cookie is sent. Not read from configuration: set from the API version prefix at startup.</summary>
internal sealed class RefreshCookieOptions
{
    public string Path { get; set; } = string.Empty;
}

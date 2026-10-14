namespace Liftingo.Api.Endpoints.Account;

/// <summary>
/// The JSON body of <c>POST /auth/external/{provider}/login</c>. The provider comes from the route; the command also
/// carries the caller's device and address.
/// </summary>
/// <param name="Token">A Google ID token or a Facebook access token.</param>
/// <param name="AcceptTermsAndAge">Sent again with the same token after an <c>Auth.ConsentRequired</c> answer.</param>
/// <param name="AcceptHealthData">Sent again with the same token after an <c>Auth.ConsentRequired</c> answer.</param>
/// <param name="AcceptMarketing">Optional; <c>false</c> when missing.</param>
/// <param name="Email">Needed only when the provider gave no e-mail (<c>emailRequired</c> in the consent answer).</param>
public sealed record ExternalLoginRequest(
    string Token,
    bool? AcceptTermsAndAge,
    bool? AcceptHealthData,
    bool? AcceptMarketing,
    string? Email);
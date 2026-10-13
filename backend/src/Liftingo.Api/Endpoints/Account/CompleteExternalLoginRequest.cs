namespace Liftingo.Api.Endpoints.Account;

/// <summary>
/// The JSON body of <c>POST /auth/external/complete</c>. The command also carries the pending provider sign-in from
/// the external cookie and the caller's device and address.
/// </summary>
/// <param name="AcceptTermsAndAge">Required.</param>
/// <param name="AcceptHealthData">Required.</param>
/// <param name="AcceptMarketing">Optional.</param>
/// <param name="Email">Needed only when the provider gave no e-mail (<c>emailRequired</c> in the pending response).</param>
public sealed record CompleteExternalLoginRequest(
    bool AcceptTermsAndAge,
    bool AcceptHealthData,
    bool AcceptMarketing,
    string? Email);

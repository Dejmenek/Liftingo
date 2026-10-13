namespace Liftingo.Api.Endpoints.Account;

/// <summary>The JSON body of <c>POST /auth/external/confirm-link</c>. The command also carries the caller's device and address.</summary>
/// <param name="Token">The token from the link-confirmation e-mail.</param>
/// <param name="AcceptTermsAndAge">Only read when the account e-mail is unverified.</param>
/// <param name="AcceptHealthData">Only read when the account e-mail is unverified.</param>
/// <param name="AcceptMarketing">Only read when the account e-mail is unverified.</param>
public sealed record ConfirmProviderLinkRequest(
    string Token,
    bool? AcceptTermsAndAge,
    bool? AcceptHealthData,
    bool? AcceptMarketing);

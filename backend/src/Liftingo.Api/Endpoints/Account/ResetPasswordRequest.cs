namespace Liftingo.Api.Endpoints.Account;

/// <summary>The JSON body of <c>POST /auth/password/reset</c>. The command also carries the caller's device and address.</summary>
/// <param name="Token">The token from the reset e-mail.</param>
/// <param name="NewPassword">The new password.</param>
/// <param name="AcceptTermsAndAge">Only read when the account e-mail is unverified.</param>
/// <param name="AcceptHealthData">Only read when the account e-mail is unverified.</param>
/// <param name="AcceptMarketing">Only read when the account e-mail is unverified.</param>
public sealed record ResetPasswordRequest(
    string Token,
    string NewPassword,
    bool? AcceptTermsAndAge,
    bool? AcceptHealthData,
    bool? AcceptMarketing);

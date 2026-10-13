using Liftingo.Application.Common.Messaging;
using Liftingo.Application.Features.Account.Shared;

namespace Liftingo.Application.Features.Account.ResetPassword;

/// <param name="Token">The token from the reset e-mail.</param>
/// <param name="NewPassword">The new password.</param>
/// <param name="AcceptTermsAndAge">Only read when the account e-mail is unverified.</param>
/// <param name="AcceptHealthData">Only read when the account e-mail is unverified.</param>
/// <param name="AcceptMarketing">Only read when the account e-mail is unverified.</param>
/// <param name="Client">The caller's device and address.</param>
public sealed record ResetPasswordCommand(
    string Token,
    string NewPassword,
    bool? AcceptTermsAndAge,
    bool? AcceptHealthData,
    bool? AcceptMarketing,
    ClientInfo Client) : ICommand;

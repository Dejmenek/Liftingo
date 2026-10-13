using Liftingo.Application.Features.Account.Shared;

namespace Liftingo.Application.Features.Account.ConfirmProviderLink;

/// <param name="Token">The token from the link-confirmation e-mail.</param>
/// <param name="AcceptTermsAndAge">Only read when the account e-mail is unverified (R17).</param>
/// <param name="AcceptHealthData">Only read when the account e-mail is unverified (R17).</param>
/// <param name="AcceptMarketing">Only read when the account e-mail is unverified (R17).</param>
/// <param name="Client">The caller's device and address.</param>
public sealed record ConfirmProviderLinkCommand(
    string Token,
    bool? AcceptTermsAndAge,
    bool? AcceptHealthData,
    bool? AcceptMarketing,
    ClientInfo Client);

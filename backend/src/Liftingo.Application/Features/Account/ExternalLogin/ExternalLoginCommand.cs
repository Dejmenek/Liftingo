using Liftingo.Application.Common.Messaging;
using Liftingo.Application.Features.Account.Shared;

namespace Liftingo.Application.Features.Account.ExternalLogin;

/// <param name="Provider">The provider's route name, e.g. <c>google</c>.</param>
/// <param name="Token">A Google ID token or a Facebook access token, as the client got it from the provider.</param>
/// <param name="AcceptTermsAndAge">Only read when the sign-in would create an account or take over an unverified one.</param>
/// <param name="AcceptHealthData">Only read when the sign-in would create an account or take over an unverified one.</param>
/// <param name="AcceptMarketing">Only read when the sign-in would create an account or take over an unverified one.</param>
/// <param name="Email">Only read when the provider gave no e-mail.</param>
public sealed record ExternalLoginCommand(
    string Provider,
    string Token,
    bool? AcceptTermsAndAge,
    bool? AcceptHealthData,
    bool? AcceptMarketing,
    string? Email,
    ClientInfo Client) : ICommand<ExternalLoginResponse>;
using Liftingo.Application.Common.Messaging;
using Liftingo.Application.Common.Security;
using Liftingo.Application.Features.Account.Shared;

namespace Liftingo.Application.Features.Account.CompleteExternalLogin;

/// <param name="AcceptTermsAndAge">Required.</param>
/// <param name="AcceptHealthData">Required.</param>
/// <param name="AcceptMarketing">Optional.</param>
/// <param name="Email">Needed only when the provider gave no e-mail (<c>emailRequired</c> in the pending response).</param>
/// <param name="External">The pending provider sign-in, or <c>null</c> when there is none or it expired.</param>
/// <param name="Client">The caller's device and address.</param>
public sealed record CompleteExternalLoginCommand(
    bool AcceptTermsAndAge,
    bool AcceptHealthData,
    bool AcceptMarketing,
    string? Email,
    ExternalIdentity? External,
    ClientInfo Client) : ICommand<CompleteExternalLoginResponse>;

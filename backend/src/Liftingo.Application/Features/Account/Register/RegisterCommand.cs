using Liftingo.Application.Common.Messaging;
using Liftingo.Application.Features.Account.Shared;

namespace Liftingo.Application.Features.Account.Register;

/// <param name="Email">The e-mail; stored trimmed.</param>
/// <param name="AcceptTermsAndAge">Required consent.</param>
/// <param name="AcceptHealthData">Required consent.</param>
/// <param name="AcceptMarketing">Optional consent; <c>false</c> is recorded as a refusal.</param>
public sealed record RegisterCommand(
    string Email,
    string Password,
    bool AcceptTermsAndAge,
    bool AcceptHealthData,
    bool AcceptMarketing,
    ClientInfo Client) : ICommand<RegisterResponse>;
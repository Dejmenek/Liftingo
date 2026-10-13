using Liftingo.Application.Common.Messaging;
using Liftingo.Application.Features.Account.Shared;

namespace Liftingo.Application.Features.Account.Register;

/// <param name="Email">The e-mail; stored trimmed (FR-005).</param>
/// <param name="Password">The password.</param>
/// <param name="AcceptTermsAndAge">Required consent (FR-003).</param>
/// <param name="AcceptHealthData">Required consent (FR-003).</param>
/// <param name="AcceptMarketing">Optional consent; <c>false</c> is recorded as a refusal (FR-004).</param>
/// <param name="Client">The caller's device and address.</param>
public sealed record RegisterCommand(
    string Email,
    string Password,
    bool AcceptTermsAndAge,
    bool AcceptHealthData,
    bool AcceptMarketing,
    ClientInfo Client) : ICommand<RegisterResponse>;

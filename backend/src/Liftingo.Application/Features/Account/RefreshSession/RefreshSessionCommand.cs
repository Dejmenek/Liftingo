using Liftingo.Application.Features.Account.Shared;

namespace Liftingo.Application.Features.Account.RefreshSession;

/// <param name="RefreshToken">The token from the refresh cookie, or <c>null</c> when the request had none.</param>
/// <param name="Client">The caller's device and address.</param>
public sealed record RefreshSessionCommand(string? RefreshToken, ClientInfo Client);

using Liftingo.Application.Common.Messaging;
using Liftingo.Application.Features.Account.Shared;

namespace Liftingo.Application.Features.Account.Logout;

/// <param name="RefreshToken">The token from the refresh cookie, or <c>null</c> when the request had none.</param>
public sealed record LogoutCommand(string? RefreshToken, ClientInfo Client) : ICommand;
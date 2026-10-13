using Liftingo.Application.Common.Messaging;
using Liftingo.Application.Features.Account.Shared;

namespace Liftingo.Application.Features.Account.Login;

/// <param name="Email">The e-mail; trimmed and compared case-insensitively.</param>
public sealed record LoginCommand(string Email, string Password, ClientInfo Client) : ICommand<LoginResponse>;

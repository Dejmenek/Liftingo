using Liftingo.Application.Common.Messaging;
using Liftingo.Application.Features.Account.Shared;

namespace Liftingo.Application.Features.Account.LogoutAllDevices;

/// <summary>Ends every session of the signed-in user, who comes from the access token.</summary>
public sealed record LogoutAllDevicesCommand(ClientInfo Client) : ICommand;

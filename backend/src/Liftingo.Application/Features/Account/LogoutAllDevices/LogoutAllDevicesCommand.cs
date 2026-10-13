using Liftingo.Application.Features.Account.Shared;

namespace Liftingo.Application.Features.Account.LogoutAllDevices;

/// <param name="Client">The caller's device and address. The signed-in user comes from the access token.</param>
public sealed record LogoutAllDevicesCommand(ClientInfo Client);

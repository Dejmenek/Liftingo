using Liftingo.Application.Common.Messaging;
using Liftingo.Application.Common.Security;
using Liftingo.Application.Features.Account.Shared;

namespace Liftingo.Application.Features.Account.HandleExternalLoginCallback;

/// <param name="External">What the provider returned, or <c>null</c> when it failed, the user cancelled, or the cookie is invalid.</param>
/// <param name="Client">The caller's device and address; the device id from the start of the flow wins.</param>
public sealed record HandleExternalLoginCallbackCommand(ExternalIdentity? External, ClientInfo Client) : ICommand<HandleExternalLoginCallbackResponse>;

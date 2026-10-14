using Liftingo.Application.Common.Messaging;

namespace Liftingo.Application.Features.Account.ResendVerificationEmail;

/// <summary>Sends a new verification link. The signed-in user comes from the access token, so there is no input.</summary>
public sealed record ResendVerificationEmailCommand : ICommand;
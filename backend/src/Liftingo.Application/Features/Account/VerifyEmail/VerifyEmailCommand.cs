using Liftingo.Application.Common.Messaging;

namespace Liftingo.Application.Features.Account.VerifyEmail;

/// <param name="Token">The token from the verification e-mail.</param>
public sealed record VerifyEmailCommand(string Token) : ICommand<VerifyEmailResponse>;
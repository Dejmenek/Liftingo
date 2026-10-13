namespace Liftingo.Application.Features.Account.ForgotPassword;

/// <param name="Email">The address to send the reset link to, if it has an account.</param>
public sealed record ForgotPasswordCommand(string Email);

namespace Liftingo.Api.Endpoints.Account;

/// <summary>The JSON body of <c>POST /auth/login</c>. The command also carries the caller's device and address.</summary>
public sealed record LoginRequest(string Email, string Password);

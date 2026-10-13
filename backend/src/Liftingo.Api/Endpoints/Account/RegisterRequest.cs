namespace Liftingo.Api.Endpoints.Account;

/// <summary>The JSON body of <c>POST /auth/register</c>. The command also carries the caller's device and address.</summary>
public sealed record RegisterRequest(
    string Email,
    string Password,
    bool AcceptTermsAndAge,
    bool AcceptHealthData,
    bool AcceptMarketing);

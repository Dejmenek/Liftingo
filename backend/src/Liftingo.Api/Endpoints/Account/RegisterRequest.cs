namespace Liftingo.Api.Endpoints.Account;

public sealed record RegisterRequest(
    string Email,
    string Password,
    bool AcceptTermsAndAge,
    bool AcceptHealthData,
    bool AcceptMarketing);

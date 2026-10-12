using Bogus;

namespace Liftingo.Api.IntegrationTests.Infrastructure.Fakers;

/// <summary>
/// The JSON body of <c>POST /auth/register</c>, defined here (not reused from Application) so a test can
/// send values the real request type would not allow.
/// </summary>
public sealed record RegisterRequestBody(
    string Email,
    string Password,
    bool AcceptTermsAndAge,
    bool AcceptHealthData,
    bool AcceptMarketing);

public sealed class RegisterRequestFaker : Faker<RegisterRequestBody>
{
    // Meets the default policy: at least 8 characters, a digit and a special character.
    public const string ValidPassword = "Sztanga1!";

    public RegisterRequestFaker()
    {
        CustomInstantiator(f => new RegisterRequestBody(
            // Unique per call, so tests never collide on the unique e-mail index.
            $"{f.Random.String2(10, "abcdefghijklmnopqrstuvwxyz0123456789")}@example.pl",
            ValidPassword,
            AcceptTermsAndAge: true,
            AcceptHealthData: true,
            AcceptMarketing: false));
    }
}

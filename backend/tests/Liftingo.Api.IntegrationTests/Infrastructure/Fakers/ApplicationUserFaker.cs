using Bogus;

using Liftingo.Infrastructure.Identity;

namespace Liftingo.Api.IntegrationTests.Infrastructure.Fakers;

public sealed class ApplicationUserFaker : Faker<ApplicationUser>
{
    public ApplicationUserFaker()
    {
        CustomInstantiator(f =>
        {
            string email = f.Internet.Email();

            return new ApplicationUser
            {
                Id = f.Random.Guid(),
                Email = email,
                UserName = email,
                NormalizedEmail = email.ToUpperInvariant(),
                NormalizedUserName = email.ToUpperInvariant(),
                EmailConfirmed = true,
                SecurityStamp = f.Random.Guid().ToString(),
            };
        });
    }
}
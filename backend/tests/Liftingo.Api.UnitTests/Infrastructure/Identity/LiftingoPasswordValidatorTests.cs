using Liftingo.Infrastructure.Identity;

using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using NSubstitute;

namespace Liftingo.Api.UnitTests.Infrastructure.Identity;

public class LiftingoPasswordValidatorTests
{
    private readonly UserManager<ApplicationUser> _userManager = CreateUserManager();
    private readonly LiftingoPasswordValidator _validator = new();

    [Fact]
    public async Task ValidateAsync_PolishLettersAndDigitOnly_IsRejectedForMissingSpecialCharacter()
    {
        IdentityResult result = await _validator.ValidateAsync(_userManager, new ApplicationUser(), "zażółć12");

        Assert.False(result.Succeeded);
        Assert.Contains(result.Errors, e => e.Code == nameof(IdentityErrorDescriber.PasswordRequiresNonAlphanumeric));
    }

    [Fact]
    public async Task ValidateAsync_PolishLettersDigitAndPunctuation_IsAccepted()
    {
        IdentityResult result = await _validator.ValidateAsync(_userManager, new ApplicationUser(), "zażółć1!");

        Assert.True(result.Succeeded);
    }

    [Fact]
    public async Task ValidateAsync_SpaceAsOnlySpecialCharacter_IsAccepted()
    {
        IdentityResult result = await _validator.ValidateAsync(_userManager, new ApplicationUser(), "abc 1234");

        Assert.True(result.Succeeded);
    }

    [Fact]
    public async Task ValidateAsync_TooShortPassword_IsRejectedForLength()
    {
        IdentityResult result = await _validator.ValidateAsync(_userManager, new ApplicationUser(), "ab1!");

        Assert.Contains(result.Errors, e => e.Code == nameof(IdentityErrorDescriber.PasswordTooShort));
    }

    // The validator only reads UserManager.Options, so the store is never touched.
    private static UserManager<ApplicationUser> CreateUserManager()
    {
        var options = new IdentityOptions();
        options.Password.RequiredLength = 8;
        options.Password.RequireDigit = true;
        options.Password.RequireNonAlphanumeric = true;
        options.Password.RequireUppercase = false;
        options.Password.RequireLowercase = false;

        return new UserManager<ApplicationUser>(
            Substitute.For<IUserStore<ApplicationUser>>(),
            Options.Create(options),
            passwordHasher: null!,
            userValidators: null!,
            passwordValidators: null!,
            keyNormalizer: null!,
            errors: null!,
            services: null!,
            logger: NullLogger<UserManager<ApplicationUser>>.Instance);
    }
}

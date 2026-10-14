using Microsoft.AspNetCore.Identity;

namespace Liftingo.Infrastructure.Identity;

/// <summary>
/// Identity treats only ASCII letters and digits as such, so a Polish letter would count as a special
/// character. A special character is neither a letter nor a digit in any script.
/// </summary>
public sealed class LiftingoPasswordValidator(IdentityErrorDescriber? errors = null)
    : PasswordValidator<ApplicationUser>(errors)
{
    public override bool IsLetterOrDigit(char c) => char.IsLetterOrDigit(c);
}
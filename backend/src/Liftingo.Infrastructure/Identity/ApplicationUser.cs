using Microsoft.AspNetCore.Identity;

namespace Liftingo.Infrastructure.Identity;

public class ApplicationUser : IdentityUser<Guid>
{
    public DateTimeOffset CreatedAt { get; set; }
}
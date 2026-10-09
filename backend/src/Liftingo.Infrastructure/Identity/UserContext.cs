using Liftingo.Application.Common;
using Microsoft.AspNetCore.Http;
using System.Security.Claims;

namespace Liftingo.Infrastructure.Identity;

public sealed class UserContext(IHttpContextAccessor httpContextAccessor) : IUserContext
{
    private const string UserIdClaimType = ClaimTypes.NameIdentifier;

    public bool IsAuthenticated =>
        httpContextAccessor.HttpContext?.User.Identity?.IsAuthenticated == true;

    public Guid UserId
    {
        get
        {
            ClaimsPrincipal? user = httpContextAccessor.HttpContext?.User;

            if (user?.Identity?.IsAuthenticated != true)
            {
                throw new InvalidOperationException("The current request is not authenticated.");
            }

            string? value = user.FindFirstValue(UserIdClaimType);

            if (!Guid.TryParse(value, out Guid userId))
            {
                throw new InvalidOperationException(
                    $"The authenticated user has no valid '{UserIdClaimType}' claim.");
            }

            return userId;
        }
    }
}

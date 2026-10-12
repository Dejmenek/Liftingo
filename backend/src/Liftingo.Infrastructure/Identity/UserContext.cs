using Liftingo.Application.Common;
using Microsoft.AspNetCore.Http;
using System.Security.Claims;

namespace Liftingo.Infrastructure.Identity;

public sealed class UserContext(IHttpContextAccessor httpContextAccessor) : IUserContext
{
    private const string UserIdClaimType = ClaimTypes.NameIdentifier;
    private const string EmailVerifiedClaimType = "email_verified";
    private const string SessionIdClaimType = "sid";

    public bool IsAuthenticated =>
        httpContextAccessor.HttpContext?.User.Identity?.IsAuthenticated == true;

    public bool IsEmailVerified =>
        IsAuthenticated &&
        bool.TryParse(httpContextAccessor.HttpContext?.User.FindFirstValue(EmailVerifiedClaimType), out bool verified) &&
        verified;

    public Guid? SessionId =>
        IsAuthenticated &&
        Guid.TryParse(httpContextAccessor.HttpContext?.User.FindFirstValue(SessionIdClaimType), out Guid sessionId)
            ? sessionId
            : null;

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
using Liftingo.Application.Common.Exceptions;
using Liftingo.Application.Common.Security;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.DependencyInjection;
using System.Runtime.ExceptionServices;
using System.Security.Claims;

namespace Liftingo.Infrastructure.Identity;

/// <summary>
/// Ties an access token to its session: a token whose session was revoked or has expired is rejected at
/// once, not when it runs out.
/// </summary>
public static class SessionValidation
{
    public static async Task OnTokenValidatedAsync(TokenValidatedContext context)
    {
        ClaimsPrincipal? principal = context.Principal;

        string? subject = principal?.FindFirstValue("sub");
        bool hasSession = Guid.TryParse(principal?.FindFirstValue("sid"), out Guid sessionId);

        if (principal?.Identity is not ClaimsIdentity identity || subject is null || !hasSession)
        {
            context.Fail("The token carries no subject or session.");

            return;
        }

        var sessions = context.HttpContext.RequestServices.GetRequiredService<ISessionStore>();

        if (!await sessions.IsActiveAsync(sessionId, context.HttpContext.RequestAborted))
        {
            context.Fail("The session is no longer active.");

            return;
        }

        // UserContext reads NameIdentifier, and inbound claim mapping is off.
        identity.AddClaim(new Claim(ClaimTypes.NameIdentifier, subject));
    }

    public static Task OnAuthenticationFailedAsync(AuthenticationFailedContext context)
    {
        // Make sure a store outage reaches the exception handler (503) however JwtBearerHandler
        // treats exceptions thrown from its events.
        if (context.Exception is SessionStoreUnavailableException)
        {
            ExceptionDispatchInfo.Capture(context.Exception).Throw();
        }

        return Task.CompletedTask;
    }
}
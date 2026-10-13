using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace Liftingo.Infrastructure.Identity;

/// <summary>
/// Configures the JWT bearer scheme that Api registers (R20): the signing key, issuer and audience from
/// <see cref="JwtOptions"/>, lifetimes checked against the injected clock, and the session check of
/// <see cref="SessionValidation"/>.
/// </summary>
internal sealed class JwtBearerOptionsSetup(IOptions<JwtOptions> jwtOptions, TimeProvider timeProvider)
    : IConfigureNamedOptions<JwtBearerOptions>
{
    public void Configure(string? name, JwtBearerOptions options)
    {
        if (name == JwtBearerDefaults.AuthenticationScheme)
        {
            Configure(options);
        }
    }

    public void Configure(JwtBearerOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        JwtOptions jwt = jwtOptions.Value;

        // Keep sub, sid and email_verified under their JWT names.
        options.MapInboundClaims = false;

        options.Events = new JwtBearerEvents
        {
            OnTokenValidated = SessionValidation.OnTokenValidatedAsync,
            OnAuthenticationFailed = SessionValidation.OnAuthenticationFailedAsync,
        };

        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = jwt.Issuer,
            ValidateAudience = true,
            ValidAudience = jwt.Audience,
            ValidateLifetime = true,
            RequireExpirationTime = true,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Convert.FromBase64String(jwt.SigningKey)),
            ValidAlgorithms = [SecurityAlgorithms.HmacSha256],
            ClockSkew = TimeSpan.Zero,
            LifetimeValidator = (notBefore, expires, _, _) => IsWithinLifetime(timeProvider.GetUtcNow().UtcDateTime, notBefore, expires),
        };
    }

    // Lifetime is checked against the injected TimeProvider, so tests with a fake clock stay consistent
    // with the tokens they issue. There is no clock skew: an access token lives exactly its lifetime.
    private static bool IsWithinLifetime(DateTime utcNow, DateTime? notBefore, DateTime? expires) =>
        expires is not null && expires.Value > utcNow && (notBefore is null || notBefore.Value <= utcNow);
}

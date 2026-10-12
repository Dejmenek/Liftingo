using Liftingo.Application.Common.Security;

using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace Liftingo.Infrastructure.Identity;

/// <summary>Issues the 15-minute HS256 access token. Claim names stay as in the JWT (inbound mapping is off).</summary>
public sealed class JwtAccessTokenIssuer(IOptions<JwtOptions> options, TimeProvider timeProvider) : IAccessTokenIssuer
{
    private readonly JsonWebTokenHandler _handler = new();
    private readonly SigningCredentials _credentials = new(
        new SymmetricSecurityKey(Convert.FromBase64String(options.Value.SigningKey)),
        SecurityAlgorithms.HmacSha256);

    public AccessToken Issue(Guid userId, Guid sessionId, bool emailVerified)
    {
        JwtOptions jwt = options.Value;
        DateTimeOffset now = timeProvider.GetUtcNow();
        DateTimeOffset expiresAt = now + jwt.AccessTokenLifetime;

        var descriptor = new SecurityTokenDescriptor
        {
            Issuer = jwt.Issuer,
            Audience = jwt.Audience,
            IssuedAt = now.UtcDateTime,
            Expires = expiresAt.UtcDateTime,
            SigningCredentials = _credentials,
            Claims = new Dictionary<string, object>
            {
                [JwtRegisteredClaimNames.Sub] = userId.ToString(),
                ["sid"] = sessionId.ToString(),
                ["email_verified"] = emailVerified,
                [JwtRegisteredClaimNames.Jti] = Guid.NewGuid().ToString(),
            },
        };

        return new AccessToken(_handler.CreateToken(descriptor), expiresAt);
    }
}

using Liftingo.Infrastructure.Identity;
using Microsoft.AspNetCore.Http;
using System.Security.Claims;

namespace Liftingo.Api.UnitTests.Common;

public class UserContextTests
{
    [Fact]
    public void UserId_AuthenticatedUserWithNameIdentifierClaim_ReturnsUserId()
    {
        var expected = Guid.NewGuid();
        UserContext sut = CreateSut(CreatePrincipal(authenticated: true, (ClaimTypes.NameIdentifier, expected.ToString())));

        Assert.Equal(expected, sut.UserId);
    }

    [Fact]
    public void IsAuthenticated_AuthenticatedUser_ReturnsTrue()
    {
        UserContext sut = CreateSut(CreatePrincipal(authenticated: true, (ClaimTypes.NameIdentifier, Guid.NewGuid().ToString())));

        Assert.True(sut.IsAuthenticated);
    }

    [Fact]
    public void IsAuthenticated_UnauthenticatedPrincipal_ReturnsFalse()
    {
        UserContext sut = CreateSut(CreatePrincipal(authenticated: false, (ClaimTypes.NameIdentifier, Guid.NewGuid().ToString())));

        Assert.False(sut.IsAuthenticated);
    }

    [Fact]
    public void IsAuthenticated_NoHttpContext_ReturnsFalse()
    {
        var sut = new UserContext(new HttpContextAccessor());

        Assert.False(sut.IsAuthenticated);
    }

    [Fact]
    public void UserId_UnauthenticatedPrincipal_ThrowsInvalidOperationException()
    {
        UserContext sut = CreateSut(CreatePrincipal(authenticated: false, (ClaimTypes.NameIdentifier, Guid.NewGuid().ToString())));

        Assert.Throws<InvalidOperationException>(() => sut.UserId);
    }

    [Fact]
    public void UserId_NoHttpContext_ThrowsInvalidOperationException()
    {
        var sut = new UserContext(new HttpContextAccessor());

        Assert.Throws<InvalidOperationException>(() => sut.UserId);
    }

    [Fact]
    public void UserId_MissingIdClaim_ThrowsInvalidOperationException()
    {
        UserContext sut = CreateSut(CreatePrincipal(authenticated: true));

        Assert.Throws<InvalidOperationException>(() => sut.UserId);
    }

    [Fact]
    public void UserId_IdClaimIsNotGuid_ThrowsInvalidOperationException()
    {
        UserContext sut = CreateSut(CreatePrincipal(authenticated: true, (ClaimTypes.NameIdentifier, "not-a-guid")));

        Assert.Throws<InvalidOperationException>(() => sut.UserId);
    }

    [Fact]
    public void UserId_IdOnlyInRawSubClaim_ThrowsInvalidOperationException()
    {
        UserContext sut = CreateSut(CreatePrincipal(authenticated: true, ("sub", Guid.NewGuid().ToString())));

        Assert.Throws<InvalidOperationException>(() => sut.UserId);
    }

    private static UserContext CreateSut(ClaimsPrincipal principal) =>
        new(new HttpContextAccessor { HttpContext = new DefaultHttpContext { User = principal } });

    private static ClaimsPrincipal CreatePrincipal(bool authenticated, params (string Type, string Value)[] claims)
    {
        var identity = new ClaimsIdentity(
            claims.Select(c => new Claim(c.Type, c.Value)),
            authenticationType: authenticated ? "TestAuth" : null);

        return new ClaimsPrincipal(identity);
    }
}
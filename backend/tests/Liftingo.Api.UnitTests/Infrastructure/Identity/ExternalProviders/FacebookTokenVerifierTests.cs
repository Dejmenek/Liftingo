using Liftingo.Application.Common.Security;
using Liftingo.Infrastructure.Identity.ExternalProviders;
using Microsoft.Extensions.Logging.Abstractions;
using System.Net;
using System.Text;

namespace Liftingo.Api.UnitTests.Infrastructure.Identity.ExternalProviders;

public sealed class FacebookTokenVerifierTests : IDisposable
{
    private const string AppId = "1111";
    private const string AppSecret = "app-secret";
    private const string UserId = "10203040";
    private const string Token = "EAAB-user-token";

    private static readonly FacebookProviderOptions s_options = new() { AppId = AppId, AppSecret = AppSecret };

    private readonly StubGraphHandler _graph = new();
    private readonly HttpClient _http;

    public FacebookTokenVerifierTests() =>
        _http = new HttpClient(_graph, disposeHandler: false)
        {
            BaseAddress = new Uri($"https://graph.facebook.com/{s_options.GraphApiVersion}/"),
        };

    public void Dispose()
    {
        _http.Dispose();
        _graph.Dispose();
    }

    [Fact]
    public async Task VerifyAsync_ValidToken_ReturnsIdentityWithEmail()
    {
        _graph.DebugToken = DebugTokenAnswer(AppId, isValid: true, UserId);
        _graph.Me = MeAnswer(UserId, "Anna Nowak", "  anna@example.pl ");

        ExternalIdentity? identity = await Verifier().VerifyAsync(Token, TestContext.Current.CancellationToken);

        Assert.NotNull(identity);
        Assert.Equal("Facebook", identity.Provider);
        Assert.Equal(UserId, identity.ProviderKey);
        Assert.Equal("anna@example.pl", identity.Email);
        Assert.Equal("Anna Nowak", identity.DisplayName);
    }

    [Fact]
    public async Task VerifyAsync_ValidToken_AsksDebugTokenWithTheAppCredentials()
    {
        _graph.DebugToken = DebugTokenAnswer(AppId, isValid: true, UserId);
        _graph.Me = MeAnswer(UserId, "Anna Nowak", "anna@example.pl");

        await Verifier().VerifyAsync(Token, TestContext.Current.CancellationToken);

        Uri debugRequest = _graph.Requests[0];
        Assert.Equal("/v26.0/debug_token", debugRequest.AbsolutePath);
        Assert.Contains($"input_token={Token}", debugRequest.Query, StringComparison.Ordinal);
        Assert.Contains($"access_token={AppId}%7C{AppSecret}", debugRequest.Query, StringComparison.Ordinal);
    }

    [Fact]
    public async Task VerifyAsync_ValidToken_SendsTheAppSecretProofToMe()
    {
        _graph.DebugToken = DebugTokenAnswer(AppId, isValid: true, UserId);
        _graph.Me = MeAnswer(UserId, "Anna Nowak", "anna@example.pl");

        await Verifier().VerifyAsync(Token, TestContext.Current.CancellationToken);

        // HMAC-SHA256 of the token keyed with the app secret, lowercase hex.
        string expectedProof = Convert.ToHexStringLower(
            System.Security.Cryptography.HMACSHA256.HashData(Encoding.UTF8.GetBytes(AppSecret), Encoding.UTF8.GetBytes(Token)));

        Uri meRequest = _graph.Requests[1];
        Assert.Equal("/v26.0/me", meRequest.AbsolutePath);
        Assert.Contains($"appsecret_proof={expectedProof}", meRequest.Query, StringComparison.Ordinal);
        Assert.Contains($"access_token={Token}", meRequest.Query, StringComparison.Ordinal);
    }

    [Fact]
    public async Task VerifyAsync_TokenOfAnotherApp_ReturnsNullWithoutAskingMe()
    {
        _graph.DebugToken = DebugTokenAnswer("9999", isValid: true, UserId);
        _graph.Me = MeAnswer(UserId, "Anna Nowak", "anna@example.pl");

        ExternalIdentity? identity = await Verifier().VerifyAsync(Token, TestContext.Current.CancellationToken);

        Assert.Null(identity);
        Assert.Single(_graph.Requests);
    }

    [Fact]
    public async Task VerifyAsync_InvalidToken_ReturnsNull()
    {
        _graph.DebugToken = DebugTokenAnswer(AppId, isValid: false, UserId);
        _graph.Me = MeAnswer(UserId, "Anna Nowak", "anna@example.pl");

        ExternalIdentity? identity = await Verifier().VerifyAsync(Token, TestContext.Current.CancellationToken);

        Assert.Null(identity);
    }

    [Fact]
    public async Task VerifyAsync_MeIdDiffersFromDebugTokenUser_ReturnsNull()
    {
        _graph.DebugToken = DebugTokenAnswer(AppId, isValid: true, UserId);
        _graph.Me = MeAnswer("55555555", "Someone Else", "else@example.pl");

        ExternalIdentity? identity = await Verifier().VerifyAsync(Token, TestContext.Current.CancellationToken);

        Assert.Null(identity);
    }

    [Fact]
    public async Task VerifyAsync_DebugTokenErrorStatus_ReturnsNull()
    {
        _graph.DebugToken = new StubResponse(HttpStatusCode.BadRequest, """{"error":{"message":"Invalid OAuth access token."}}""");

        ExternalIdentity? identity = await Verifier().VerifyAsync(Token, TestContext.Current.CancellationToken);

        Assert.Null(identity);
    }

    [Fact]
    public async Task VerifyAsync_MeErrorStatus_ReturnsNull()
    {
        _graph.DebugToken = DebugTokenAnswer(AppId, isValid: true, UserId);
        _graph.Me = new StubResponse(HttpStatusCode.InternalServerError, "{}");

        ExternalIdentity? identity = await Verifier().VerifyAsync(Token, TestContext.Current.CancellationToken);

        Assert.Null(identity);
    }

    [Fact]
    public async Task VerifyAsync_NetworkError_ReturnsNull()
    {
        _graph.Throw = new HttpRequestException("connection refused");

        ExternalIdentity? identity = await Verifier().VerifyAsync(Token, TestContext.Current.CancellationToken);

        Assert.Null(identity);
    }

    [Fact]
    public async Task VerifyAsync_AccountWithoutEmail_ReturnsIdentityWithoutEmail()
    {
        _graph.DebugToken = DebugTokenAnswer(AppId, isValid: true, UserId);
        _graph.Me = new StubResponse(HttpStatusCode.OK, $$"""{"id":"{{UserId}}","name":"Anna Nowak"}""");

        ExternalIdentity? identity = await Verifier().VerifyAsync(Token, TestContext.Current.CancellationToken);

        Assert.NotNull(identity);
        Assert.Null(identity.Email);
        Assert.Equal(UserId, identity.ProviderKey);
    }

    private FacebookTokenVerifier Verifier() =>
        new(_http, s_options, NullLogger<FacebookTokenVerifier>.Instance);

    private static StubResponse DebugTokenAnswer(string appId, bool isValid, string userId) =>
        new(
            HttpStatusCode.OK,
            $$$"""{"data":{"app_id":"{{{appId}}}","type":"USER","is_valid":{{{(isValid ? "true" : "false")}}},"user_id":"{{{userId}}}"}}""");

    private static StubResponse MeAnswer(string id, string name, string email) =>
        new(HttpStatusCode.OK, $$"""{"id":"{{id}}","name":"{{name}}","email":"{{email}}"}""");

    private sealed record StubResponse(HttpStatusCode Status, string Json);

    private sealed class StubGraphHandler : HttpMessageHandler
    {
        public StubResponse? DebugToken { get; set; }

        public StubResponse? Me { get; set; }

        public Exception? Throw { get; set; }

        public List<Uri> Requests { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (Throw is not null)
            {
                throw Throw;
            }

            Uri uri = request.RequestUri!;
            Requests.Add(uri);

            StubResponse stub = uri.AbsolutePath.EndsWith("/debug_token", StringComparison.Ordinal)
                ? DebugToken ?? throw new InvalidOperationException("No debug_token response set.")
                : Me ?? throw new InvalidOperationException("No me response set.");

            return Task.FromResult(new HttpResponseMessage(stub.Status)
            {
                Content = new StringContent(stub.Json, Encoding.UTF8, "application/json"),
            });
        }
    }
}
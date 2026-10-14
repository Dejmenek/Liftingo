using Liftingo.Application.Common.Security;

namespace Liftingo.Infrastructure.Identity.ExternalProviders;

/// <summary>Hands the credential to the verifier of its provider.</summary>
internal sealed class ExternalIdentityVerifier(IEnumerable<IProviderTokenVerifier> verifiers) : IExternalIdentityVerifier
{
    public Task<ExternalIdentity?> VerifyAsync(string loginProvider, string token, CancellationToken ct)
    {
        IProviderTokenVerifier? verifier = verifiers.FirstOrDefault(
            v => string.Equals(v.LoginProvider, loginProvider, StringComparison.Ordinal));

        return verifier is null
            ? Task.FromResult<ExternalIdentity?>(null)
            : verifier.VerifyAsync(token, ct);
    }
}
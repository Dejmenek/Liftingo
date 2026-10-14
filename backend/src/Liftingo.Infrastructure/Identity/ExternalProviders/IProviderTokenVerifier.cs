using Liftingo.Application.Common.Security;

namespace Liftingo.Infrastructure.Identity.ExternalProviders;

/// <summary>Verifies the credentials of one provider. Registered only when the provider is configured.</summary>
internal interface IProviderTokenVerifier
{
    string LoginProvider { get; }

    Task<ExternalIdentity?> VerifyAsync(string token, CancellationToken ct);
}

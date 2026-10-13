using Liftingo.Application.Common.Result;
using Liftingo.Application.Common.Security;
using Liftingo.Application.Features.Account.Shared;

namespace Liftingo.Application.Features.Account.GetPendingExternalLogin;

/// <summary>What the consent step shows about the pending provider sign-in.</summary>
public sealed class GetPendingExternalLoginQueryHandler(IIdentityService identity, IExternalProviderCatalog catalog)
{
    public async Task<Result<GetPendingExternalLoginResponse>> HandleAsync(GetPendingExternalLoginQuery query, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(query);

        if (query.External is not { } external || catalog.FindRouteName(external.Provider) is not { } provider)
        {
            return Result.Failure<GetPendingExternalLoginResponse>(ExternalLoginErrors.Expired);
        }

        bool accountExists = external.Email is not null
            && await identity.FindByEmailAsync(external.Email, ct) is { EmailVerified: false };

        return new GetPendingExternalLoginResponse(provider, external.Email, external.Email is null, accountExists);
    }
}

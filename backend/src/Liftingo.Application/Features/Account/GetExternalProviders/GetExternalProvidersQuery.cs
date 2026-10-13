using Liftingo.Application.Common.Messaging;

namespace Liftingo.Application.Features.Account.GetExternalProviders;

/// <summary>Lists the configured external sign-in providers. No input.</summary>
public sealed record GetExternalProvidersQuery : IQuery<GetExternalProvidersResponse>;

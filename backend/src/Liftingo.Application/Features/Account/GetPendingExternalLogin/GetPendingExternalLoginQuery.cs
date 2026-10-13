using Liftingo.Application.Common.Messaging;
using Liftingo.Application.Common.Security;

namespace Liftingo.Application.Features.Account.GetPendingExternalLogin;

/// <param name="External">The pending provider sign-in, or <c>null</c> when there is none or it expired.</param>
public sealed record GetPendingExternalLoginQuery(ExternalIdentity? External) : IQuery<GetPendingExternalLoginResponse>;

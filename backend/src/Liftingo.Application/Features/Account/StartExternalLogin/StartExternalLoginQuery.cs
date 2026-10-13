using Liftingo.Application.Common.Messaging;

namespace Liftingo.Application.Features.Account.StartExternalLogin;

/// <param name="Provider">The provider's route name, e.g. <c>google</c>.</param>
public sealed record StartExternalLoginQuery(string Provider) : IQuery<StartExternalLoginResponse>;

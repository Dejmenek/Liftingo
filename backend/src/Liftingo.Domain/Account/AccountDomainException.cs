using Liftingo.Domain.Common;

namespace Liftingo.Domain.Account;

public sealed class AccountDomainException(string message) : DomainException(message);
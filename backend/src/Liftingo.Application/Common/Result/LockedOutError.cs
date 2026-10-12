namespace Liftingo.Application.Common.Result;

public sealed record LockedOutError : Error
{
    public const string ErrorCode = "Auth.LockedOut";

    public DateTimeOffset RetryAfter { get; }

    public LockedOutError(DateTimeOffset retryAfter)
        : base(ErrorCode, "Zbyt wiele nieudanych prób logowania. Spróbuj ponownie później.", ErrorType.TooManyRequests)
    {
        RetryAfter = retryAfter;
    }
}
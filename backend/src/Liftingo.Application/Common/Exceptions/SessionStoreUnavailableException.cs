namespace Liftingo.Application.Common.Exceptions;

/// <summary>The session store (Redis) cannot be reached. Api maps it to 503.</summary>
public sealed class SessionStoreUnavailableException : Exception
{
    public SessionStoreUnavailableException()
        : base("The session store is unavailable.")
    {
    }

    public SessionStoreUnavailableException(string message)
        : base(message)
    {
    }

    public SessionStoreUnavailableException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}

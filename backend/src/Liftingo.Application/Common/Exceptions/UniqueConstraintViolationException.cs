namespace Liftingo.Application.Common.Exceptions;

/// <summary>
/// A unique index rejected a write (SQL Server error 2601 or 2627). A handler turns it into a
/// <c>Result</c> only where two requests can legitimately race for the same value.
/// </summary>
public sealed class UniqueConstraintViolationException : Exception
{
    public UniqueConstraintViolationException()
        : base("A unique constraint was violated.")
    {
    }

    public UniqueConstraintViolationException(string message)
        : base(message)
    {
    }

    public UniqueConstraintViolationException(string message, Exception innerException)
        : base(message, innerException)
    {
    }

    /// <summary>The name of the index or constraint that was violated, when SQL Server's message names it.</summary>
    public string? IndexName { get; init; }
}
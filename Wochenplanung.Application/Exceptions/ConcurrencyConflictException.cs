namespace Wochenplanung.Application.Exceptions;

public sealed class ConcurrencyConflictException : Exception
{
    public ConcurrencyConflictException()
        : base("The record was changed by another user.")
    {
    }
}

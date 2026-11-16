namespace ShortLink.Application.Common.Exceptions;

/// <summary>
/// Thrown when a requested resource is not found in the persistence store.
/// Handled by ExceptionHandlingMiddleware to return HTTP 404 Not Found.
/// </summary>
public class NotFoundException : Exception
{
    public NotFoundException(string message) : base(message)
    {
    }

    public NotFoundException(string name, object key)
        : base($"Entity '{name}' with identifier '{key}' was not found.")
    {
    }
}

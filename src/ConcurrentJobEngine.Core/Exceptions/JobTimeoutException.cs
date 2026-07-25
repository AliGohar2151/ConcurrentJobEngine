using System;

namespace ConcurrentJobEngine.Core.Exceptions;

/// <summary>
/// Exception thrown when job execution times out.
/// </summary>
public class JobTimeoutException : Exception
{
    public JobTimeoutException() { }

    public JobTimeoutException(string message) : base(message) { }

    public JobTimeoutException(string message, Exception innerException) : base(message, innerException) { }
}

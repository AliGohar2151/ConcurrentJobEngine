using System;

namespace ConcurrentJobEngine.Core.Exceptions;

/// <summary>
/// Exception thrown when job execution times out.
/// </summary>
public class JobTimeoutException : Exception
{
    /// <summary>
    /// Initializes a new instance of the <see cref="JobTimeoutException"/> class.
    /// </summary>
    public JobTimeoutException() { }

    /// <summary>
    /// Initializes a new instance of the <see cref="JobTimeoutException"/> class with a specified error message.
    /// </summary>
    /// <param name="message">The message that describes the error.</param>
    public JobTimeoutException(string message) : base(message) { }

    /// <summary>
    /// Initializes a new instance of the <see cref="JobTimeoutException"/> class with a specified error message and inner exception.
    /// </summary>
    /// <param name="message">The message that describes the error.</param>
    /// <param name="innerException">The exception that is the cause of the current exception.</param>
    public JobTimeoutException(string message, Exception innerException) : base(message, innerException) { }
}

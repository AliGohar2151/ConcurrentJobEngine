using System;

namespace ConcurrentJobEngine.Core.Exceptions;

/// <summary>
/// Exception thrown when job execution fails.
/// </summary>
public class JobExecutionException : Exception
{
    /// <summary>
    /// Initializes a new instance of the <see cref="JobExecutionException"/> class.
    /// </summary>
    public JobExecutionException() { }

    /// <summary>
    /// Initializes a new instance of the <see cref="JobExecutionException"/> class with a specified error message.
    /// </summary>
    /// <param name="message">The message that describes the error.</param>
    public JobExecutionException(string message) : base(message) { }

    /// <summary>
    /// Initializes a new instance of the <see cref="JobExecutionException"/> class with a specified error message and inner exception.
    /// </summary>
    /// <param name="message">The message that describes the error.</param>
    /// <param name="innerException">The exception that is the cause of the current exception.</param>
    public JobExecutionException(string message, Exception innerException) : base(message, innerException) { }
}

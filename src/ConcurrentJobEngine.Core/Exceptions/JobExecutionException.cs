using System;

namespace ConcurrentJobEngine.Core.Exceptions;

/// <summary>
/// Exception thrown when job execution fails.
/// </summary>
public class JobExecutionException : Exception
{
    public JobExecutionException() { }

    public JobExecutionException(string message) : base(message) { }

    public JobExecutionException(string message, Exception innerException) : base(message, innerException) { }
}

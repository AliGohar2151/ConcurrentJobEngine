using System;

namespace ConcurrentJobEngine.Core.Exceptions;

/// <summary>
/// Exception thrown when a job is rejected by the engine (e.g. queue capacity limits).
/// </summary>
public class JobRejectedException : Exception
{
    public JobRejectedException() { }

    public JobRejectedException(string message) : base(message) { }

    public JobRejectedException(string message, Exception innerException) : base(message, innerException) { }
}

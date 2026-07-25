namespace ConcurrentJobEngine.Core.Enums;

/// <summary>
/// Specifies the reason why a job failed execution.
/// </summary>
public enum FailureReason
{
    /// <summary>
    /// No failure occurred.
    /// </summary>
    None = 0,

    /// <summary>
    /// Job payload or option validation failed.
    /// </summary>
    ValidationError,

    /// <summary>
    /// No registered handler was found for the job payload type.
    /// </summary>
    HandlerNotFound,

    /// <summary>
    /// The handler threw an unhandled exception or returned a failure result.
    /// </summary>
    ExecutionFailed,

    /// <summary>
    /// Job execution exceeded the configured timeout limit.
    /// </summary>
    Timeout,

    /// <summary>
    /// Job execution was explicitly cancelled.
    /// </summary>
    Cancelled,

    /// <summary>
    /// Maximum allowed retry attempts were exhausted.
    /// </summary>
    MaxAttemptsExhausted
}

namespace ConcurrentJobEngine.Core.Enums;

/// <summary>
/// Specifies the reason why a job failed execution.
/// </summary>
public enum FailureReason
{
    None = 0,
    ValidationError,
    HandlerNotFound,
    ExecutionFailed,
    Timeout,
    Cancelled,
    MaxAttemptsExhausted
}

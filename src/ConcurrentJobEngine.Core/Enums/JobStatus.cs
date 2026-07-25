namespace ConcurrentJobEngine.Core.Enums;

/// <summary>
/// Represents the current state of a job in the processing engine lifecycle.
/// </summary>
public enum JobStatus
{
    Submitted = 0,
    Queued,
    Running,
    Completed,
    Failed,
    Retrying,
    Cancelled,
    TimedOut,
    DeadLettered
}

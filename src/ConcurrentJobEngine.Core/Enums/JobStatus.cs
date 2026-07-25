namespace ConcurrentJobEngine.Core.Enums;

/// <summary>
/// Represents the current state of a job in the processing engine lifecycle.
/// </summary>
public enum JobStatus
{
    /// <summary>
    /// Job has been submitted to the engine and initial state persisted.
    /// </summary>
    Submitted = 0,

    /// <summary>
    /// Job has been placed into the priority scheduler queue waiting for a worker thread.
    /// </summary>
    Queued,

    /// <summary>
    /// A worker thread has picked up the job and handler execution is in progress.
    /// </summary>
    Running,

    /// <summary>
    /// Job execution completed successfully.
    /// </summary>
    Completed,

    /// <summary>
    /// Job execution failed.
    /// </summary>
    Failed,

    /// <summary>
    /// Job execution failed and a retry delay is active before re-queuing.
    /// </summary>
    Retrying,

    /// <summary>
    /// Job execution was cancelled before or during handler execution.
    /// </summary>
    Cancelled,

    /// <summary>
    /// Job execution exceeded its allowed timeout limit.
    /// </summary>
    TimedOut,

    /// <summary>
    /// Job retry attempts were exhausted and record was sent to the dead-letter store.
    /// </summary>
    DeadLettered
}

using System;
using ConcurrentJobEngine.Core.Enums;

namespace ConcurrentJobEngine.Core.Models;

/// <summary>
/// Provides contextual information about the current execution attempt of a job.
/// </summary>
public sealed class JobExecutionContext
{
    /// <summary>
    /// Gets the unique ID of the job being executed.
    /// </summary>
    public Guid JobId { get; }

    /// <summary>
    /// Gets the current attempt number (starting at 1).
    /// </summary>
    public int AttemptNumber { get; }

    /// <summary>
    /// Gets the timestamp when the execution attempt started.
    /// </summary>
    public DateTimeOffset StartedAt { get; }

    /// <summary>
    /// Gets the scheduling priority of the job being executed.
    /// </summary>
    public JobPriority Priority { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="JobExecutionContext"/> class.
    /// </summary>
    public JobExecutionContext(Guid jobId, int attemptNumber, DateTimeOffset startedAt, JobPriority priority)
    {
        JobId = jobId;
        AttemptNumber = attemptNumber;
        StartedAt = startedAt;
        Priority = priority;
    }
}

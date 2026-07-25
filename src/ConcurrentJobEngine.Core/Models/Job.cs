using System;
using System.Collections.Generic;
using ConcurrentJobEngine.Core.Abstractions;
using ConcurrentJobEngine.Core.Enums;

namespace ConcurrentJobEngine.Core.Models;

/// <summary>
/// Represents a job tracking metadata and payload inside the engine.
/// </summary>
public sealed class Job
{
    /// <summary>
    /// Gets the unique identifier of the job.
    /// </summary>
    public Guid Id { get; }

    /// <summary>
    /// Gets the user-defined strongly-typed job payload.
    /// </summary>
    public IJob Payload { get; }

    /// <summary>
    /// Gets the job type name derived from the payload class type.
    /// </summary>
    public string Type => Payload.GetType().Name;

    /// <summary>
    /// Gets or sets the scheduling priority of the job.
    /// </summary>
    public JobPriority Priority { get; set; }

    /// <summary>
    /// Gets the timestamp when the job was submitted.
    /// </summary>
    public DateTimeOffset CreatedAt { get; }

    /// <summary>
    /// Gets or sets the current lifecycle status of the job.
    /// </summary>
    public JobStatus Status { get; set; }

    /// <summary>
    /// Gets or sets the number of attempts executed for this job.
    /// </summary>
    public int AttemptCount { get; set; }

    /// <summary>
    /// Gets or sets the timestamp when execution of the first attempt started.
    /// </summary>
    public DateTimeOffset? StartedAt { get; set; }

    /// <summary>
    /// Gets or sets the timestamp when execution completed (successfully or failed).
    /// </summary>
    public DateTimeOffset? CompletedAt { get; set; }

    /// <summary>
    /// Gets or sets the failure reason if execution failed.
    /// </summary>
    public FailureReason? FailureReason { get; set; }

    /// <summary>
    /// Gets or sets the specific retry options for this job.
    /// </summary>
    public RetryOptions? RetryOptions { get; set; }

    /// <summary>
    /// Gets or sets the execution timeout limit for this job.
    /// </summary>
    public TimeSpan? Timeout { get; set; }

    /// <summary>
    /// Gets custom metadata dictionary associated with the job.
    /// </summary>
    public Dictionary<string, string> Metadata { get; } = new();

    /// <summary>
    /// Initializes a new instance of the <see cref="Job"/> class.
    /// </summary>
    public Job(Guid id, IJob payload, JobPriority priority, DateTimeOffset createdAt, TimeSpan? timeout = null, RetryOptions? retryOptions = null)
    {
        Id = id;
        Payload = payload;
        Priority = priority;
        CreatedAt = createdAt;
        Status = JobStatus.Submitted;
        Timeout = timeout;
        RetryOptions = retryOptions;
    }
}

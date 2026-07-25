using System;
using ConcurrentJobEngine.Core.Enums;

namespace ConcurrentJobEngine.Core.Models;

/// <summary>
/// Configuration options specified when submitting a job.
/// </summary>
public sealed class JobOptions
{
    /// <summary>
    /// Gets or sets the priority of the job. Defaults to Normal.
    /// </summary>
    public JobPriority Priority { get; set; } = JobPriority.Normal;

    /// <summary>
    /// Gets or sets the timeout duration for job execution. If null, no timeout is enforced at job level.
    /// </summary>
    public TimeSpan? Timeout { get; set; }

    /// <summary>
    /// Gets or sets the custom retry configurations for this job. If null, engine default retry configuration applies.
    /// </summary>
    public RetryOptions? Retry { get; set; }
}

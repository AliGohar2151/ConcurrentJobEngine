using System;

namespace ConcurrentJobEngine.Core.Models;

/// <summary>
/// Configuration options for the concurrent job execution engine.
/// </summary>
public sealed class ConcurrentJobEngineOptions
{
    /// <summary>
    /// Gets or sets the number of concurrent worker loops. Defaults to Environment.ProcessorCount.
    /// </summary>
    public int WorkerCount { get; set; } = Environment.ProcessorCount;

    /// <summary>
    /// Gets or sets the maximum duration to wait for running jobs to complete during stop/shutdown.
    /// </summary>
    public TimeSpan ShutdownTimeout { get; set; } = TimeSpan.FromSeconds(5);

    /// <summary>
    /// Gets or sets the maximum allowed capacity of active (queued or running) jobs. Submissions exceeding this limit are rejected.
    /// </summary>
    public int MaxQueueLimit { get; set; } = 1000;

    /// <summary>
    /// Gets or sets the default timeout duration for jobs if not overridden at the job level. Null means no timeout.
    /// </summary>
    public TimeSpan? DefaultJobTimeout { get; set; }

    /// <summary>
    /// Gets or sets the default retry options if not overridden at the job level.
    /// </summary>
    public RetryOptions DefaultRetryOptions { get; set; } = new() { MaxAttempts = 3 };
}

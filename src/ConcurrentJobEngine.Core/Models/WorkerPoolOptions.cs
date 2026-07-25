using System;

namespace ConcurrentJobEngine.Core.Models;

/// <summary>
/// Configurations for the concurrent worker pool.
/// </summary>
public sealed class WorkerPoolOptions
{
    /// <summary>
    /// Gets or sets the number of concurrent worker loops. Defaults to Environment.ProcessorCount.
    /// </summary>
    public int WorkerCount { get; set; } = Environment.ProcessorCount;

    /// <summary>
    /// Gets or sets the maximum duration to wait for running jobs to complete during stop/shutdown.
    /// </summary>
    public TimeSpan ShutdownTimeout { get; set; } = TimeSpan.FromSeconds(5);
}

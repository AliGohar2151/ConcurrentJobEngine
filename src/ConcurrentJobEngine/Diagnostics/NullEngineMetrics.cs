using ConcurrentJobEngine.Core.Abstractions;

namespace ConcurrentJobEngine.Diagnostics;

/// <summary>
/// A no-op implementation of <see cref="IEngineMetrics"/> for use when metrics recording is disabled.
/// </summary>
public sealed class NullEngineMetrics : IEngineMetrics
{
    /// <summary>
    /// Gets a singleton instance of <see cref="NullEngineMetrics"/>.
    /// </summary>
    public static NullEngineMetrics Instance { get; } = new();

    /// <inheritdoc/>
    public void RecordJobSubmitted() { }

    /// <inheritdoc/>
    public void RecordJobCompleted(double executionDurationSeconds) { }

    /// <inheritdoc/>
    public void RecordJobFailed() { }

    /// <inheritdoc/>
    public void RecordJobRetried() { }

    /// <inheritdoc/>
    public void RecordJobCancelled() { }

    /// <inheritdoc/>
    public void RecordJobTimedOut() { }

    /// <inheritdoc/>
    public void RecordJobDeadLettered() { }

    /// <inheritdoc/>
    public void RecordJobDequeued(double queueWaitDurationSeconds) { }

    /// <inheritdoc/>
    public void IncrementActiveJobs() { }

    /// <inheritdoc/>
    public void DecrementActiveJobs() { }

    /// <inheritdoc/>
    public void IncrementQueueDepth() { }

    /// <inheritdoc/>
    public void DecrementQueueDepth() { }
}

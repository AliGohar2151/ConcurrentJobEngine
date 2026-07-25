using System.Diagnostics.Metrics;
using ConcurrentJobEngine.Core.Abstractions;

namespace ConcurrentJobEngine.Diagnostics;

/// <summary>
/// Implements <see cref="IEngineMetrics"/> using <see cref="Meter"/> from <c>System.Diagnostics.Metrics</c>.
/// </summary>
public sealed class EngineMetrics : IEngineMetrics
{
    /// <summary>
    /// The meter name used for all ConcurrentJobEngine diagnostic instruments.
    /// </summary>
    public const string MeterName = "ConcurrentJobEngine";

    private readonly Meter _meter;

    private readonly Counter<long> _jobsSubmitted;
    private readonly Counter<long> _jobsCompleted;
    private readonly Counter<long> _jobsFailed;
    private readonly Counter<long> _jobsRetried;
    private readonly Counter<long> _jobsCancelled;
    private readonly Counter<long> _jobsTimedOut;
    private readonly Counter<long> _jobsDeadLettered;

    private readonly UpDownCounter<long> _jobsActive;
    private readonly UpDownCounter<long> _queueDepth;

    private readonly Histogram<double> _queueDuration;
    private readonly Histogram<double> _executionDuration;

    /// <summary>
    /// Initializes a new instance of the <see cref="EngineMetrics"/> class using the default meter.
    /// </summary>
    public EngineMetrics() : this(new Meter(MeterName, "1.0.0"))
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="EngineMetrics"/> class with a custom <see cref="Meter"/>.
    /// </summary>
    /// <param name="meter">The meter instance.</param>
    public EngineMetrics(Meter meter)
    {
        _meter = meter;

        _jobsSubmitted = _meter.CreateCounter<long>("jobs.submitted", description: "Total jobs submitted to engine");
        _jobsCompleted = _meter.CreateCounter<long>("jobs.completed", description: "Total jobs completed successfully");
        _jobsFailed = _meter.CreateCounter<long>("jobs.failed", description: "Total job execution failures");
        _jobsRetried = _meter.CreateCounter<long>("jobs.retried", description: "Total job retries scheduled");
        _jobsCancelled = _meter.CreateCounter<long>("jobs.cancelled", description: "Total jobs cancelled");
        _jobsTimedOut = _meter.CreateCounter<long>("jobs.timed_out", description: "Total jobs timed out");
        _jobsDeadLettered = _meter.CreateCounter<long>("jobs.dead_lettered", description: "Total jobs routed to dead-letter store");

        _jobsActive = _meter.CreateUpDownCounter<long>("jobs.active", description: "Number of active jobs");
        _queueDepth = _meter.CreateUpDownCounter<long>("queue.depth", description: "Number of jobs in queue waiting for execution");

        _queueDuration = _meter.CreateHistogram<double>("job.queue_duration", unit: "s", description: "Time spent in queue waiting for worker execution");
        _executionDuration = _meter.CreateHistogram<double>("job.execution_duration", unit: "s", description: "Time spent executing job handler");
    }

    /// <inheritdoc/>
    public void RecordJobSubmitted() => _jobsSubmitted.Add(1);

    /// <inheritdoc/>
    public void RecordJobCompleted(double executionDurationSeconds)
    {
        _jobsCompleted.Add(1);
        if (executionDurationSeconds >= 0)
        {
            _executionDuration.Record(executionDurationSeconds);
        }
    }

    /// <inheritdoc/>
    public void RecordJobFailed() => _jobsFailed.Add(1);

    /// <inheritdoc/>
    public void RecordJobRetried() => _jobsRetried.Add(1);

    /// <inheritdoc/>
    public void RecordJobCancelled() => _jobsCancelled.Add(1);

    /// <inheritdoc/>
    public void RecordJobTimedOut() => _jobsTimedOut.Add(1);

    /// <inheritdoc/>
    public void RecordJobDeadLettered() => _jobsDeadLettered.Add(1);

    /// <inheritdoc/>
    public void RecordJobDequeued(double queueWaitDurationSeconds)
    {
        if (queueWaitDurationSeconds >= 0)
        {
            _queueDuration.Record(queueWaitDurationSeconds);
        }
    }

    /// <inheritdoc/>
    public void IncrementActiveJobs() => _jobsActive.Add(1);

    /// <inheritdoc/>
    public void DecrementActiveJobs() => _jobsActive.Add(-1);

    /// <inheritdoc/>
    public void IncrementQueueDepth() => _queueDepth.Add(1);

    /// <inheritdoc/>
    public void DecrementQueueDepth() => _queueDepth.Add(-1);
}

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ConcurrentJobEngine.Core.Abstractions;
using ConcurrentJobEngine.Core.Enums;
using ConcurrentJobEngine.Core.Exceptions;
using ConcurrentJobEngine.Core.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ConcurrentJobEngine.Execution;

/// <summary>
/// Coordinates the intake/submission of jobs, scheduling operations, and client status queries.
/// </summary>
public sealed class JobProcessor : IJobProcessor
{
    private readonly IJobScheduler _scheduler;
    private readonly IJobStateStore _stateStore;
    private readonly IDeadLetterStore _deadLetterStore;
    private readonly IWorkerPool _workerPool;
    private readonly IJobCancellationRegistry _cancellationRegistry;
    private readonly ConcurrentJobEngineOptions _options;
    private readonly ILogger<JobProcessor> _logger;

    private volatile bool _isShuttingDown;

    /// <summary>
    /// Initializes a new instance of the <see cref="JobProcessor"/> class.
    /// </summary>
    public JobProcessor(
        IJobScheduler scheduler,
        IJobStateStore stateStore,
        IDeadLetterStore deadLetterStore,
        IWorkerPool workerPool,
        IJobCancellationRegistry cancellationRegistry,
        IOptions<ConcurrentJobEngineOptions> options,
        ILogger<JobProcessor> logger)
    {
        _scheduler = scheduler ?? throw new ArgumentNullException(nameof(scheduler));
        _stateStore = stateStore ?? throw new ArgumentNullException(nameof(stateStore));
        _deadLetterStore = deadLetterStore ?? throw new ArgumentNullException(nameof(deadLetterStore));
        _workerPool = workerPool ?? throw new ArgumentNullException(nameof(workerPool));
        _cancellationRegistry = cancellationRegistry ?? throw new ArgumentNullException(nameof(cancellationRegistry));
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    public Task<Guid> SubmitAsync<TJob>(
        TJob job,
        CancellationToken cancellationToken = default)
        where TJob : IJob
    {
        return SubmitAsync(job, new JobOptions(), cancellationToken);
    }

    /// <inheritdoc />
    public async Task<Guid> SubmitAsync<TJob>(
        TJob job,
        JobOptions options,
        CancellationToken cancellationToken = default)
        where TJob : IJob
    {
        if (_isShuttingDown)
        {
            _logger.LogWarning("Job submission rejected. Engine is shutting down.");
            throw new JobRejectedException("Engine is shutting down. No new jobs can be submitted.");
        }

        if (job is null)
        {
            throw new ArgumentNullException(nameof(job));
        }
        if (options is null)
        {
            throw new ArgumentNullException(nameof(options));
        }

        // Enforce backpressure limits
        int activeCount = await _stateStore.GetActiveCountAsync(cancellationToken);
        if (activeCount >= _options.MaxQueueLimit)
        {
            _logger.LogWarning("Job submission rejected. Active job count ({ActiveCount}) reaches or exceeds maximum limit ({MaxLimit}).", activeCount, _options.MaxQueueLimit);
            throw new JobRejectedException($"Engine capacity limit reached. Max limit: {_options.MaxQueueLimit}");
        }

        var jobId = Guid.NewGuid();
        var jobWrapper = new Job(jobId, job, options.Priority, DateTimeOffset.UtcNow)
        {
            Timeout = options.Timeout,
            RetryOptions = options.Retry,
            Status = JobStatus.Submitted
        };

        _logger.LogInformation("Submitting job {JobId} of type {JobType}.", jobId, typeof(TJob).Name);

        // 1. Persist initial status to state store
        await _stateStore.AddOrUpdateAsync(jobWrapper, cancellationToken);

        // 2. Schedule the job
        await _scheduler.ScheduleAsync(jobWrapper, cancellationToken);

        // Update state to Queued after scheduling
        jobWrapper.Status = JobStatus.Queued;
        await _stateStore.AddOrUpdateAsync(jobWrapper, cancellationToken);

        return jobId;
    }

    /// <inheritdoc />
    public async Task<JobStatusInfo?> GetStatusAsync(
        Guid jobId,
        CancellationToken cancellationToken = default)
    {
        var job = await _stateStore.GetAsync(jobId, cancellationToken);
        if (job is null)
        {
            return null;
        }

        return new JobStatusInfo(
            job.Id,
            job.Status,
            job.CreatedAt,
            job.StartedAt,
            job.CompletedAt,
            job.AttemptCount,
            job.FailureReason);
    }

    /// <inheritdoc />
    public async Task CancelAsync(
        Guid jobId,
        CancellationToken cancellationToken = default)
    {
        var job = await _stateStore.GetAsync(jobId, cancellationToken);
        if (job is null)
        {
            throw new InvalidOperationException($"Job {jobId} not found.");
        }

        if (job.Status == JobStatus.Completed || job.Status == JobStatus.Failed || 
            job.Status == JobStatus.Cancelled || job.Status == JobStatus.TimedOut)
        {
            return; // Already in a final state
        }

        _logger.LogInformation("Requesting cancellation for job {JobId} in status {Status}.", jobId, job.Status);

        if (job.Status == JobStatus.Running)
        {
            job.Status = JobStatus.Cancelled;
            job.CompletedAt = DateTimeOffset.UtcNow;
            job.FailureReason = FailureReason.Cancelled;
            await _stateStore.AddOrUpdateAsync(job, cancellationToken);
            _cancellationRegistry.Cancel(jobId);
        }
        else
        {
            // It is Submitted or Queued. We can transition it to Cancelled directly.
            job.Status = JobStatus.Cancelled;
            job.CompletedAt = DateTimeOffset.UtcNow;
            job.FailureReason = FailureReason.Cancelled;
            await _stateStore.AddOrUpdateAsync(job, cancellationToken);
        }
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<DeadLetterRecord>> GetDeadLetterJobsAsync(
        CancellationToken cancellationToken = default)
    {
        return _deadLetterStore.GetAllAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task StopAsync(CancellationToken cancellationToken = default)
    {
        _isShuttingDown = true;
        _logger.LogInformation("Engine shutdown initiated. Draining in-flight jobs.");
        await _workerPool.StopAsync(cancellationToken);
        _logger.LogInformation("Engine shutdown complete.");
    }
}

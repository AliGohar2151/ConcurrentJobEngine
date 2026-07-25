using System;
using System.Threading;
using System.Threading.Tasks;
using ConcurrentJobEngine.Core.Abstractions;
using ConcurrentJobEngine.Core.Enums;
using ConcurrentJobEngine.Core.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ConcurrentJobEngine.Execution;

/// <summary>
/// Coordinates single job execution lifecycle transitions, manages timeouts/cancellations, and updates the state store.
/// </summary>
public sealed class JobExecutor : IJobExecutor
{
    private readonly IJobStateStore _stateStore;
    private readonly IJobScheduler _scheduler;
    private readonly IDeadLetterStore _deadLetterStore;
    private readonly IServiceProvider _serviceProvider;
    private readonly IJobCancellationRegistry _cancellationRegistry;
    private readonly ConcurrentJobEngineOptions _options;
    private readonly ILogger<JobExecutor> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="JobExecutor"/> class.
    /// </summary>
    public JobExecutor(
        IJobStateStore stateStore,
        IJobScheduler scheduler,
        IDeadLetterStore deadLetterStore,
        IServiceProvider serviceProvider,
        IJobCancellationRegistry cancellationRegistry,
        IOptions<ConcurrentJobEngineOptions> options,
        ILogger<JobExecutor> logger)
    {
        _stateStore = stateStore ?? throw new ArgumentNullException(nameof(stateStore));
        _scheduler = scheduler ?? throw new ArgumentNullException(nameof(scheduler));
        _deadLetterStore = deadLetterStore ?? throw new ArgumentNullException(nameof(deadLetterStore));
        _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
        _cancellationRegistry = cancellationRegistry ?? throw new ArgumentNullException(nameof(cancellationRegistry));
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Executes a single job, tracking lifecycle changes and handling timeouts and cancellations.
    /// </summary>
    public async Task<JobResult> ExecuteAsync(Job job, CancellationToken cancellationToken = default)
    {
        if (job is null)
        {
            throw new ArgumentNullException(nameof(job));
        }

        if (job.Status == JobStatus.Cancelled)
        {
            _logger.LogInformation("Job {JobId} is already cancelled. Skipping execution.", job.Id);
            return JobResult.Failure(FailureReason.Cancelled, "Job was cancelled before execution started.");
        }

        job.AttemptCount++;
        job.Status = JobStatus.Running;
        job.StartedAt ??= DateTimeOffset.UtcNow;
        await _stateStore.AddOrUpdateAsync(job, cancellationToken);

        var startedAt = DateTimeOffset.UtcNow;
        var context = new JobExecutionContext(job.Id, job.AttemptCount, startedAt, job.Priority);

        var timeout = job.Timeout ?? _options.DefaultJobTimeout;

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        if (timeout.HasValue && timeout.Value > TimeSpan.Zero)
        {
            cts.CancelAfter(timeout.Value);
        }

        _cancellationRegistry.Register(job.Id, cts);

        try
        {
            var wrapper = JobHandlerResolver.GetWrapper(job.Payload.GetType());
            var result = await wrapper.HandleAsync(job.Payload, context, _serviceProvider, cts.Token);

            if (result.IsSuccess)
            {
                job.Status = JobStatus.Completed;
                job.CompletedAt = DateTimeOffset.UtcNow;
                await _stateStore.AddOrUpdateAsync(job, CancellationToken.None);
                _logger.LogInformation("Job {JobId} completed successfully.", job.Id);
                return result;
            }

            var retryOpts = job.RetryOptions ?? _options.DefaultRetryOptions;
            if (job.AttemptCount < retryOpts.MaxAttempts)
            {
                job.Status = JobStatus.Failed;
                job.CompletedAt = DateTimeOffset.UtcNow;
                job.FailureReason = result.FailureReason;
                await _stateStore.AddOrUpdateAsync(job, CancellationToken.None);

                var delay = BackoffCalculator.ComputeDelay(retryOpts, job.AttemptCount);
                _logger.LogWarning("Job {JobId} failed on attempt {Attempt} with reason {Reason}. Retrying in {Delay} ms.", job.Id, job.AttemptCount, result.FailureReason, delay.TotalMilliseconds);
                if (delay > TimeSpan.Zero) await Task.Delay(delay, CancellationToken.None);

                job.Status = JobStatus.Queued;
                await _stateStore.AddOrUpdateAsync(job, CancellationToken.None);
                await _scheduler.ScheduleAsync(job, CancellationToken.None);
            }
            else
            {
                job.Status = JobStatus.Failed;
                job.CompletedAt = DateTimeOffset.UtcNow;
                job.FailureReason = result.FailureReason;
                await _stateStore.AddOrUpdateAsync(job, CancellationToken.None);
                _logger.LogError("Job {JobId} failed on attempt {Attempt} with reason {Reason}. Max attempts reached. Sending to dead-letter store.", job.Id, job.AttemptCount, result.FailureReason);
                await _deadLetterStore.AddAsync(new DeadLetterRecord(
                    job.Id, job.Payload.GetType().Name, job.Payload,
                    result.FailureReason ?? FailureReason.ExecutionFailed, result.Message,
                    job.AttemptCount, job.StartedAt ?? DateTimeOffset.UtcNow, DateTimeOffset.UtcNow), CancellationToken.None);
            }

            return result;
        }
        catch (OperationCanceledException ex) when (cts.Token.IsCancellationRequested)
        {
            if (job.Status == JobStatus.Cancelled || cancellationToken.IsCancellationRequested)
            {
                job.Status = JobStatus.Cancelled;
                job.CompletedAt = DateTimeOffset.UtcNow;
                job.FailureReason = FailureReason.Cancelled;
                await _stateStore.AddOrUpdateAsync(job, CancellationToken.None);
                _logger.LogWarning("Job {JobId} was cancelled.", job.Id);
                return JobResult.Failure(FailureReason.Cancelled, "Job execution was cancelled.", ex);
            }
            else
            {
                var retryOpts = job.RetryOptions ?? _options.DefaultRetryOptions;
                if (job.AttemptCount < retryOpts.MaxAttempts)
                {
                    job.Status = JobStatus.TimedOut;
                    job.CompletedAt = DateTimeOffset.UtcNow;
                    job.FailureReason = FailureReason.Timeout;
                    await _stateStore.AddOrUpdateAsync(job, CancellationToken.None);

                    var delay = BackoffCalculator.ComputeDelay(retryOpts, job.AttemptCount);
                    _logger.LogWarning("Job {JobId} timed out on attempt {Attempt}. Retrying in {Delay} ms.", job.Id, job.AttemptCount, delay.TotalMilliseconds);
                    if (delay > TimeSpan.Zero) await Task.Delay(delay, CancellationToken.None);

                    job.Status = JobStatus.Queued;
                    await _stateStore.AddOrUpdateAsync(job, CancellationToken.None);
                    await _scheduler.ScheduleAsync(job, CancellationToken.None);
                }
                else
                {
                    job.Status = JobStatus.TimedOut;
                    job.CompletedAt = DateTimeOffset.UtcNow;
                    job.FailureReason = FailureReason.Timeout;
                    await _stateStore.AddOrUpdateAsync(job, CancellationToken.None);
                    _logger.LogError("Job {JobId} timed out on attempt {Attempt}. Max attempts reached. Sending to dead-letter store.", job.Id, job.AttemptCount);
                    await _deadLetterStore.AddAsync(new DeadLetterRecord(
                        job.Id, job.Payload.GetType().Name, job.Payload,
                        FailureReason.Timeout, $"Job exceeded timeout limit of {job.Timeout}.",
                        job.AttemptCount, job.StartedAt ?? DateTimeOffset.UtcNow, DateTimeOffset.UtcNow), CancellationToken.None);
                }
                return JobResult.Failure(FailureReason.Timeout, $"Job exceeded timeout limit of {job.Timeout}.", ex);
            }
        }
        catch (Exception ex)
        {
            var retryOpts = job.RetryOptions ?? _options.DefaultRetryOptions;
            if (job.AttemptCount < retryOpts.MaxAttempts)
            {
                job.Status = JobStatus.Failed;
                job.CompletedAt = DateTimeOffset.UtcNow;
                job.FailureReason = FailureReason.ExecutionFailed;
                await _stateStore.AddOrUpdateAsync(job, CancellationToken.None);

                var delay = BackoffCalculator.ComputeDelay(retryOpts, job.AttemptCount);
                _logger.LogWarning("Job {JobId} encountered exception on attempt {Attempt}. Retrying in {Delay} ms.", job.Id, job.AttemptCount, delay.TotalMilliseconds);
                if (delay > TimeSpan.Zero) await Task.Delay(delay, CancellationToken.None);

                job.Status = JobStatus.Queued;
                await _stateStore.AddOrUpdateAsync(job, CancellationToken.None);
                await _scheduler.ScheduleAsync(job, CancellationToken.None);
            }
            else
            {
                job.Status = JobStatus.Failed;
                job.CompletedAt = DateTimeOffset.UtcNow;
                job.FailureReason = FailureReason.ExecutionFailed;
                await _stateStore.AddOrUpdateAsync(job, CancellationToken.None);
                _logger.LogError(ex, "Job {JobId} encountered exception on attempt {Attempt}. Max attempts reached. Sending to dead-letter store.", job.Id, job.AttemptCount);
                await _deadLetterStore.AddAsync(new DeadLetterRecord(
                    job.Id, job.Payload.GetType().Name, job.Payload,
                    FailureReason.ExecutionFailed, ex.Message,
                    job.AttemptCount, job.StartedAt ?? DateTimeOffset.UtcNow, DateTimeOffset.UtcNow), CancellationToken.None);
            }
            return JobResult.Failure(FailureReason.ExecutionFailed, ex.Message, ex);
        }
        finally
        {
            _cancellationRegistry.Unregister(job.Id);
        }
    }
}

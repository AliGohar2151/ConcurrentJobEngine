using System;
using System.Threading;
using System.Threading.Tasks;
using ConcurrentJobEngine.Core.Abstractions;
using ConcurrentJobEngine.Core.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ConcurrentJobEngine.Execution;

/// <summary>
/// Controls a collection of concurrent background Task loops executing jobs retrieved from IJobScheduler.
/// </summary>
public sealed class WorkerPool : IWorkerPool
{
    private readonly IJobScheduler _scheduler;
    private readonly IJobExecutor _executor;
    private readonly ConcurrentJobEngineOptions _options;
    private readonly ILogger<WorkerPool> _logger;

    private Task[]? _workerTasks;
    private CancellationTokenSource? _dequeueCts;
    private CancellationTokenSource? _executeCts;
    private bool _isRunning;
    private readonly object _lock = new();

    /// <inheritdoc />
    public int WorkerCount => _options.WorkerCount;

    /// <summary>
    /// Initializes a new instance of the <see cref="WorkerPool"/> class.
    /// </summary>
    public WorkerPool(
        IJobScheduler scheduler,
        IJobExecutor executor,
        IOptions<ConcurrentJobEngineOptions> options,
        ILogger<WorkerPool> logger)
    {
        _scheduler = scheduler ?? throw new ArgumentNullException(nameof(scheduler));
        _executor = executor ?? throw new ArgumentNullException(nameof(executor));
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    public Task StartAsync(CancellationToken cancellationToken = default)
    {
        lock (_lock)
        {
            if (_isRunning)
            {
                throw new InvalidOperationException("Worker pool is already running.");
            }

            _logger.LogInformation("Starting worker pool with {WorkerCount} workers.", WorkerCount);
            
            _dequeueCts = new CancellationTokenSource();
            _executeCts = new CancellationTokenSource();
            _workerTasks = new Task[WorkerCount];

            for (int i = 0; i < WorkerCount; i++)
            {
                int workerId = i;
                _workerTasks[i] = Task.Run(() => WorkerLoopAsync(workerId, _dequeueCts.Token), CancellationToken.None);
            }

            _isRunning = true;
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public async Task StopAsync(CancellationToken cancellationToken = default)
    {
        Task[] tasksToAwait;
        CancellationTokenSource dequeueCts;
        CancellationTokenSource executeCts;

        lock (_lock)
        {
            if (!_isRunning)
            {
                return;
            }

            _logger.LogInformation("Stopping worker pool gracefully.");
            _isRunning = false;

            tasksToAwait = _workerTasks!;
            dequeueCts = _dequeueCts!;
            executeCts = _executeCts!;

            _workerTasks = null;
            _dequeueCts = null;
            _executeCts = null;
        }

        // 1. Stop workers from dequeuing new jobs immediately
        dequeueCts.Cancel();

        // 2. Setup execution timeout for currently running jobs
        using var timeoutCts = new CancellationTokenSource(_options.ShutdownTimeout);
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(timeoutCts.Token, cancellationToken);

        // Cancel execution CTS when linked CTS is cancelled
        using var registration = linkedCts.Token.Register(() => executeCts.Cancel());

        try
        {
            // 3. Wait for workers to finish current execution loops
            await Task.WhenAll(tasksToAwait);
            _logger.LogInformation("Worker pool stopped successfully.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Worker pool stop completed with errors.");
        }
        finally
        {
            dequeueCts.Dispose();
            executeCts.Dispose();
        }
    }

    private async Task WorkerLoopAsync(int workerId, CancellationToken dequeueToken)
    {
        _logger.LogInformation("Worker {WorkerId} loop started.", workerId);

        while (!dequeueToken.IsCancellationRequested)
        {
            Job job;
            try
            {
                // Dequeue a job. If cancelled, it throws OperationCanceledException and exits loop.
                job = await _scheduler.GetNextJobAsync(dequeueToken);
            }
            catch (OperationCanceledException) when (dequeueToken.IsCancellationRequested)
            {
                // Dequeue token cancelled for shutdown: drain any remaining queued jobs before loop exit
                while (_scheduler is PriorityJobScheduler priorityScheduler && priorityScheduler.TryGetNextJob(out var drainedJob) && drainedJob != null)
                {
                    try
                    {
                        await _executor.ExecuteAsync(drainedJob, _executeCts?.Token ?? CancellationToken.None);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Worker {WorkerId} encountered unhandled error executing drained job {JobId}.", workerId, drainedJob.Id);
                    }
                }
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Worker {WorkerId} failed to retrieve next job.", workerId);
                // Simple delay to prevent tight loop on persistent failures
                try
                {
                    await Task.Delay(100, dequeueToken);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                continue;
            }

            _logger.LogInformation("Worker {WorkerId} picked up job {JobId}.", workerId, job.Id);

            try
            {
                // Execute job using execution cancellation token (which supports shutdown timeout)
                await _executor.ExecuteAsync(job, _executeCts?.Token ?? CancellationToken.None);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Worker {WorkerId} encountered unhandled error executing job {JobId}.", workerId, job.Id);
            }
        }

        _logger.LogInformation("Worker {WorkerId} loop exited.", workerId);
    }
}

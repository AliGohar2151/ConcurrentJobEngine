using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ConcurrentJobEngine.Core.Abstractions;
using ConcurrentJobEngine.Core.Enums;
using ConcurrentJobEngine.Core.Models;
using ConcurrentJobEngine.Execution;
using ConcurrentJobEngine.Queues;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Xunit;

namespace ConcurrentJobEngine.UnitTests;

/// <summary>
/// Unit tests verifying WorkerPool concurrency, lifecycle, and graceful shutdown timeouts.
/// </summary>
public class WorkerPoolTests
{
    private sealed class FakeJobScheduler : IJobScheduler
    {
        public readonly IJobQueue<Job> Queue = new InMemoryJobQueue<Job>();

        public Task ScheduleAsync(Job job, CancellationToken cancellationToken = default)
        {
            Queue.TryEnqueue(job);
            return Task.CompletedTask;
        }

        public async Task<Job> GetNextJobAsync(CancellationToken cancellationToken = default)
        {
            return await Queue.DequeueAsync(cancellationToken);
        }
    }

    private sealed class FakeJobExecutor : IJobExecutor
    {
        public readonly ConcurrentBag<Guid> ExecutedJobIds = new();
        public readonly ConcurrentDictionary<Guid, TaskCompletionSource<bool>> RunningJobs = new();

        public async Task<JobResult> ExecuteAsync(Job job, CancellationToken cancellationToken = default)
        {
            var tcs = RunningJobs.GetOrAdd(job.Id, _ => new TaskCompletionSource<bool>());
            
            // Wait for completion or cancellation
            using var registration = cancellationToken.Register(() => tcs.TrySetCanceled(cancellationToken));
            
            try
            {
                await tcs.Task;
                ExecutedJobIds.Add(job.Id);
                job.Status = JobStatus.Completed;
                return JobResult.Success();
            }
            catch (OperationCanceledException ex)
            {
                job.Status = JobStatus.Cancelled;
                return JobResult.Failure(FailureReason.Cancelled, "Cancelled", ex);
            }
        }
    }

    private readonly FakeJobScheduler _scheduler = new();
    private readonly FakeJobExecutor _executor = new();
    private readonly ILogger<WorkerPool> _logger;

    public WorkerPoolTests()
    {
        using var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
        _logger = loggerFactory.CreateLogger<WorkerPool>();
    }

    private IOptions<ConcurrentJobEngineOptions> CreateOptions(int workerCount, TimeSpan shutdownTimeout)
    {
        return Options.Create(new ConcurrentJobEngineOptions
        {
            WorkerCount = workerCount,
            ShutdownTimeout = shutdownTimeout
        });
    }

    [Fact]
    public async Task WorkerPool_Lifecycle_StartsAndStops()
    {
        var pool = new WorkerPool(_scheduler, _executor, CreateOptions(2, TimeSpan.FromSeconds(1)), _logger);

        await pool.StartAsync();
        Assert.Equal(2, pool.WorkerCount);

        await pool.StopAsync();
    }

    [Fact]
    public async Task WorkerPool_ExecutesJobsConcurrently()
    {
        var pool = new WorkerPool(_scheduler, _executor, CreateOptions(3, TimeSpan.FromSeconds(2)), _logger);
        await pool.StartAsync();

        var job1 = new Job(Guid.NewGuid(), new SuccessJobPayload(), JobPriority.Normal, DateTimeOffset.UtcNow);
        var job2 = new Job(Guid.NewGuid(), new SuccessJobPayload(), JobPriority.Normal, DateTimeOffset.UtcNow);
        var job3 = new Job(Guid.NewGuid(), new SuccessJobPayload(), JobPriority.Normal, DateTimeOffset.UtcNow);

        await _scheduler.ScheduleAsync(job1);
        await _scheduler.ScheduleAsync(job2);
        await _scheduler.ScheduleAsync(job3);

        // Wait for workers to pick up the jobs
        while (_executor.RunningJobs.Count < 3)
        {
            await Task.Delay(10);
        }

        // Unblock them
        foreach (var tcs in _executor.RunningJobs.Values)
        {
            tcs.TrySetResult(true);
        }

        // Wait for executions to complete
        while (_executor.ExecutedJobIds.Count < 3)
        {
            await Task.Delay(10);
        }

        Assert.Contains(job1.Id, _executor.ExecutedJobIds);
        Assert.Contains(job2.Id, _executor.ExecutedJobIds);
        Assert.Contains(job3.Id, _executor.ExecutedJobIds);

        await pool.StopAsync();
    }

    [Fact]
    public async Task WorkerPool_GracefulShutdown_AllowsRunningJobsToComplete()
    {
        var pool = new WorkerPool(_scheduler, _executor, CreateOptions(1, TimeSpan.FromSeconds(10)), _logger);
        await pool.StartAsync();

        var runningJob = new Job(Guid.NewGuid(), new SuccessJobPayload(), JobPriority.Normal, DateTimeOffset.UtcNow);
        var pendingJob = new Job(Guid.NewGuid(), new SuccessJobPayload(), JobPriority.Normal, DateTimeOffset.UtcNow);

        await _scheduler.ScheduleAsync(runningJob);

        // Wait for workers to pick up the first job
        while (!_executor.RunningJobs.TryGetValue(runningJob.Id, out _))
        {
            await Task.Delay(10);
        }

        // Schedule the pending job
        await _scheduler.ScheduleAsync(pendingJob);

        // Now initiate stop. It should stop taking new jobs (pendingJob should not start).
        var stopTask = Task.Run(async () => await pool.StopAsync());

        // Give it a moment to call StopAsync and cancel dequeue token
        await Task.Delay(50);

        // Unblock running job
        _executor.RunningJobs[runningJob.Id].TrySetResult(true);

        // Wait for StopAsync to finish
        await stopTask;

        // Verify runningJob was executed
        Assert.Contains(runningJob.Id, _executor.ExecutedJobIds);
        
        // Verify pendingJob was NOT executed (remains in queue/scheduler)
        Assert.DoesNotContain(pendingJob.Id, _executor.ExecutedJobIds);
        Assert.Equal(1, _scheduler.Queue.Count);
    }

    [Fact]
    public async Task WorkerPool_ShutdownTimeout_CancelsExecutingJobs()
    {
        // 200ms shutdown timeout
        var pool = new WorkerPool(_scheduler, _executor, CreateOptions(1, TimeSpan.FromMilliseconds(200)), _logger);
        await pool.StartAsync();

        var job = new Job(Guid.NewGuid(), new SuccessJobPayload(), JobPriority.Normal, DateTimeOffset.UtcNow);
        await _scheduler.ScheduleAsync(job);

        // Wait for worker to pick up the job
        while (!_executor.RunningJobs.TryGetValue(job.Id, out _))
        {
            await Task.Delay(10);
        }

        // Call stop. It should cancel the job after 200ms.
        var startTime = DateTimeOffset.UtcNow;
        await pool.StopAsync();
        var duration = DateTimeOffset.UtcNow - startTime;

        // Verify it waited for about 200ms
        Assert.True(duration >= TimeSpan.FromMilliseconds(150), $"Should wait for timeout, took {duration.TotalMilliseconds}ms");

        // Verify job was canceled
        Assert.Equal(JobStatus.Cancelled, job.Status);
    }

    private sealed record SuccessJobPayload : IJob;
}

using System;
using System.Threading;
using System.Threading.Tasks;
using ConcurrentJobEngine.Core.Abstractions;
using ConcurrentJobEngine.Core.Enums;
using ConcurrentJobEngine.Core.Models;
using ConcurrentJobEngine.DependencyInjection;
using ConcurrentJobEngine.Execution;
using ConcurrentJobEngine.Storage;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Xunit;

namespace ConcurrentJobEngine.UnitTests;

/// <summary>
/// Unit tests verifying execution failure retries and rescheduling bounds.
/// </summary>
public class JobRetryTests
{
    private sealed record TestFailJob : IJob;

    private sealed class FailJobHandler : IJobHandler<TestFailJob>
    {
        public Task<JobResult> HandleAsync(TestFailJob job, JobExecutionContext context, CancellationToken cancellationToken)
        {
            return Task.FromResult(JobResult.Failure(FailureReason.ExecutionFailed, "Transient error"));
        }
    }

    private readonly InMemoryJobStateStore _stateStore = new();
    private readonly PriorityJobScheduler _scheduler = new();
    private readonly JobCancellationRegistry _cancellationRegistry = new();
    private readonly IDeadLetterStore _deadLetterStore = new InMemoryDeadLetterStore();
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<JobExecutor> _logger;

    public JobRetryTests()
    {
        var services = new ServiceCollection();
        services.AddJobHandler<TestFailJob, FailJobHandler>();
        _serviceProvider = services.BuildServiceProvider();

        using var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
        _logger = loggerFactory.CreateLogger<JobExecutor>();
    }

    [Fact]
    public async Task ExecuteAsync_ReschedulesJob_OnFailure_WhenAttemptsRemaining()
    {
        var options = Options.Create(new ConcurrentJobEngineOptions
        {
            DefaultRetryOptions = new RetryOptions { MaxAttempts = 3 }
        });

        var executor = new JobExecutor(_stateStore, _scheduler, _deadLetterStore, _serviceProvider, _cancellationRegistry, options, _logger);
        
        var job = new Job(Guid.NewGuid(), new TestFailJob(), JobPriority.Normal, DateTimeOffset.UtcNow)
        {
            RetryOptions = new RetryOptions { MaxAttempts = 3 }
        };

        // Transition: Submitted -> Queued
        await _stateStore.AddOrUpdateAsync(job);
        job.Status = JobStatus.Queued;
        await _stateStore.AddOrUpdateAsync(job);

        // First attempt (AttemptCount goes 0 -> 1)
        var result = await executor.ExecuteAsync(job, CancellationToken.None);

        Assert.False(result.IsSuccess);
        
        // After first attempt failed, it should be rescheduled (Status back to Queued)
        var storedJob = await _stateStore.GetAsync(job.Id);
        Assert.NotNull(storedJob);
        Assert.Equal(JobStatus.Queued, storedJob.Status);
        Assert.Equal(1, storedJob.AttemptCount);

        // Verify scheduled back to the queue
        var rescheduledJob = await _scheduler.GetNextJobAsync();
        Assert.Equal(job.Id, rescheduledJob.Id);
    }

    [Fact]
    public async Task ExecuteAsync_SetsFinalFailureStatus_WhenMaxAttemptsReached()
    {
        var options = Options.Create(new ConcurrentJobEngineOptions
        {
            DefaultRetryOptions = new RetryOptions { MaxAttempts = 2 }
        });

        var executor = new JobExecutor(_stateStore, _scheduler, _deadLetterStore, _serviceProvider, _cancellationRegistry, options, _logger);
        
        var job = new Job(Guid.NewGuid(), new TestFailJob(), JobPriority.Normal, DateTimeOffset.UtcNow)
        {
            RetryOptions = new RetryOptions { MaxAttempts = 2 }
        };

        // Transition: Submitted -> Queued
        await _stateStore.AddOrUpdateAsync(job);
        job.Status = JobStatus.Queued;
        await _stateStore.AddOrUpdateAsync(job);

        // First attempt (fails, rescheduled)
        var r1 = await executor.ExecuteAsync(job, CancellationToken.None);
        Assert.Equal(JobStatus.Queued, job.Status);
        
        // Dequeue for second attempt
        var rescheduledJob = await _scheduler.GetNextJobAsync();
        rescheduledJob.Status = JobStatus.Queued; // Transitioned correctly in state store by scheduler flow simulator
        
        // Second attempt (fails, final)
        var r2 = await executor.ExecuteAsync(rescheduledJob, CancellationToken.None);

        Assert.False(r2.IsSuccess);
        Assert.Equal(JobStatus.Failed, rescheduledJob.Status);
        Assert.Equal(2, rescheduledJob.AttemptCount);

        // Verify NOT scheduled again
        var nextJobTask = Task.Run(async () => await _scheduler.GetNextJobAsync());
        await Task.Delay(50);
        Assert.False(nextJobTask.IsCompleted); // Scheduler is empty/blocks
    }
}

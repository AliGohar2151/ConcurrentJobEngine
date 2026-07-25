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
/// Unit tests verifying dead-letter store population after retry exhaustion.
/// </summary>
public class JobDeadLetterTests
{
    private sealed record TestDeadLetterJob : IJob;

    private sealed class AlwaysFailHandler : IJobHandler<TestDeadLetterJob>
    {
        public Task<JobResult> HandleAsync(TestDeadLetterJob job, JobExecutionContext context, CancellationToken cancellationToken)
            => Task.FromResult(JobResult.Failure(FailureReason.ExecutionFailed, "Permanent error"));
    }

    private readonly InMemoryJobStateStore _stateStore = new();
    private readonly PriorityJobScheduler _scheduler = new();
    private readonly JobCancellationRegistry _cancellationRegistry = new();
    private readonly InMemoryDeadLetterStore _deadLetterStore = new();
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<JobExecutor> _logger;

    public JobDeadLetterTests()
    {
        var services = new ServiceCollection();
        services.AddJobHandler<TestDeadLetterJob, AlwaysFailHandler>();
        _serviceProvider = services.BuildServiceProvider();

        using var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
        _logger = loggerFactory.CreateLogger<JobExecutor>();
    }

    [Fact]
    public async Task ExecuteAsync_OnFinalFailure_AddsJobToDeadLetterStore()
    {
        var options = Options.Create(new ConcurrentJobEngineOptions
        {
            DefaultRetryOptions = new RetryOptions { MaxAttempts = 1 }
        });

        var executor = new JobExecutor(_stateStore, _scheduler, _deadLetterStore, _serviceProvider, _cancellationRegistry, options, _logger);

        var job = new Job(Guid.NewGuid(), new TestDeadLetterJob(), JobPriority.Normal, DateTimeOffset.UtcNow)
        {
            RetryOptions = new RetryOptions { MaxAttempts = 1 }
        };

        await _stateStore.AddOrUpdateAsync(job);
        job.Status = JobStatus.Queued;
        await _stateStore.AddOrUpdateAsync(job);

        // Single attempt then final failure
        var result = await executor.ExecuteAsync(job, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(JobStatus.Failed, job.Status);

        // Dead-letter store should have a record
        var record = await _deadLetterStore.GetAsync(job.Id);
        Assert.NotNull(record);
        Assert.Equal(job.Id, record.JobId);
        Assert.Equal(FailureReason.ExecutionFailed, record.FailureReason);
        Assert.Equal(1, record.AttemptCount);
    }

    [Fact]
    public async Task ExecuteAsync_RescheduledJob_NotInDeadLetterStore_UntilExhausted()
    {
        var options = Options.Create(new ConcurrentJobEngineOptions
        {
            DefaultRetryOptions = new RetryOptions { MaxAttempts = 2 }
        });

        var executor = new JobExecutor(_stateStore, _scheduler, _deadLetterStore, _serviceProvider, _cancellationRegistry, options, _logger);

        var job = new Job(Guid.NewGuid(), new TestDeadLetterJob(), JobPriority.Normal, DateTimeOffset.UtcNow)
        {
            RetryOptions = new RetryOptions { MaxAttempts = 2 }
        };

        await _stateStore.AddOrUpdateAsync(job);
        job.Status = JobStatus.Queued;
        await _stateStore.AddOrUpdateAsync(job);

        // First attempt: should reschedule, NOT dead-letter
        await executor.ExecuteAsync(job, CancellationToken.None);

        var recordAfterFirst = await _deadLetterStore.GetAsync(job.Id);
        Assert.Null(recordAfterFirst);

        // Second attempt: max attempts reached, dead-lettered
        var rescheduledJob = await _scheduler.GetNextJobAsync();
        rescheduledJob.Status = JobStatus.Queued;

        await executor.ExecuteAsync(rescheduledJob, CancellationToken.None);

        var recordAfterSecond = await _deadLetterStore.GetAsync(rescheduledJob.Id);
        Assert.NotNull(recordAfterSecond);
        Assert.Equal(2, recordAfterSecond.AttemptCount);
    }

    [Fact]
    public async Task GetAllAsync_ReturnsAllDeadLetteredJobs()
    {
        var options = Options.Create(new ConcurrentJobEngineOptions
        {
            DefaultRetryOptions = new RetryOptions { MaxAttempts = 1 }
        });

        var executor = new JobExecutor(_stateStore, _scheduler, _deadLetterStore, _serviceProvider, _cancellationRegistry, options, _logger);

        // Execute two failing jobs
        for (var i = 0; i < 2; i++)
        {
            var job = new Job(Guid.NewGuid(), new TestDeadLetterJob(), JobPriority.Normal, DateTimeOffset.UtcNow)
            {
                RetryOptions = new RetryOptions { MaxAttempts = 1 }
            };

            await _stateStore.AddOrUpdateAsync(job);
            job.Status = JobStatus.Queued;
            await _stateStore.AddOrUpdateAsync(job);
            await executor.ExecuteAsync(job, CancellationToken.None);
        }

        var allRecords = await _deadLetterStore.GetAllAsync();
        Assert.Equal(2, allRecords.Count);
    }
}

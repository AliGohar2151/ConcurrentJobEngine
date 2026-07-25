using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using ConcurrentJobEngine.Core.Abstractions;
using ConcurrentJobEngine.Core.Enums;
using ConcurrentJobEngine.Core.Models;
using ConcurrentJobEngine.DependencyInjection;
using ConcurrentJobEngine.Execution;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xunit;

using Microsoft.Extensions.Options;

using ConcurrentJobEngine.Storage;

namespace ConcurrentJobEngine.UnitTests;

/// <summary>
/// Unit tests for JobExecutor checking execution pipeline, timeout, cancellation, and state storage.
/// </summary>
public class JobExecutorTests
{
    private sealed class FakeJobStateStore : IJobStateStore
    {
        public readonly ConcurrentDictionary<Guid, Job> States = new();

        public Task AddOrUpdateAsync(Job job, CancellationToken cancellationToken = default)
        {
            States[job.Id] = job;
            return Task.CompletedTask;
        }

        public Task<Job?> GetAsync(Guid jobId, CancellationToken cancellationToken = default)
        {
            States.TryGetValue(jobId, out var job);
            return Task.FromResult(job);
        }

        public Task RemoveAsync(Guid jobId, CancellationToken cancellationToken = default)
        {
            States.TryRemove(jobId, out _);
            return Task.CompletedTask;
        }

        public Task<int> GetActiveCountAsync(CancellationToken cancellationToken = default)
        {
            int count = 0;
            foreach (var job in States.Values)
            {
                if (job.Status == JobStatus.Submitted || job.Status == JobStatus.Queued || job.Status == JobStatus.Running)
                {
                    count++;
                }
            }
            return Task.FromResult(count);
        }
    }

    private sealed record SuccessJob : IJob;

    private sealed class SuccessJobHandler : IJobHandler<SuccessJob>
    {
        public Task<JobResult> HandleAsync(SuccessJob job, JobExecutionContext context, CancellationToken cancellationToken)
        {
            return Task.FromResult(JobResult.Success());
        }
    }

    private sealed record FailJob : IJob;

    private sealed class FailJobHandler : IJobHandler<FailJob>
    {
        public Task<JobResult> HandleAsync(FailJob job, JobExecutionContext context, CancellationToken cancellationToken)
        {
            return Task.FromResult(JobResult.Failure(FailureReason.ExecutionFailed, "Something went wrong"));
        }
    }

    private sealed record TimeoutJob : IJob;

    private sealed class TimeoutJobHandler : IJobHandler<TimeoutJob>
    {
        public async Task<JobResult> HandleAsync(TimeoutJob job, JobExecutionContext context, CancellationToken cancellationToken)
        {
            await Task.Delay(1000, cancellationToken);
            return JobResult.Success();
        }
    }

    private sealed class FakeJobScheduler : IJobScheduler
    {
        public Task ScheduleAsync(Job job, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<Job> GetNextJobAsync(CancellationToken cancellationToken = default) => Task.FromResult<Job>(null!);
    }

    private readonly FakeJobStateStore _stateStore = new();
    private readonly IJobScheduler _scheduler = new FakeJobScheduler();
    private readonly IJobCancellationRegistry _cancellationRegistry = new JobCancellationRegistry();
    private readonly IDeadLetterStore _deadLetterStore = new InMemoryDeadLetterStore();
    private readonly IOptions<ConcurrentJobEngineOptions> _options = Options.Create(new ConcurrentJobEngineOptions());
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<JobExecutor> _logger;

    public JobExecutorTests()
    {
        var services = new ServiceCollection();
        services.AddJobHandler<SuccessJob, SuccessJobHandler>();
        services.AddJobHandler<FailJob, FailJobHandler>();
        services.AddJobHandler<TimeoutJob, TimeoutJobHandler>();
        _serviceProvider = services.BuildServiceProvider();

        using var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
        _logger = loggerFactory.CreateLogger<JobExecutor>();
    }

    [Fact]
    public async Task ExecuteAsync_WhenSuccessful_SetsCompletedStatus()
    {
        var executor = new JobExecutor(_stateStore, _scheduler, _deadLetterStore, _serviceProvider, _cancellationRegistry, _options, _logger);
        var jobPayload = new SuccessJob();
        var job = new Job(Guid.NewGuid(), jobPayload, JobPriority.Normal, DateTimeOffset.UtcNow);

        var result = await executor.ExecuteAsync(job, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(JobStatus.Completed, job.Status);
        Assert.Equal(1, job.AttemptCount);
        
        var storedJob = await _stateStore.GetAsync(job.Id);
        Assert.NotNull(storedJob);
        Assert.Equal(JobStatus.Completed, storedJob.Status);
    }

    [Fact]
    public async Task ExecuteAsync_WhenHandlerFails_SetsFailedStatus()
    {
        var executor = new JobExecutor(_stateStore, _scheduler, _deadLetterStore, _serviceProvider, _cancellationRegistry, _options, _logger);
        var jobPayload = new FailJob();
        var job = new Job(Guid.NewGuid(), jobPayload, JobPriority.Normal, DateTimeOffset.UtcNow)
        {
            RetryOptions = new RetryOptions { MaxAttempts = 1 }
        };

        var result = await executor.ExecuteAsync(job, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(FailureReason.ExecutionFailed, result.FailureReason);
        Assert.Equal(JobStatus.Failed, job.Status);
        
        var storedJob = await _stateStore.GetAsync(job.Id);
        Assert.NotNull(storedJob);
        Assert.Equal(JobStatus.Failed, storedJob.Status);
    }

    [Fact]
    public async Task ExecuteAsync_WhenTimeoutOccurs_SetsTimedOutStatus()
    {
        var executor = new JobExecutor(_stateStore, _scheduler, _deadLetterStore, _serviceProvider, _cancellationRegistry, _options, _logger);
        var jobPayload = new TimeoutJob();
        var job = new Job(Guid.NewGuid(), jobPayload, JobPriority.Normal, DateTimeOffset.UtcNow)
        {
            Timeout = TimeSpan.FromMilliseconds(50),
            RetryOptions = new RetryOptions { MaxAttempts = 1 }
        };

        var result = await executor.ExecuteAsync(job, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(FailureReason.Timeout, result.FailureReason);
        Assert.Equal(JobStatus.TimedOut, job.Status);

        var storedJob = await _stateStore.GetAsync(job.Id);
        Assert.NotNull(storedJob);
        Assert.Equal(JobStatus.TimedOut, storedJob.Status);
    }

    [Fact]
    public async Task ExecuteAsync_WhenCancelled_SetsCancelledStatus()
    {
        var executor = new JobExecutor(_stateStore, _scheduler, _deadLetterStore, _serviceProvider, _cancellationRegistry, _options, _logger);
        var jobPayload = new TimeoutJob();
        var job = new Job(Guid.NewGuid(), jobPayload, JobPriority.Normal, DateTimeOffset.UtcNow);

        using var cts = new CancellationTokenSource();
        cts.CancelAfter(50);

        var result = await executor.ExecuteAsync(job, cts.Token);

        Assert.False(result.IsSuccess);
        Assert.Equal(FailureReason.Cancelled, result.FailureReason);
        Assert.Equal(JobStatus.Cancelled, job.Status);

        var storedJob = await _stateStore.GetAsync(job.Id);
        Assert.NotNull(storedJob);
        Assert.Equal(JobStatus.Cancelled, storedJob.Status);
    }
}

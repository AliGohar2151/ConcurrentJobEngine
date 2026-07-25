using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using ConcurrentJobEngine.Core.Abstractions;
using ConcurrentJobEngine.Core.Enums;
using ConcurrentJobEngine.Core.Exceptions;
using ConcurrentJobEngine.Core.Models;
using ConcurrentJobEngine.Execution;
using ConcurrentJobEngine.Storage;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Xunit;

namespace ConcurrentJobEngine.UnitTests;

/// <summary>
/// Unit tests verifying JobProcessor behavior.
/// </summary>
public class JobProcessorTests
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

    private sealed class FakeJobScheduler : IJobScheduler
    {
        public readonly ConcurrentQueue<Job> ScheduledJobs = new();

        public Task ScheduleAsync(Job job, CancellationToken cancellationToken = default)
        {
            ScheduledJobs.Enqueue(job);
            return Task.CompletedTask;
        }

        public Task<Job> GetNextJobAsync(CancellationToken cancellationToken = default)
        {
            ScheduledJobs.TryDequeue(out var job);
            return Task.FromResult(job!);
        }
    }

    private sealed class FakeWorkerPool : IWorkerPool
    {
        public int WorkerCount => 1;
        public Task StartAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task StopAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed record TestJob : IJob;

    private readonly FakeJobStateStore _stateStore = new();
    private readonly FakeJobScheduler _scheduler = new();
    private readonly IJobCancellationRegistry _cancellationRegistry = new JobCancellationRegistry();
    private readonly IDeadLetterStore _deadLetterStore = new InMemoryDeadLetterStore();
    private readonly IWorkerPool _workerPool = new FakeWorkerPool();
    private readonly IOptions<ConcurrentJobEngineOptions> _options;
    private readonly ILogger<JobProcessor> _logger;

    public JobProcessorTests()
    {
        using var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
        _logger = loggerFactory.CreateLogger<JobProcessor>();
        _options = Options.Create(new ConcurrentJobEngineOptions
        {
            MaxQueueLimit = 2
        });
    }

    [Fact]
    public async Task SubmitAsync_SavesInitialStateAndSchedulesJob()
    {
        var processor = new JobProcessor(_scheduler, _stateStore, _deadLetterStore, _workerPool, _cancellationRegistry, _options, _logger);
        var jobPayload = new TestJob();
        var options = new JobOptions
        {
            Priority = JobPriority.High,
            Timeout = TimeSpan.FromMinutes(1)
        };

        var jobId = await processor.SubmitAsync(jobPayload, options, CancellationToken.None);

        Assert.NotEqual(Guid.Empty, jobId);

        // Verify scheduled in scheduler
        Assert.Single(_scheduler.ScheduledJobs);
        _scheduler.ScheduledJobs.TryPeek(out var scheduledJob);
        Assert.NotNull(scheduledJob);
        Assert.Equal(jobId, scheduledJob.Id);
        Assert.Equal(JobPriority.High, scheduledJob.Priority);
        Assert.Equal(TimeSpan.FromMinutes(1), scheduledJob.Timeout);

        // Verify stored in state store with Queued status
        var storedJob = await _stateStore.GetAsync(jobId);
        Assert.NotNull(storedJob);
        Assert.Equal(JobStatus.Queued, storedJob.Status);
    }

    [Fact]
    public async Task SubmitAsync_UnderBackpressureLimit_ThrowsJobRejectedException()
    {
        var processor = new JobProcessor(_scheduler, _stateStore, _deadLetterStore, _workerPool, _cancellationRegistry, _options, _logger);

        // Max limit is configured as 2 in constructor options
        var job1 = new TestJob();
        var job2 = new TestJob();
        var job3 = new TestJob();

        // Submitting first 2 should succeed
        await processor.SubmitAsync(job1);
        await processor.SubmitAsync(job2);

        // Submitting third should trigger backpressure and throw JobRejectedException
        await Assert.ThrowsAsync<JobRejectedException>(async () => await processor.SubmitAsync(job3));
    }

    [Fact]
    public async Task GetStatusAsync_ReturnsCorrectStatusInfo()
    {
        var processor = new JobProcessor(_scheduler, _stateStore, _deadLetterStore, _workerPool, _cancellationRegistry, _options, _logger);
        var jobId = Guid.NewGuid();
        var job = new Job(jobId, new TestJob(), JobPriority.Normal, DateTimeOffset.UtcNow)
        {
            Status = JobStatus.Running,
            AttemptCount = 2
        };

        await _stateStore.AddOrUpdateAsync(job);

        var statusInfo = await processor.GetStatusAsync(jobId);
        Assert.NotNull(statusInfo);
        Assert.Equal(jobId, statusInfo.JobId);
        Assert.Equal(JobStatus.Running, statusInfo.Status);
        Assert.Equal(2, statusInfo.AttemptCount);
    }

    [Fact]
    public async Task GetStatusAsync_ReturnsNull_WhenJobNotFound()
    {
        var processor = new JobProcessor(_scheduler, _stateStore, _deadLetterStore, _workerPool, _cancellationRegistry, _options, _logger);
        var statusInfo = await processor.GetStatusAsync(Guid.NewGuid());
        Assert.Null(statusInfo);
    }
}

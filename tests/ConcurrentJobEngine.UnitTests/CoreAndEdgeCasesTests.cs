using System;
using System.Threading;
using System.Threading.Tasks;
using ConcurrentJobEngine.Core.Abstractions;
using ConcurrentJobEngine.Core.Enums;
using ConcurrentJobEngine.Core.Exceptions;
using ConcurrentJobEngine.Core.Models;
using ConcurrentJobEngine.Execution;
using ConcurrentJobEngine.Storage;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Xunit;

namespace ConcurrentJobEngine.UnitTests;

/// <summary>
/// Unit tests covering Core models, custom exceptions, state store active count boundary filtering, and worker pool edge cases.
/// </summary>
public class CoreAndEdgeCasesTests
{
    private sealed record TestJob : IJob;

    [Fact]
    public void DomainExceptions_ConstructWithMessagesAndInnerExceptions()
    {
        var inner = new InvalidOperationException("Inner error");
        var ex1 = new JobExecutionException("Execution failed", inner);
        Assert.Equal("Execution failed", ex1.Message);
        Assert.Same(inner, ex1.InnerException);

        var ex2 = new JobRejectedException("Rejected");
        Assert.Equal("Rejected", ex2.Message);

        var ex3 = new JobTimeoutException("Timed out");
        Assert.Equal("Timed out", ex3.Message);
    }

    [Fact]
    public void JobOptions_DefaultsAndCustomAssignments()
    {
        var options = new JobOptions();
        Assert.Equal(JobPriority.Normal, options.Priority);
        Assert.Null(options.Timeout);
        Assert.Null(options.Retry);

        options.Priority = JobPriority.Critical;
        options.Timeout = TimeSpan.FromSeconds(30);
        options.Retry = new RetryOptions { MaxAttempts = 5 };

        Assert.Equal(JobPriority.Critical, options.Priority);
        Assert.Equal(TimeSpan.FromSeconds(30), options.Timeout);
        Assert.Equal(5, options.Retry.MaxAttempts);
    }

    [Fact]
    public void WorkerPoolOptions_Defaults()
    {
        var options = new WorkerPoolOptions();
        Assert.Equal(Environment.ProcessorCount, options.WorkerCount);
        Assert.Equal(TimeSpan.FromSeconds(5), options.ShutdownTimeout);
    }

    [Fact]
    public void ConcurrentJobEngineOptions_Defaults()
    {
        var options = new ConcurrentJobEngineOptions();
        Assert.Equal(Environment.ProcessorCount, options.WorkerCount);
        Assert.Equal(TimeSpan.FromSeconds(5), options.ShutdownTimeout);
        Assert.Equal(1000, options.MaxQueueLimit);
        Assert.Null(options.DefaultJobTimeout);
        Assert.NotNull(options.DefaultRetryOptions);
        Assert.Equal(3, options.DefaultRetryOptions.MaxAttempts);
    }

    [Fact]
    public void JobExecutionContext_PropertiesMatchConstructor()
    {
        var jobId = Guid.NewGuid();
        var startedAt = DateTimeOffset.UtcNow;
        var context = new JobExecutionContext(jobId, attemptNumber: 2, startedAt: startedAt, priority: JobPriority.High);

        Assert.Equal(jobId, context.JobId);
        Assert.Equal(2, context.AttemptNumber);
        Assert.Equal(startedAt, context.StartedAt);
        Assert.Equal(JobPriority.High, context.Priority);
    }

    [Fact]
    public void DeadLetterRecord_PropertiesMatchConstructor()
    {
        var jobId = Guid.NewGuid();
        var first = DateTimeOffset.UtcNow.AddMinutes(-5);
        var last = DateTimeOffset.UtcNow;
        var payload = new TestJob();

        var record = new DeadLetterRecord(jobId, "TestJob", payload, FailureReason.ExecutionFailed, "Error msg", 3, first, last);

        Assert.Equal(jobId, record.JobId);
        Assert.Equal("TestJob", record.JobType);
        Assert.Same(payload, record.Payload);
        Assert.Equal(FailureReason.ExecutionFailed, record.FailureReason);
        Assert.Equal("Error msg", record.ExceptionDetails);
        Assert.Equal(3, record.AttemptCount);
        Assert.Equal(first, record.FirstFailureTime);
        Assert.Equal(last, record.LastFailureTime);
    }

    [Fact]
    public async Task InMemoryJobStateStore_GetActiveCountAsync_FiltersOnlyActiveStatuses()
    {
        var store = new InMemoryJobStateStore();

        async Task AddWithTransitionAsync(Job job, JobStatus finalStatus)
        {
            job.Status = JobStatus.Submitted;
            await store.AddOrUpdateAsync(job);
            if (finalStatus != JobStatus.Submitted)
            {
                if (finalStatus == JobStatus.Queued)
                {
                    job.Status = JobStatus.Queued;
                    await store.AddOrUpdateAsync(job);
                }
                else if (finalStatus == JobStatus.Running)
                {
                    job.Status = JobStatus.Queued;
                    await store.AddOrUpdateAsync(job);
                    job.Status = JobStatus.Running;
                    await store.AddOrUpdateAsync(job);
                }
                else
                {
                    job.Status = JobStatus.Queued;
                    await store.AddOrUpdateAsync(job);
                    job.Status = JobStatus.Running;
                    await store.AddOrUpdateAsync(job);
                    job.Status = finalStatus;
                    await store.AddOrUpdateAsync(job);
                }
            }
        }

        await AddWithTransitionAsync(new Job(Guid.NewGuid(), new TestJob(), JobPriority.Normal, DateTimeOffset.UtcNow), JobStatus.Submitted);
        await AddWithTransitionAsync(new Job(Guid.NewGuid(), new TestJob(), JobPriority.Normal, DateTimeOffset.UtcNow), JobStatus.Queued);
        await AddWithTransitionAsync(new Job(Guid.NewGuid(), new TestJob(), JobPriority.Normal, DateTimeOffset.UtcNow), JobStatus.Running);
        await AddWithTransitionAsync(new Job(Guid.NewGuid(), new TestJob(), JobPriority.Normal, DateTimeOffset.UtcNow), JobStatus.Completed);
        await AddWithTransitionAsync(new Job(Guid.NewGuid(), new TestJob(), JobPriority.Normal, DateTimeOffset.UtcNow), JobStatus.Failed);
        await AddWithTransitionAsync(new Job(Guid.NewGuid(), new TestJob(), JobPriority.Normal, DateTimeOffset.UtcNow), JobStatus.Cancelled);
        await AddWithTransitionAsync(new Job(Guid.NewGuid(), new TestJob(), JobPriority.Normal, DateTimeOffset.UtcNow), JobStatus.TimedOut);

        var activeCount = await store.GetActiveCountAsync();
        Assert.Equal(3, activeCount); // Submitted, Queued, Running
    }

    [Fact]
    public async Task WorkerPool_StartAsync_CalledTwice_ThrowsInvalidOperationException()
    {
        var scheduler = new PriorityJobScheduler();
        var stateStore = new InMemoryJobStateStore();
        var deadLetterStore = new InMemoryDeadLetterStore();
        var registry = new JobCancellationRegistry();
        var options = Options.Create(new ConcurrentJobEngineOptions { WorkerCount = 1 });
        var serviceProvider = new Microsoft.Extensions.DependencyInjection.ServiceCollection().BuildServiceProvider();

        using var loggerFactory = LoggerFactory.Create(b => b.AddConsole());
        var executor = new JobExecutor(stateStore, scheduler, deadLetterStore, serviceProvider, registry, options, loggerFactory.CreateLogger<JobExecutor>());
        var pool = new WorkerPool(scheduler, executor, options, loggerFactory.CreateLogger<WorkerPool>());

        await pool.StartAsync();
        await Assert.ThrowsAsync<InvalidOperationException>(() => pool.StartAsync());

        await pool.StopAsync();
    }
}

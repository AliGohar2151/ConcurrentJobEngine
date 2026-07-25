using System;
using System.Threading.Tasks;
using ConcurrentJobEngine.Core.Abstractions;
using ConcurrentJobEngine.Core.Enums;
using ConcurrentJobEngine.Core.Models;
using ConcurrentJobEngine.Storage;
using Xunit;

namespace ConcurrentJobEngine.UnitTests;

/// <summary>
/// Unit tests for InMemoryJobStateStore verifying transition pathways and validation rules.
/// </summary>
public class InMemoryJobStateStoreTests
{
    private sealed record TestJobPayload : IJob;

    private Job CreateJob(JobStatus status = JobStatus.Submitted)
    {
        return new Job(Guid.NewGuid(), new TestJobPayload(), JobPriority.Normal, DateTimeOffset.UtcNow)
        {
            Status = status
        };
    }

    [Fact]
    public async Task AddOrUpdateAsync_WhenInitialStateIsSubmitted_Succeeds()
    {
        var store = new InMemoryJobStateStore();
        var job = CreateJob(JobStatus.Submitted);

        await store.AddOrUpdateAsync(job);

        var retrieved = await store.GetAsync(job.Id);
        Assert.NotNull(retrieved);
        Assert.Equal(JobStatus.Submitted, retrieved.Status);
    }

    [Fact]
    public async Task AddOrUpdateAsync_WhenInitialStateIsNotSubmitted_ThrowsInvalidOperationException()
    {
        var store = new InMemoryJobStateStore();
        var job = CreateJob(JobStatus.Queued);

        await Assert.ThrowsAsync<InvalidOperationException>(async () => await store.AddOrUpdateAsync(job));
    }

    [Fact]
    public async Task AddOrUpdateAsync_WithValidTransitions_Succeeds()
    {
        var store = new InMemoryJobStateStore();
        var job = CreateJob(JobStatus.Submitted);

        // Submitted -> Queued
        await store.AddOrUpdateAsync(job);
        job.Status = JobStatus.Queued;
        await store.AddOrUpdateAsync(job);

        // Queued -> Running
        job.Status = JobStatus.Running;
        await store.AddOrUpdateAsync(job);

        // Running -> Completed
        job.Status = JobStatus.Completed;
        await store.AddOrUpdateAsync(job);

        var retrieved = await store.GetAsync(job.Id);
        Assert.Equal(JobStatus.Completed, retrieved!.Status);
    }

    [Fact]
    public async Task AddOrUpdateAsync_WithInvalidTransition_ThrowsInvalidOperationException()
    {
        var store = new InMemoryJobStateStore();
        var job = CreateJob(JobStatus.Submitted);

        await store.AddOrUpdateAsync(job);

        // Invalid: Submitted -> Completed directly
        job.Status = JobStatus.Completed;
        await Assert.ThrowsAsync<InvalidOperationException>(async () => await store.AddOrUpdateAsync(job));
    }

    [Fact]
    public async Task AddOrUpdateAsync_TransitionFromFailedOrTimedOutToQueued_Succeeds()
    {
        var store = new InMemoryJobStateStore();
        
        // Setup failed path
        var job1 = CreateJob(JobStatus.Submitted);
        await store.AddOrUpdateAsync(job1);
        job1.Status = JobStatus.Queued;
        await store.AddOrUpdateAsync(job1);
        job1.Status = JobStatus.Running;
        await store.AddOrUpdateAsync(job1);
        job1.Status = JobStatus.Failed;
        await store.AddOrUpdateAsync(job1);

        // Retry: Failed -> Queued
        job1.Status = JobStatus.Queued;
        await store.AddOrUpdateAsync(job1);
        Assert.Equal(JobStatus.Queued, (await store.GetAsync(job1.Id))!.Status);

        // Setup timed out path
        var job2 = CreateJob(JobStatus.Submitted);
        await store.AddOrUpdateAsync(job2);
        job2.Status = JobStatus.Queued;
        await store.AddOrUpdateAsync(job2);
        job2.Status = JobStatus.Running;
        await store.AddOrUpdateAsync(job2);
        job2.Status = JobStatus.TimedOut;
        await store.AddOrUpdateAsync(job2);

        // Retry: TimedOut -> Queued
        job2.Status = JobStatus.Queued;
        await store.AddOrUpdateAsync(job2);
        Assert.Equal(JobStatus.Queued, (await store.GetAsync(job2.Id))!.Status);
    }

    [Fact]
    public async Task RemoveAsync_CleansUpStateStorage()
    {
        var store = new InMemoryJobStateStore();
        var job = CreateJob(JobStatus.Submitted);

        await store.AddOrUpdateAsync(job);
        Assert.NotNull(await store.GetAsync(job.Id));

        await store.RemoveAsync(job.Id);
        Assert.Null(await store.GetAsync(job.Id));
    }
}

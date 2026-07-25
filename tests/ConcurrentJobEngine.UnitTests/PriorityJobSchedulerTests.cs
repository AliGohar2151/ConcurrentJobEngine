using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ConcurrentJobEngine.Core.Abstractions;
using ConcurrentJobEngine.Core.Enums;
using ConcurrentJobEngine.Core.Models;
using ConcurrentJobEngine.Execution;
using Xunit;

namespace ConcurrentJobEngine.UnitTests;

/// <summary>
/// Unit tests verifying PriorityJobScheduler priority ordering, FIFO equal prioritisation, and concurrent safety.
/// </summary>
public class PriorityJobSchedulerTests
{
    private sealed record TestJobPayload : IJob;

    private Job CreateJob(JobPriority priority)
    {
        return new Job(Guid.NewGuid(), new TestJobPayload(), priority, DateTimeOffset.UtcNow);
    }

    [Fact]
    public async Task Scheduler_PrioritizesHigherPriorityJobs()
    {
        var scheduler = new PriorityJobScheduler();

        var jobLow = CreateJob(JobPriority.Low);
        var jobHigh = CreateJob(JobPriority.High);
        var jobCritical = CreateJob(JobPriority.Critical);
        var jobNormal = CreateJob(JobPriority.Normal);

        // Schedule in non-sorted order
        await scheduler.ScheduleAsync(jobLow);
        await scheduler.ScheduleAsync(jobHigh);
        await scheduler.ScheduleAsync(jobCritical);
        await scheduler.ScheduleAsync(jobNormal);

        // Dequeue should return in priority order: Critical -> High -> Normal -> Low
        var d1 = await scheduler.GetNextJobAsync();
        Assert.Equal(jobCritical.Id, d1.Id);

        var d2 = await scheduler.GetNextJobAsync();
        Assert.Equal(jobHigh.Id, d2.Id);

        var d3 = await scheduler.GetNextJobAsync();
        Assert.Equal(jobNormal.Id, d3.Id);

        var d4 = await scheduler.GetNextJobAsync();
        Assert.Equal(jobLow.Id, d4.Id);
    }

    [Fact]
    public async Task Scheduler_EnforcesFIFO_ForIdenticalPriorities()
    {
        var scheduler = new PriorityJobScheduler();

        var job1 = CreateJob(JobPriority.Normal);
        var job2 = CreateJob(JobPriority.Normal);
        var job3 = CreateJob(JobPriority.Normal);

        await scheduler.ScheduleAsync(job1);
        await scheduler.ScheduleAsync(job2);
        await scheduler.ScheduleAsync(job3);

        var d1 = await scheduler.GetNextJobAsync();
        Assert.Equal(job1.Id, d1.Id);

        var d2 = await scheduler.GetNextJobAsync();
        Assert.Equal(job2.Id, d2.Id);

        var d3 = await scheduler.GetNextJobAsync();
        Assert.Equal(job3.Id, d3.Id);
    }

    [Fact]
    public async Task GetNextJobAsync_BlocksUntilItemIsScheduled()
    {
        var scheduler = new PriorityJobScheduler();

        var dequeueTask = Task.Run(async () => await scheduler.GetNextJobAsync());

        await Task.Delay(50);
        Assert.False(dequeueTask.IsCompleted);

        var job = CreateJob(JobPriority.Normal);
        await scheduler.ScheduleAsync(job);

        await Task.WhenAny(dequeueTask, Task.Delay(500));
        Assert.True(dequeueTask.IsCompleted);

        var dequeuedJob = await dequeueTask;
        Assert.Equal(job.Id, dequeuedJob.Id);
    }

    [Fact]
    public async Task Scheduler_HandlesConcurrentLoad()
    {
        var scheduler = new PriorityJobScheduler();
        int producerCount = 5;
        int jobsPerProducer = 100;
        int totalJobs = producerCount * jobsPerProducer;

        var producers = new List<Task>();
        for (int i = 0; i < producerCount; i++)
        {
            producers.Add(Task.Run(async () =>
            {
                var rand = new Random();
                for (int j = 0; j < jobsPerProducer; j++)
                {
                    var priority = (JobPriority)rand.Next(0, 4);
                    var job = CreateJob(priority);
                    await scheduler.ScheduleAsync(job);
                }
            }));
        }

        await Task.WhenAll(producers);

        var dequeuedList = new ConcurrentBag<Job>();
        var consumers = new List<Task>();
        int consumerCount = 3;

        for (int i = 0; i < consumerCount; i++)
        {
            consumers.Add(Task.Run(async () =>
            {
                while (dequeuedList.Count < totalJobs)
                {
                    try
                    {
                        using var cts = new CancellationTokenSource(200);
                        var job = await scheduler.GetNextJobAsync(cts.Token);
                        dequeuedList.Add(job);
                    }
                    catch (OperationCanceledException)
                    {
                        // Safe break if queue drains and wait times out
                        break;
                    }
                }
            }));
        }

        await Task.WhenAll(consumers);

        Assert.Equal(totalJobs, dequeuedList.Count);
    }
}

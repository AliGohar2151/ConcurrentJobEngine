using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ConcurrentJobEngine.Core.Abstractions;
using ConcurrentJobEngine.Core.Enums;
using ConcurrentJobEngine.Core.Models;
using ConcurrentJobEngine.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xunit;

namespace ConcurrentJobEngine.IntegrationTests;

/// <summary>
/// Stress tests verifying thread safety, state consistency, zero job loss, and zero duplicate executions under high concurrency.
/// </summary>
public class ConcurrencyTests
{
    private sealed record ConcurrentWorkJob(int JobNumber) : IJob;

    private sealed class ConcurrentWorkHandler : IJobHandler<ConcurrentWorkJob>
    {
        public static readonly ConcurrentDictionary<Guid, int> ExecutionCounts = new();

        public Task<JobResult> HandleAsync(ConcurrentWorkJob job, JobExecutionContext context, CancellationToken cancellationToken)
        {
            ExecutionCounts.AddOrUpdate(context.JobId, 1, (_, count) => count + 1);
            return Task.FromResult(JobResult.Success());
        }
    }

    private static IServiceProvider BuildConcurrencyServiceProvider(int workerCount)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddConcurrentJobEngine(opts =>
        {
            opts.WorkerCount = workerCount;
            opts.MaxQueueLimit = 10_000;
        });

        services.AddJobHandler<ConcurrentWorkJob, ConcurrentWorkHandler>();
        return services.BuildServiceProvider();
    }

    [Fact]
    public async Task HighVolumeConcurrentSubmission_ZeroJobLoss_ZeroDuplicateExecutions()
    {
        ConcurrentWorkHandler.ExecutionCounts.Clear();

        const int totalJobs = 1_000;
        const int producerTasksCount = 20;
        const int jobsPerProducer = totalJobs / producerTasksCount;

        var provider = BuildConcurrencyServiceProvider(workerCount: 8);
        var processor = provider.GetRequiredService<IJobProcessor>();
        var workerPool = provider.GetRequiredService<IWorkerPool>();

        await workerPool.StartAsync();

        try
        {
            var submittedJobIds = new ConcurrentBag<Guid>();

            // 20 parallel producers submitting jobs simultaneously
            var producerTasks = Enumerable.Range(0, producerTasksCount).Select(producerId => Task.Run(async () =>
            {
                for (int i = 0; i < jobsPerProducer; i++)
                {
                    int jobNumber = producerId * jobsPerProducer + i;
                    var jobId = await processor.SubmitAsync(new ConcurrentWorkJob(jobNumber));
                    submittedJobIds.Add(jobId);
                }
            })).ToArray();

            await Task.WhenAll(producerTasks);

            Assert.Equal(totalJobs, submittedJobIds.Count);

            // Wait for all 1,000 jobs to complete processing
            var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
            while (ConcurrentWorkHandler.ExecutionCounts.Count < totalJobs && !timeoutCts.IsCancellationRequested)
            {
                await Task.Delay(10);
            }

            Assert.Equal(totalJobs, ConcurrentWorkHandler.ExecutionCounts.Count);

            // Verify zero duplicate executions: every job ID was executed exactly once
            foreach (var kvp in ConcurrentWorkHandler.ExecutionCounts)
            {
                Assert.Equal(1, kvp.Value);
            }
        }
        finally
        {
            await workerPool.StopAsync();
        }
    }

    [Fact]
    public async Task ConcurrentCancellationAndExecution_ThreadSafety()
    {
        const int totalJobs = 200;
        var provider = BuildConcurrencyServiceProvider(workerCount: 4);
        var processor = provider.GetRequiredService<IJobProcessor>();
        var workerPool = provider.GetRequiredService<IWorkerPool>();

        await workerPool.StartAsync();

        try
        {
            var submittedIds = new List<Guid>();
            for (int i = 0; i < totalJobs; i++)
            {
                var id = await processor.SubmitAsync(new ConcurrentWorkJob(i));
                submittedIds.Add(id);
            }

            // Concurrently request cancellation on half of the jobs
            var cancelTasks = submittedIds.Take(totalJobs / 2).Select(id => Task.Run(async () =>
            {
                try
                {
                    await processor.CancelAsync(id);
                }
                catch
                {
                    // Ignore if already completed
                }
            })).ToArray();

            await Task.WhenAll(cancelTasks);

            // Wait for all jobs to settle into a final state (Completed or Cancelled)
            var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(15));
            while (!timeoutCts.Token.IsCancellationRequested)
            {
                int finalizedCount = 0;
                foreach (var id in submittedIds)
                {
                    var status = await processor.GetStatusAsync(id);
                    if (status != null && (status.Status == JobStatus.Completed || status.Status == JobStatus.Cancelled))
                    {
                        finalizedCount++;
                    }
                }

                if (finalizedCount == totalJobs) break;
                await Task.Delay(100, timeoutCts.Token);
            }

            // Verify every single job reached a terminal state
            foreach (var id in submittedIds)
            {
                var status = await processor.GetStatusAsync(id);
                Assert.NotNull(status);
                Assert.True(status.Status == JobStatus.Completed || status.Status == JobStatus.Cancelled);
            }
        }
        finally
        {
            await workerPool.StopAsync();
        }
    }
}

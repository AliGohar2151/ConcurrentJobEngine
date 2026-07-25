using System;
using System.Threading;
using System.Threading.Tasks;
using ConcurrentJobEngine.Core.Abstractions;
using ConcurrentJobEngine.Core.Enums;
using ConcurrentJobEngine.Core.Exceptions;
using ConcurrentJobEngine.Core.Models;
using ConcurrentJobEngine.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xunit;

namespace ConcurrentJobEngine.IntegrationTests;

/// <summary>
/// End-to-end integration tests verifying the full ConcurrentJobEngine pipeline assembled via .NET Dependency Injection.
/// </summary>
public class EndToEndJobEngineTests
{
    private sealed record TestSuccessJob(int TargetId) : IJob;
    private sealed record TestFailingJob(string Reason) : IJob;
    private sealed record TestTimeoutJob(int DelayMs) : IJob;
    private sealed record TestCancelJob : IJob;

    private sealed class SuccessHandler : IJobHandler<TestSuccessJob>
    {
        public Task<JobResult> HandleAsync(TestSuccessJob job, JobExecutionContext context, CancellationToken cancellationToken)
            => Task.FromResult(JobResult.Success());
    }

    private sealed class FailingHandler : IJobHandler<TestFailingJob>
    {
        public Task<JobResult> HandleAsync(TestFailingJob job, JobExecutionContext context, CancellationToken cancellationToken)
            => Task.FromResult(JobResult.Failure(FailureReason.ExecutionFailed, job.Reason));
    }

    private sealed class TimeoutHandler : IJobHandler<TestTimeoutJob>
    {
        public async Task<JobResult> HandleAsync(TestTimeoutJob job, JobExecutionContext context, CancellationToken cancellationToken)
        {
            await Task.Delay(job.DelayMs, cancellationToken);
            return JobResult.Success();
        }
    }

    private sealed class CancelHandler : IJobHandler<TestCancelJob>
    {
        public async Task<JobResult> HandleAsync(TestCancelJob job, JobExecutionContext context, CancellationToken cancellationToken)
        {
            await Task.Delay(5000, cancellationToken);
            return JobResult.Success();
        }
    }

    private static IServiceProvider BuildEngineServiceProvider(Action<ConcurrentJobEngineOptions>? configure = null)
    {
        var services = new ServiceCollection();
        services.AddLogging(b => b.AddConsole());
        services.AddConcurrentJobEngine(configure ?? (opts =>
        {
            opts.WorkerCount = 2;
            opts.ShutdownTimeout = TimeSpan.FromSeconds(3);
        }));

        services.AddJobHandler<TestSuccessJob, SuccessHandler>();
        services.AddJobHandler<TestFailingJob, FailingHandler>();
        services.AddJobHandler<TestTimeoutJob, TimeoutHandler>();
        services.AddJobHandler<TestCancelJob, CancelHandler>();

        return services.BuildServiceProvider();
    }

    [Fact]
    public async Task SuccessJobPipeline_ProcessesAndCompletesJobs()
    {
        var provider = BuildEngineServiceProvider();
        var processor = provider.GetRequiredService<IJobProcessor>();
        var workerPool = provider.GetRequiredService<IWorkerPool>();

        await workerPool.StartAsync();

        try
        {
            var jobId = await processor.SubmitAsync(new TestSuccessJob(42));
            Assert.NotEqual(Guid.Empty, jobId);

            // Poll for completion
            JobStatusInfo? status = null;
            for (int i = 0; i < 50; i++)
            {
                status = await processor.GetStatusAsync(jobId);
                if (status != null && status.Status == JobStatus.Completed)
                {
                    break;
                }
                await Task.Delay(50);
            }

            Assert.NotNull(status);
            Assert.Equal(JobStatus.Completed, status.Status);
            Assert.Equal(1, status.AttemptCount);
            Assert.NotNull(status.CompletedAt);
        }
        finally
        {
            await workerPool.StopAsync();
        }
    }

    [Fact]
    public async Task RetryExhaustion_RoutesToDeadLetterStore()
    {
        var provider = BuildEngineServiceProvider();
        var processor = provider.GetRequiredService<IJobProcessor>();
        var workerPool = provider.GetRequiredService<IWorkerPool>();

        await workerPool.StartAsync();

        try
        {
            var options = new JobOptions
            {
                Retry = new RetryOptions
                {
                    MaxAttempts = 2,
                    InitialDelay = TimeSpan.FromMilliseconds(10)
                }
            };

            var jobId = await processor.SubmitAsync(new TestFailingJob("Database offline"), options);

            // Poll for final status
            JobStatusInfo? status = null;
            for (int i = 0; i < 50; i++)
            {
                status = await processor.GetStatusAsync(jobId);
                if (status != null && status.Status == JobStatus.Failed)
                {
                    break;
                }
                await Task.Delay(50);
            }

            Assert.NotNull(status);
            Assert.Equal(JobStatus.Failed, status.Status);
            Assert.Equal(2, status.AttemptCount);

            var deadLetterJobs = await processor.GetDeadLetterJobsAsync();
            Assert.Contains(deadLetterJobs, record => record.JobId == jobId);
        }
        finally
        {
            await workerPool.StopAsync();
        }
    }

    [Fact]
    public async Task TimeoutJob_EnforcesTimeoutAndRetries()
    {
        var provider = BuildEngineServiceProvider();
        var processor = provider.GetRequiredService<IJobProcessor>();
        var workerPool = provider.GetRequiredService<IWorkerPool>();

        await workerPool.StartAsync();

        try
        {
            var options = new JobOptions
            {
                Timeout = TimeSpan.FromMilliseconds(100),
                Retry = new RetryOptions { MaxAttempts = 1 }
            };

            var jobId = await processor.SubmitAsync(new TestTimeoutJob(1000), options);

            JobStatusInfo? status = null;
            for (int i = 0; i < 50; i++)
            {
                status = await processor.GetStatusAsync(jobId);
                if (status != null && (status.Status == JobStatus.TimedOut || status.Status == JobStatus.Failed))
                {
                    break;
                }
                await Task.Delay(50);
            }

            Assert.NotNull(status);
            Assert.True(status.Status == JobStatus.TimedOut || status.Status == JobStatus.Failed);
        }
        finally
        {
            await workerPool.StopAsync();
        }
    }

    [Fact]
    public async Task JobCancellation_CancelsRunningWorker()
    {
        var provider = BuildEngineServiceProvider();
        var processor = provider.GetRequiredService<IJobProcessor>();
        var workerPool = provider.GetRequiredService<IWorkerPool>();

        await workerPool.StartAsync();

        try
        {
            var jobId = await processor.SubmitAsync(new TestCancelJob());

            // Wait for worker to pick up and transition to Running
            for (int i = 0; i < 50; i++)
            {
                var s = await processor.GetStatusAsync(jobId);
                if (s != null && s.Status == JobStatus.Running) break;
                await Task.Delay(50);
            }

            await processor.CancelAsync(jobId);

            JobStatusInfo? status = null;
            for (int i = 0; i < 50; i++)
            {
                status = await processor.GetStatusAsync(jobId);
                if (status != null && status.Status == JobStatus.Cancelled) break;
                await Task.Delay(50);
            }

            Assert.NotNull(status);
            Assert.Equal(JobStatus.Cancelled, status.Status);
        }
        finally
        {
            await workerPool.StopAsync();
        }
    }

    [Fact]
    public async Task GracefulShutdown_DrainsInFlightJobsAndRejectsSubmissions()
    {
        var provider = BuildEngineServiceProvider();
        var processor = provider.GetRequiredService<IJobProcessor>();
        var workerPool = provider.GetRequiredService<IWorkerPool>();

        await workerPool.StartAsync();

        var jobId = await processor.SubmitAsync(new TestSuccessJob(100));

        await processor.StopAsync();

        await Assert.ThrowsAsync<JobRejectedException>(() => processor.SubmitAsync(new TestSuccessJob(200)));

        var status = await processor.GetStatusAsync(jobId);
        Assert.NotNull(status);
        Assert.Equal(JobStatus.Completed, status.Status);
    }
}

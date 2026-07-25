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
/// Unit tests verifying job execution timeouts and fallback engine defaults.
/// </summary>
public class JobTimeoutTests
{
    private sealed record TestTimeoutJob : IJob;

    private sealed class TimeoutJobHandler : IJobHandler<TestTimeoutJob>
    {
        public async Task<JobResult> HandleAsync(TestTimeoutJob job, JobExecutionContext context, CancellationToken cancellationToken)
        {
            await Task.Delay(2000, cancellationToken);
            return JobResult.Success();
        }
    }

    private readonly InMemoryJobStateStore _stateStore = new();
    private readonly IJobScheduler _scheduler = new PriorityJobScheduler();
    private readonly JobCancellationRegistry _cancellationRegistry = new();
    private readonly IDeadLetterStore _deadLetterStore = new InMemoryDeadLetterStore();
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<JobExecutor> _logger;

    public JobTimeoutTests()
    {
        var services = new ServiceCollection();
        services.AddJobHandler<TestTimeoutJob, TimeoutJobHandler>();
        _serviceProvider = services.BuildServiceProvider();

        using var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
        _logger = loggerFactory.CreateLogger<JobExecutor>();
    }

    [Fact]
    public async Task ExecuteAsync_EnforcesJobSpecificTimeout()
    {
        var options = Options.Create(new ConcurrentJobEngineOptions
        {
            DefaultJobTimeout = TimeSpan.FromMinutes(5), // Global is large
            DefaultRetryOptions = new RetryOptions { MaxAttempts = 1 }
        });

        var executor = new JobExecutor(_stateStore, _scheduler, _deadLetterStore, _serviceProvider, _cancellationRegistry, options, _logger);
        
        var job = new Job(Guid.NewGuid(), new TestTimeoutJob(), JobPriority.Normal, DateTimeOffset.UtcNow)
        {
            Timeout = TimeSpan.FromMilliseconds(100) // Specific is short
        };

        // Transition: Submitted -> Queued
        await _stateStore.AddOrUpdateAsync(job);
        job.Status = JobStatus.Queued;
        await _stateStore.AddOrUpdateAsync(job);

        var result = await executor.ExecuteAsync(job, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(FailureReason.Timeout, result.FailureReason);
        Assert.Equal(JobStatus.TimedOut, job.Status);
    }

    [Fact]
    public async Task ExecuteAsync_EnforcesFallbackGlobalEngineTimeout()
    {
        var options = Options.Create(new ConcurrentJobEngineOptions
        {
            DefaultJobTimeout = TimeSpan.FromMilliseconds(100), // Global is short
            DefaultRetryOptions = new RetryOptions { MaxAttempts = 1 }
         });

        var executor = new JobExecutor(_stateStore, _scheduler, _deadLetterStore, _serviceProvider, _cancellationRegistry, options, _logger);

        // Job has no specific timeout set
        var job = new Job(Guid.NewGuid(), new TestTimeoutJob(), JobPriority.Normal, DateTimeOffset.UtcNow);

        // Transition: Submitted -> Queued
        await _stateStore.AddOrUpdateAsync(job);
        job.Status = JobStatus.Queued;
        await _stateStore.AddOrUpdateAsync(job);

        var result = await executor.ExecuteAsync(job, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(FailureReason.Timeout, result.FailureReason);
        Assert.Equal(JobStatus.TimedOut, job.Status);
    }
}

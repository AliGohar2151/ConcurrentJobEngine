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
/// Unit tests verifying individual job-level cancellation under different lifecycle states.
/// </summary>
public class JobCancellationTests
{
    private sealed record TestCancelJob : IJob;

    private sealed class CancelJobHandler : IJobHandler<TestCancelJob>
    {
        public async Task<JobResult> HandleAsync(TestCancelJob job, JobExecutionContext context, CancellationToken cancellationToken)
        {
            // Wait cooperatively using token
            await Task.Delay(5000, cancellationToken);
            return JobResult.Success();
        }
    }

    private readonly InMemoryJobStateStore _stateStore = new();
    private readonly PriorityJobScheduler _scheduler = new();
    private readonly JobCancellationRegistry _cancellationRegistry = new();
    private readonly IDeadLetterStore _deadLetterStore = new InMemoryDeadLetterStore();
    private readonly IWorkerPool _workerPool = new FakeWorkerPool();
    private readonly IServiceProvider _serviceProvider;
    private readonly IOptions<ConcurrentJobEngineOptions> _options;
    private readonly ILogger<JobProcessor> _processorLogger;
    private readonly ILogger<JobExecutor> _executorLogger;

    private sealed class FakeWorkerPool : IWorkerPool
    {
        public int WorkerCount => 1;
        public Task StartAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task StopAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    public JobCancellationTests()
    {
        var services = new ServiceCollection();
        services.AddJobHandler<TestCancelJob, CancelJobHandler>();
        _serviceProvider = services.BuildServiceProvider();

        using var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
        _processorLogger = loggerFactory.CreateLogger<JobProcessor>();
        _executorLogger = loggerFactory.CreateLogger<JobExecutor>();
        _options = Options.Create(new ConcurrentJobEngineOptions());
    }

    [Fact]
    public async Task CancelAsync_ForQueuedJob_TransitionsToCancelledAndSkipsExecution()
    {
        var processor = new JobProcessor(_scheduler, _stateStore, _deadLetterStore, _workerPool, _cancellationRegistry, _options, _processorLogger);
        var executor = new JobExecutor(_stateStore, _scheduler, _deadLetterStore, _serviceProvider, _cancellationRegistry, _options, _executorLogger);

        var jobId = await processor.SubmitAsync(new TestCancelJob());

        // Cancel while it is still in the Queued state
        await processor.CancelAsync(jobId);

        // Verify status in state store is Cancelled
        var status = await processor.GetStatusAsync(jobId);
        Assert.NotNull(status);
        Assert.Equal(JobStatus.Cancelled, status.Status);

        // Try executing the job. It should skip execution and return Cancelled immediately
        var job = await _scheduler.GetNextJobAsync();
        Assert.Equal(jobId, job.Id);
        
        var result = await executor.ExecuteAsync(job, CancellationToken.None);
        Assert.False(result.IsSuccess);
        Assert.Equal(FailureReason.Cancelled, result.FailureReason);
    }

    [Fact]
    public async Task CancelAsync_ForRunningJob_CancelsExecutingHandlerCooperatively()
    {
        var processor = new JobProcessor(_scheduler, _stateStore, _deadLetterStore, _workerPool, _cancellationRegistry, _options, _processorLogger);
        var executor = new JobExecutor(_stateStore, _scheduler, _deadLetterStore, _serviceProvider, _cancellationRegistry, _options, _executorLogger);

        var jobId = await processor.SubmitAsync(new TestCancelJob());
        var job = await _scheduler.GetNextJobAsync();

        var executeTask = Task.Run(async () => await executor.ExecuteAsync(job, CancellationToken.None));

        // Wait a short moment to ensure the execution loop starts and registers the CTS
        await Task.Delay(50);

        // Cancel the running job
        await processor.CancelAsync(jobId);

        var result = await executeTask;

        Assert.False(result.IsSuccess);
        Assert.Equal(FailureReason.Cancelled, result.FailureReason);

        var status = await processor.GetStatusAsync(jobId);
        Assert.NotNull(status);
        Assert.Equal(JobStatus.Cancelled, status.Status);
    }
}

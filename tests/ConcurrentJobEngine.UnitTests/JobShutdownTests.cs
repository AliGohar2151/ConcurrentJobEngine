using System;
using System.Threading;
using System.Threading.Tasks;
using ConcurrentJobEngine.Core.Abstractions;
using ConcurrentJobEngine.Core.Exceptions;
using ConcurrentJobEngine.Core.Models;
using ConcurrentJobEngine.Execution;
using ConcurrentJobEngine.Storage;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Xunit;

namespace ConcurrentJobEngine.UnitTests;

/// <summary>
/// Unit tests verifying graceful engine shutdown: new submissions are gated and the worker pool is drained.
/// </summary>
public class JobShutdownTests
{
    private sealed record TestShutdownJob : IJob;

    /// <summary>
    /// A fake worker pool that records whether StopAsync was called.
    /// </summary>
    private sealed class TrackingWorkerPool : IWorkerPool
    {
        public int WorkerCount => 1;
        public bool StopCalled { get; private set; }

        public Task StartAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task StopAsync(CancellationToken cancellationToken = default)
        {
            StopCalled = true;
            return Task.CompletedTask;
        }
    }

    private sealed class FakeJobScheduler : IJobScheduler
    {
        public Task ScheduleAsync(Job job, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<Job> GetNextJobAsync(CancellationToken cancellationToken = default)
            => Task.FromResult<Job>(null!);
    }

    private readonly InMemoryJobStateStore _stateStore = new();
    private readonly InMemoryDeadLetterStore _deadLetterStore = new();
    private readonly FakeJobScheduler _scheduler = new();
    private readonly JobCancellationRegistry _cancellationRegistry = new();
    private readonly IOptions<ConcurrentJobEngineOptions> _options;
    private readonly ILogger<JobProcessor> _logger;

    public JobShutdownTests()
    {
        using var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
        _logger = loggerFactory.CreateLogger<JobProcessor>();
        _options = Options.Create(new ConcurrentJobEngineOptions
        {
            MaxQueueLimit = 100,
            ShutdownTimeout = TimeSpan.FromSeconds(5)
        });
    }

    [Fact]
    public async Task StopAsync_DelegatesToWorkerPool()
    {
        var workerPool = new TrackingWorkerPool();
        var processor = new JobProcessor(_scheduler, _stateStore, _deadLetterStore, workerPool, _cancellationRegistry, _options, _logger);

        await processor.StopAsync();

        Assert.True(workerPool.StopCalled);
    }

    [Fact]
    public async Task SubmitAsync_AfterStopAsync_ThrowsJobRejectedException()
    {
        var workerPool = new TrackingWorkerPool();
        var processor = new JobProcessor(_scheduler, _stateStore, _deadLetterStore, workerPool, _cancellationRegistry, _options, _logger);

        await processor.StopAsync();

        await Assert.ThrowsAsync<JobRejectedException>(() => processor.SubmitAsync(new TestShutdownJob()));
    }

    [Fact]
    public async Task SubmitAsync_BeforeStopAsync_Succeeds()
    {
        var workerPool = new TrackingWorkerPool();
        var processor = new JobProcessor(_scheduler, _stateStore, _deadLetterStore, workerPool, _cancellationRegistry, _options, _logger);

        // Should not throw before StopAsync is called
        var jobId = await processor.SubmitAsync(new TestShutdownJob());
        Assert.NotEqual(Guid.Empty, jobId);
    }

    [Fact]
    public async Task StopAsync_IsIdempotent_WhenCalledTwice()
    {
        var workerPool = new TrackingWorkerPool();
        var processor = new JobProcessor(_scheduler, _stateStore, _deadLetterStore, workerPool, _cancellationRegistry, _options, _logger);

        await processor.StopAsync();
        // Second call should not throw
        await processor.StopAsync();
    }
}

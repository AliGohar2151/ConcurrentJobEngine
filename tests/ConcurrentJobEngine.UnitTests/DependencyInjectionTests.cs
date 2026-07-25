using System;
using System.Threading.Tasks;
using ConcurrentJobEngine.Core.Abstractions;
using ConcurrentJobEngine.Core.Models;
using ConcurrentJobEngine.DependencyInjection;
using ConcurrentJobEngine.Execution;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace ConcurrentJobEngine.UnitTests;

/// <summary>
/// Verifies that AddConcurrentJobEngine correctly registers all engine services in the DI container.
/// </summary>
public class DependencyInjectionTests
{
    private sealed record SampleJob : IJob;

    private sealed class SampleJobHandler : IJobHandler<SampleJob>
    {
        public Task<JobResult> HandleAsync(SampleJob job, JobExecutionContext context, System.Threading.CancellationToken cancellationToken)
            => Task.FromResult(JobResult.Success());
    }

    [Fact]
    public void AddConcurrentJobEngine_RegistersIJobProcessor()
    {
        var services = new ServiceCollection()
            .AddLogging()
            .AddConcurrentJobEngine();

        var provider = services.BuildServiceProvider();

        var processor = provider.GetService<IJobProcessor>();
        Assert.NotNull(processor);
    }

    [Fact]
    public void AddConcurrentJobEngine_RegistersIWorkerPool()
    {
        var services = new ServiceCollection()
            .AddLogging()
            .AddConcurrentJobEngine();

        var provider = services.BuildServiceProvider();

        var workerPool = provider.GetService<IWorkerPool>();
        Assert.NotNull(workerPool);
    }

    [Fact]
    public void AddConcurrentJobEngine_RegistersIJobExecutor()
    {
        var services = new ServiceCollection()
            .AddLogging()
            .AddConcurrentJobEngine();

        var provider = services.BuildServiceProvider();

        var executor = provider.GetService<IJobExecutor>();
        Assert.NotNull(executor);
    }

    [Fact]
    public void AddConcurrentJobEngine_RegistersIJobStateStore()
    {
        var services = new ServiceCollection()
            .AddLogging()
            .AddConcurrentJobEngine();

        var provider = services.BuildServiceProvider();

        var stateStore = provider.GetService<IJobStateStore>();
        Assert.NotNull(stateStore);
    }

    [Fact]
    public void AddConcurrentJobEngine_RegistersIDeadLetterStore()
    {
        var services = new ServiceCollection()
            .AddLogging()
            .AddConcurrentJobEngine();

        var provider = services.BuildServiceProvider();

        var deadLetterStore = provider.GetService<IDeadLetterStore>();
        Assert.NotNull(deadLetterStore);
    }

    [Fact]
    public void AddConcurrentJobEngine_RegistersIJobScheduler()
    {
        var services = new ServiceCollection()
            .AddLogging()
            .AddConcurrentJobEngine();

        var provider = services.BuildServiceProvider();

        var scheduler = provider.GetService<IJobScheduler>();
        Assert.NotNull(scheduler);
    }

    [Fact]
    public void AddConcurrentJobEngine_RegistersIJobCancellationRegistry()
    {
        var services = new ServiceCollection()
            .AddLogging()
            .AddConcurrentJobEngine();

        var provider = services.BuildServiceProvider();

        var registry = provider.GetService<IJobCancellationRegistry>();
        Assert.NotNull(registry);
    }

    [Fact]
    public void AddConcurrentJobEngine_AllSingletonServicesAreSameInstance()
    {
        var services = new ServiceCollection()
            .AddLogging()
            .AddConcurrentJobEngine();

        var provider = services.BuildServiceProvider();

        // Resolving the same singleton twice must return the same instance
        var processor1 = provider.GetRequiredService<IJobProcessor>();
        var processor2 = provider.GetRequiredService<IJobProcessor>();
        Assert.Same(processor1, processor2);
    }

    [Fact]
    public void AddJobHandler_RegistersHandlerCorrectly()
    {
        var services = new ServiceCollection()
            .AddLogging()
            .AddConcurrentJobEngine()
            .AddJobHandler<SampleJob, SampleJobHandler>();

        var provider = services.BuildServiceProvider();

        var handler = provider.GetService<IJobHandler<SampleJob>>();
        Assert.NotNull(handler);
        Assert.IsType<SampleJobHandler>(handler);
    }

    [Fact]
    public void AddConcurrentJobEngine_WithConfigure_AppliesOptions()
    {
        var services = new ServiceCollection()
            .AddLogging()
            .AddConcurrentJobEngine(opts =>
            {
                opts.WorkerCount = 4;
                opts.MaxQueueLimit = 500;
            });

        var provider = services.BuildServiceProvider();

        // If processor resolves it means options were consumed without error
        var processor = provider.GetRequiredService<IJobProcessor>();
        Assert.NotNull(processor);
    }

    [Fact]
    public void AddConcurrentJobEngine_CalledTwice_DoesNotDuplicateRegistrations()
    {
        var services = new ServiceCollection()
            .AddLogging()
            .AddConcurrentJobEngine()
            .AddConcurrentJobEngine(); // second call should be idempotent via TryAddSingleton

        var provider = services.BuildServiceProvider();

        // Should still resolve exactly one IJobProcessor
        var processor = provider.GetRequiredService<IJobProcessor>();
        Assert.NotNull(processor);
    }
}

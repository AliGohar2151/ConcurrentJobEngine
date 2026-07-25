using System;
using System.Threading.Tasks;
using BenchmarkDotNet.Attributes;
using ConcurrentJobEngine.Core.Abstractions;
using ConcurrentJobEngine.Core.Enums;
using ConcurrentJobEngine.Core.Models;
using ConcurrentJobEngine.Execution;
using ConcurrentJobEngine.Storage;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace ConcurrentJobEngine.Benchmarks;

/// <summary>
/// Micro-benchmarks measuring JobProcessor submission throughput and memory allocations.
/// </summary>
[MemoryDiagnoser]
public class JobProcessorBenchmarks
{
    private sealed record BenchmarkJob : IJob;

    private JobProcessor _processor = null!;
    private JobOptions _options = null!;

    [Params(100, 1_000)]
    public int BatchSize { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        var scheduler = new PriorityJobScheduler();
        var stateStore = new InMemoryJobStateStore();
        var deadLetterStore = new InMemoryDeadLetterStore();
        var registry = new JobCancellationRegistry();
        var engineOptions = Options.Create(new ConcurrentJobEngineOptions { MaxQueueLimit = 100_000 });
        var workerPool = new WorkerPool(scheduler, null!, engineOptions, NullLogger<WorkerPool>.Instance);

        _processor = new JobProcessor(
            scheduler,
            stateStore,
            deadLetterStore,
            workerPool,
            registry,
            engineOptions,
            NullLogger<JobProcessor>.Instance);

        _options = new JobOptions { Priority = JobPriority.High };
    }

    [Benchmark]
    public async Task SubmitAsync_Batch()
    {
        var jobPayload = new BenchmarkJob();
        for (int i = 0; i < BatchSize; i++)
        {
            await _processor.SubmitAsync(jobPayload, _options);
        }
    }
}

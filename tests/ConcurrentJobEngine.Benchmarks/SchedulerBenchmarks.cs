using System;
using System.Threading.Tasks;
using BenchmarkDotNet.Attributes;
using ConcurrentJobEngine.Core.Abstractions;
using ConcurrentJobEngine.Core.Enums;
using ConcurrentJobEngine.Core.Models;
using ConcurrentJobEngine.Execution;

namespace ConcurrentJobEngine.Benchmarks;

/// <summary>
/// Micro-benchmarks measuring priority scheduler enqueue and dequeue throughput and allocations.
/// </summary>
[MemoryDiagnoser]
public class SchedulerBenchmarks
{
    private sealed record BenchmarkJob : IJob;

    private PriorityJobScheduler _scheduler = null!;
    private Job _job = null!;

    [Params(100, 1_000)]
    public int BatchSize { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        _scheduler = new PriorityJobScheduler();
        _job = new Job(Guid.NewGuid(), new BenchmarkJob(), JobPriority.Normal, DateTimeOffset.UtcNow);
    }

    [Benchmark]
    public async Task ScheduleAsync_Batch()
    {
        for (int i = 0; i < BatchSize; i++)
        {
            await _scheduler.ScheduleAsync(_job);
        }
    }

    [Benchmark]
    public async Task ScheduleAndDequeue_Batch()
    {
        for (int i = 0; i < BatchSize; i++)
        {
            await _scheduler.ScheduleAsync(_job);
            await _scheduler.GetNextJobAsync();
        }
    }
}

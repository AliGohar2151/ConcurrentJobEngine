using System;
using System.Collections.Generic;
using System.Diagnostics.Metrics;
using System.Threading;
using System.Threading.Tasks;
using ConcurrentJobEngine.Core.Abstractions;
using ConcurrentJobEngine.Core.Enums;
using ConcurrentJobEngine.Core.Models;
using ConcurrentJobEngine.DependencyInjection;
using ConcurrentJobEngine.Diagnostics;
using ConcurrentJobEngine.Execution;
using ConcurrentJobEngine.Storage;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Xunit;

namespace ConcurrentJobEngine.UnitTests;

/// <summary>
/// Unit tests verifying System.Diagnostics.Metrics metrics recording across the job engine lifecycle.
/// </summary>
public class MetricsTests
{
    private sealed record TestMetricJob : IJob;

    private sealed class SuccessMetricJobHandler : IJobHandler<TestMetricJob>
    {
        public Task<JobResult> HandleAsync(TestMetricJob job, JobExecutionContext context, CancellationToken cancellationToken)
            => Task.FromResult(JobResult.Success());
    }

    private sealed class FailMetricJobHandler : IJobHandler<TestMetricJob>
    {
        public Task<JobResult> HandleAsync(TestMetricJob job, JobExecutionContext context, CancellationToken cancellationToken)
            => Task.FromResult(JobResult.Failure(FailureReason.ExecutionFailed, "Error"));
    }

    [Fact]
    public void EngineMetrics_DirectCalls_PublishMeterMeasurements()
    {
        using var meter = new Meter("ConcurrentJobEngine.TestMeter", "1.0.0");
        var metrics = new EngineMetrics(meter);

        var recordedMeasurements = new Dictionary<string, long>();

        using var listener = new MeterListener();
        listener.InstrumentPublished = (instrument, meterListener) =>
        {
            if (instrument.Meter.Name == "ConcurrentJobEngine.TestMeter")
            {
                meterListener.EnableMeasurementEvents(instrument);
            }
        };

        listener.SetMeasurementEventCallback<long>((instrument, measurement, tags, state) =>
        {
            lock (recordedMeasurements)
            {
                recordedMeasurements[instrument.Name] = recordedMeasurements.GetValueOrDefault(instrument.Name) + measurement;
            }
        });

        listener.Start();

        metrics.RecordJobSubmitted();
        metrics.IncrementActiveJobs();
        metrics.IncrementQueueDepth();
        metrics.DecrementQueueDepth();
        metrics.RecordJobCompleted(0.5);
        metrics.DecrementActiveJobs();
        metrics.RecordJobRetried();
        metrics.RecordJobCancelled();
        metrics.RecordJobTimedOut();
        metrics.RecordJobFailed();
        metrics.RecordJobDeadLettered();

        listener.RecordObservableInstruments();

        lock (recordedMeasurements)
        {
            Assert.Equal(1, recordedMeasurements["jobs.submitted"]);
            Assert.Equal(1, recordedMeasurements["jobs.completed"]);
            Assert.Equal(1, recordedMeasurements["jobs.retried"]);
            Assert.Equal(1, recordedMeasurements["jobs.cancelled"]);
            Assert.Equal(1, recordedMeasurements["jobs.timed_out"]);
            Assert.Equal(1, recordedMeasurements["jobs.failed"]);
            Assert.Equal(1, recordedMeasurements["jobs.dead_lettered"]);
        }
    }

    [Fact]
    public async Task JobExecution_E2E_UpdatesMetrics()
    {
        using var meter = new Meter("ConcurrentJobEngine.E2E", "1.0.0");
        var metrics = new EngineMetrics(meter);

        var counts = new Dictionary<string, long>();

        using var listener = new MeterListener();
        listener.InstrumentPublished = (instrument, meterListener) =>
        {
            if (instrument.Meter.Name == "ConcurrentJobEngine.E2E")
            {
                meterListener.EnableMeasurementEvents(instrument);
            }
        };

        listener.SetMeasurementEventCallback<long>((instrument, measurement, tags, state) =>
        {
            lock (counts)
            {
                counts[instrument.Name] = counts.GetValueOrDefault(instrument.Name) + measurement;
            }
        });

        listener.Start();

        var stateStore = new InMemoryJobStateStore();
        var deadLetterStore = new InMemoryDeadLetterStore();
        var scheduler = new PriorityJobScheduler(metrics);
        var cancellationRegistry = new JobCancellationRegistry();

        var services = new ServiceCollection();
        services.AddJobHandler<TestMetricJob, SuccessMetricJobHandler>();
        var provider = services.BuildServiceProvider();

        using var loggerFactory = LoggerFactory.Create(b => b.AddConsole());
        var executorLogger = loggerFactory.CreateLogger<JobExecutor>();
        var processorLogger = loggerFactory.CreateLogger<JobProcessor>();
        var options = Options.Create(new ConcurrentJobEngineOptions());

        var executor = new JobExecutor(stateStore, scheduler, deadLetterStore, provider, cancellationRegistry, options, executorLogger, metrics);

        var workerPool = new WorkerPool(scheduler, executor, options, loggerFactory.CreateLogger<WorkerPool>());
        var processor = new JobProcessor(scheduler, stateStore, deadLetterStore, workerPool, cancellationRegistry, options, processorLogger, metrics);

        var jobId = await processor.SubmitAsync(new TestMetricJob());

        var job = await scheduler.GetNextJobAsync();
        var result = await executor.ExecuteAsync(job);

        Assert.True(result.IsSuccess);

        lock (counts)
        {
            Assert.Equal(1, counts.GetValueOrDefault("jobs.submitted"));
            Assert.Equal(1, counts.GetValueOrDefault("jobs.completed"));
        }
    }

    [Fact]
    public void NullEngineMetrics_DoesNotThrow()
    {
        var nullMetrics = NullEngineMetrics.Instance;
        nullMetrics.RecordJobSubmitted();
        nullMetrics.IncrementActiveJobs();
        nullMetrics.DecrementActiveJobs();
        nullMetrics.IncrementQueueDepth();
        nullMetrics.DecrementQueueDepth();
        nullMetrics.RecordJobDequeued(1.0);
        nullMetrics.RecordJobCompleted(1.0);
        nullMetrics.RecordJobFailed();
        nullMetrics.RecordJobRetried();
        nullMetrics.RecordJobCancelled();
        nullMetrics.RecordJobTimedOut();
        nullMetrics.RecordJobDeadLettered();
    }
}

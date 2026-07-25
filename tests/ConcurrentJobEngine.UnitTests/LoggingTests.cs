using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ConcurrentJobEngine.Core.Abstractions;
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
/// Verifies structured operational logging across job submission, execution, cancellation, and shutdown.
/// </summary>
public class LoggingTests
{
    private sealed record TestLogJob : IJob;

    private sealed class TestLogJobHandler : IJobHandler<TestLogJob>
    {
        public Task<JobResult> HandleAsync(TestLogJob job, JobExecutionContext context, CancellationToken cancellationToken)
            => Task.FromResult(JobResult.Success());
    }

    private class TestLoggerProvider : ILoggerProvider
    {
        public List<(LogLevel Level, string Message)> LogEntries { get; } = new();

        public ILogger CreateLogger(string categoryName) => new TestLogger(LogEntries);

        public void Dispose() { }
    }

    private class TestLogger : ILogger
    {
        private readonly List<(LogLevel Level, string Message)> _logEntries;

        public TestLogger(List<(LogLevel Level, string Message)> logEntries) => _logEntries = logEntries;

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            lock (_logEntries)
            {
                _logEntries.Add((logLevel, formatter(state, exception)));
            }
        }
    }

    [Fact]
    public async Task JobProcessor_LogsJobSubmissionAndShutdown()
    {
        var loggerProvider = new TestLoggerProvider();
        var services = new ServiceCollection();
        services.AddLogging(builder => builder.AddProvider(loggerProvider));
        services.AddConcurrentJobEngine();
        services.AddJobHandler<TestLogJob, TestLogJobHandler>();

        var provider = services.BuildServiceProvider();
        var processor = provider.GetRequiredService<IJobProcessor>();

        var jobId = await processor.SubmitAsync(new TestLogJob());

        Assert.NotEqual(Guid.Empty, jobId);
        Assert.Contains(loggerProvider.LogEntries, log => log.Message.Contains("Submitting job") && log.Level == LogLevel.Information);

        await processor.StopAsync();
        Assert.Contains(loggerProvider.LogEntries, log => log.Message.Contains("Engine shutdown") && log.Level == LogLevel.Information);
    }

    [Fact]
    public async Task WorkerPool_LogsStartAndStopEvents()
    {
        var loggerProvider = new TestLoggerProvider();
        var services = new ServiceCollection();
        services.AddLogging(builder => builder.AddProvider(loggerProvider));
        services.AddConcurrentJobEngine(opts => opts.WorkerCount = 2);

        var provider = services.BuildServiceProvider();
        var workerPool = provider.GetRequiredService<IWorkerPool>();

        await workerPool.StartAsync();
        Assert.Contains(loggerProvider.LogEntries, log => log.Message.Contains("Starting worker pool") && log.Level == LogLevel.Information);

        await workerPool.StopAsync();
        Assert.Contains(loggerProvider.LogEntries, log => log.Message.Contains("Stopping worker pool") && log.Level == LogLevel.Information);
    }
}

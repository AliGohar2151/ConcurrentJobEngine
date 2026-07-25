# ConcurrentJobEngine

[![build](https://github.com/AliGohar2151/ConcurrentJobEngine/actions/workflows/ci.yml/badge.svg)](https://github.com/AliGohar2151/ConcurrentJobEngine/actions)
[![license](https://img.shields.io/badge/license-MIT-blue.svg)](LICENSE)
[![.NET](https://img.shields.io/badge/.NET-10.0-purple.svg)](https://dotnet.microsoft.com/)

A high-performance, resilient, in-process concurrent job processing engine for C# .NET applications. Designed for high throughput, priority scheduling, backpressure control, exponential backoff retries with full jitter, dead-letter processing, structured logging, runtime metrics, and graceful shutdown.

---

## Key Features

- **Priority Scheduling**: Enforce `Critical`, `High`, `Normal`, and `Low` job prioritization with thread-safe FIFO tie-breaking.
- **Worker Pool**: Configurable background worker pool executing jobs concurrently across CPU cores.
- **Backpressure & Capacity Control**: Reject or throttle submissions when queue limits are reached.
- **Exponential Backoff & Full Jitter**: Configurable retry policies with randomized jitter to prevent thundering herd problems.
- **Dead-Letter Store**: Route permanently failed or timeout-exhausted jobs to an `IDeadLetterStore` for analysis and audit.
- **Structured Logging & Metrics**: Integrated with `ILogger<T>` and standard `.NET` `System.Diagnostics.Metrics` (`Meter`).
- **Graceful Shutdown**: Drain in-flight jobs cleanly upon engine termination within configurable shutdown timeouts.
- **Dependency Injection**: First-class integration via `.AddConcurrentJobEngine()` extension methods on `IServiceCollection`.

---

## Architecture Overview

The engine uses a decoupled producer-consumer architecture:

```text
  [ Client Application ]
            │
   SubmitAsync(job, options)
            │
            ▼
   ┌───────────────────┐
   │   JobProcessor    │ ─── (Backpressure & State Store Gate)
   └─────────┬─────────┘
             │
             ▼
   ┌───────────────────┐
   │ PriorityScheduler │ ─── (Critical > High > Normal > Low FIFO)
   └─────────┬─────────┘
             │
             ▼
   ┌───────────────────┐      ┌─────────────────────────┐
   │    WorkerPool     │ ───► │       JobExecutor       │
   │ (Worker Threads)  │      └────────────┬────────────┘
   └───────────────────┘                   │
                                           ▼
                                ┌─────────────────────┐
                                │ IJobHandler<TJob>   │
                                └─────────────────────┘
```

---

## Quickstart Guide

### 1. Register Services via Dependency Injection

```csharp
using ConcurrentJobEngine.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;

var services = new ServiceCollection();

// Register ConcurrentJobEngine with custom worker options
services.AddConcurrentJobEngine(options =>
{
    options.WorkerCount = 4;
    options.MaxQueueLimit = 5000;
    options.ShutdownTimeout = TimeSpan.FromSeconds(5);
});

// Register job handlers
services.AddJobHandler<SendEmailJob, SendEmailJobHandler>();

var provider = services.BuildServiceProvider();
```

### 2. Define a Job & Handler

```csharp
using ConcurrentJobEngine.Core.Abstractions;
using ConcurrentJobEngine.Core.Models;

public record SendEmailJob(string To, string Subject) : IJob;

public class SendEmailJobHandler : IJobHandler<SendEmailJob>
{
    public async Task<JobResult> HandleAsync(SendEmailJob job, JobExecutionContext context, CancellationToken cancellationToken)
    {
        // Business logic here
        await Task.Delay(100, cancellationToken);
        return JobResult.Success();
    }
}
```

### 3. Start Workers & Submit Jobs

```csharp
var workerPool = provider.GetRequiredService<IWorkerPool>();
var processor = provider.GetRequiredService<IJobProcessor>();

// Start worker loops
await workerPool.StartAsync();

// Submit job with High priority and Retry policy
var jobId = await processor.SubmitAsync(
    new SendEmailJob("user@example.com", "Welcome!"),
    new JobOptions
    {
        Priority = JobPriority.High,
        Timeout = TimeSpan.FromSeconds(10),
        Retry = new RetryOptions { MaxAttempts = 3, InitialDelay = TimeSpan.FromMilliseconds(200) }
    });

// Check Status
var status = await processor.GetStatusAsync(jobId);
Console.WriteLine($"Job Status: {status?.Status}");

// Graceful Shutdown
await processor.StopAsync();
```

---

## Project Structure

```text
ConcurrentJobEngine/
├── src/
│   ├── ConcurrentJobEngine.Core/          # Domain abstractions, models, enums, exceptions
│   ├── ConcurrentJobEngine/               # Engine execution, worker pool, storage, metrics
│   └── ConcurrentJobEngine.Sample/        # Interactive sample application
└── tests/
    ├── ConcurrentJobEngine.UnitTests/     # 76 unit tests for all core components
    ├── ConcurrentJobEngine.IntegrationTests/# 7 integration and stress concurrency tests
    └── ConcurrentJobEngine.Benchmarks/    # BenchmarkDotNet micro-benchmarking harness
```

---

## Verification & Testing

Run all unit and integration tests:

```bash
dotnet test
```

Generate NuGet packages:

```bash
dotnet pack -c Release
```

---

## License

Distributed under the MIT License. See `LICENSE` for details.

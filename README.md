# ConcurrentJobEngine

[![build](https://github.com/AliGohar2151/ConcurrentJobEngine/actions/workflows/ci.yml/badge.svg)](https://github.com/AliGohar2151/ConcurrentJobEngine/actions)
[![license](https://img.shields.io/badge/license-MIT-blue.svg)](LICENSE)
[![.NET](https://img.shields.io/badge/.NET-10.0-purple.svg)](https://dotnet.microsoft.com/)
[![NuGet](https://img.shields.io/badge/nuget-v1.0.0-blue.svg)](https://www.nuget.org/)

**ConcurrentJobEngine** is a production-ready, high-performance, in-process concurrent job processing framework built for modern C# and .NET applications.

---

## 📌 Problem Statement

In enterprise backend applications, processing background tasks (e.g., sending emails, processing payments, generating PDF reports, processing webhooks, or manipulating images) is a fundamental requirement. However, ad-hoc background implementations typically suffer from severe issues:

1. **Unbounded Resource Exhaustion**: Fire-and-forget `Task.Run` calls or basic unmanaged `Channel<T>` queues can easily overwhelm memory and thread-pool resources during traffic spikes.
2. **Lack of Priority Control**: Critical real-time jobs (such as payment processing) get delayed behind long-running batch jobs (such as email campaigns).
3. **Fragile Error Resilience**: Simple try/catch loops fail under transient network errors or cause a **thundering herd problem** when retrying immediately without backoff or jitter.
4. **Silent Job Loss**: Unhandled exceptions drop jobs without persistent record tracking or audit capability.
5. **Abrupt Shutdown Data Loss**: Stopping an application drops active in-flight work without giving jobs time to complete gracefully.

---

## 🚀 The Solution

`ConcurrentJobEngine` resolves these challenges by providing a robust, thread-safe, decoupled producer-consumer pipeline with native resilience, priority scheduling, backpressure control, and observability.

### Key Capabilities

- **Priority-Based Scheduling**: Supports `Critical`, `High`, `Normal`, and `Low` job priorities with deterministic FIFO tie-breaking for equal priority levels.
- **Configurable Worker Pool**: Multi-threaded worker pool that dynamically executes queued jobs up to a configured concurrency limit.
- **Backpressure & Queue Throttling**: Protects application memory by rejecting or throttling job submissions when queue capacity limits are reached (`MaxQueueLimit`).
- **Exponential Backoff & Full Jitter**: Configurable retry policies with exponential multipliers and randomized jitter to prevent service overloading.
- **Dead-Letter Storage**: Automatically routes jobs that exhaust their retry attempts or encounter fatal unrecoverable errors to an `IDeadLetterStore` for analysis.
- **Timeouts & Cooperative Cancellation**: Enforces strict execution time limits (`JobOptions.Timeout`) and passes linked `CancellationToken` signals directly to job handlers.
- **First-Class Dependency Injection**: Fluent setup via `.AddConcurrentJobEngine()` and `.AddJobHandler<TJob, THandler>()` extension methods for `IServiceCollection`.
- **Observability**: Structured logging via `ILogger<T>` and runtime execution telemetry via `.NET` `System.Diagnostics.Metrics` (`Meter`).
- **Graceful Engine Shutdown**: Drains in-flight background worker jobs safely during application teardown or `Ctrl+C` signals.

---

## 🏗️ Architecture & Pipeline

```text
  [ Client Application / API ]
               │
      SubmitAsync(jobPayload, options)
               │
               ▼
  ┌─────────────────────────┐
  │      JobProcessor       │ ───► (Validates payload & checks queue limits)
  └────────────┬────────────┘
               │
               ▼
  ┌─────────────────────────┐
  │   PriorityJobScheduler  │ ───► (Critical > High > Normal > Low FIFO Queue)
  └────────────┬────────────┘
               │
               ▼
  ┌─────────────────────────┐      ┌─────────────────────────┐
  │       WorkerPool        │ ───► │       JobExecutor       │
  │   (Concurrent Workers)  │      └────────────┬────────────┘
  └─────────────────────────┘                   │
                                                ▼
                                     ┌─────────────────────┐
                                     │  IJobHandler<TJob>  │
                                     └──────────┬──────────┘
                                                │
                                    ┌───────────┴───────────┐
                                    ▼                       ▼
                              [ Success ]              [ Failure ]
                                                        │ (Evaluate Retry)
                                                        ├─► Retry (Exponential Backoff + Jitter)
                                                        └─► Dead-Letter Store (Attempts Exhausted)
```

---

## 💻 How Developers Use It

### Step 1: Installation & Registration

Register `ConcurrentJobEngine` services in your application's Dependency Injection container:

```csharp
using ConcurrentJobEngine.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;

var services = new ServiceCollection();

// Register engine with options
services.AddConcurrentJobEngine(options =>
{
    options.WorkerCount = 8;                         // 8 parallel worker threads
    options.MaxQueueLimit = 10_000;                  // Max queued jobs before backpressure rejection
    options.ShutdownTimeout = TimeSpan.FromSeconds(10); // Time to drain jobs on graceful shutdown
});

// Register strongly-typed job handlers
services.AddJobHandler<SendEmailJob, SendEmailJobHandler>();
services.AddJobHandler<ProcessPaymentJob, ProcessPaymentJobHandler>();

var provider = services.BuildServiceProvider();
```

---

### Step 2: Define a Job & Handler

Create job payload marker contracts implementing `IJob` and strongly-typed execution handlers implementing `IJobHandler<TJob>`:

```csharp
using System.Threading;
using System.Threading.Tasks;
using ConcurrentJobEngine.Core.Abstractions;
using ConcurrentJobEngine.Core.Models;

// 1. Define job payload
public record ProcessPaymentJob(string TransactionId, decimal Amount) : IJob;

// 2. Implement handler logic
public class ProcessPaymentJobHandler : IJobHandler<ProcessPaymentJob>
{
    private readonly IPaymentGateway _paymentGateway;

    public ProcessPaymentJobHandler(IPaymentGateway paymentGateway)
    {
        _paymentGateway = paymentGateway;
    }

    public async Task<JobResult> HandleAsync(ProcessPaymentJob job, JobExecutionContext context, CancellationToken cancellationToken)
    {
        bool result = await _paymentGateway.ChargeAsync(job.TransactionId, job.Amount, cancellationToken);

        if (!result)
        {
            // Return failure result to trigger retry policy
            return JobResult.Failure(FailureReason.ExecutionFailed, "Gateway connection timeout.");
        }

        return JobResult.Success();
    }
}
```

---

### Step 3: Start Worker Pool & Submit Jobs

Start the background worker pool and submit jobs with priority and retry configurations:

```csharp
using ConcurrentJobEngine.Core.Abstractions;
using ConcurrentJobEngine.Core.Enums;
using ConcurrentJobEngine.Core.Models;

var workerPool = provider.GetRequiredService<IWorkerPool>();
var processor = provider.GetRequiredService<IJobProcessor>();

// Start worker background loop
await workerPool.StartAsync();

// Submit a critical payment job with exponential retries and full jitter
var jobId = await processor.SubmitAsync(
    new ProcessPaymentJob("TX-998231", 249.99m),
    new JobOptions
    {
        Priority = JobPriority.Critical,
        Timeout = TimeSpan.FromSeconds(5),
        Retry = new RetryOptions
        {
            MaxAttempts = 3,
            InitialDelay = TimeSpan.FromMilliseconds(200),
            BackoffMultiplier = 2.0,
            UseJitter = true
        }
    });

Console.WriteLine($"Submitted payment job with ID: {jobId}");
```

---

### Step 4: Query Job Status & Dead-Letter Store

Inspect execution state or query dead-lettered jobs:

```csharp
// Get status of a job
JobStatusInfo? status = await processor.GetStatusAsync(jobId);
Console.WriteLine($"Status: {status?.Status}, Attempts: {status?.AttemptCount}");

// Retrieve dead-lettered records
IReadOnlyList<DeadLetterRecord> deadLetterJobs = await processor.GetDeadLetterJobsAsync();
foreach (var record in deadLetterJobs)
{
    Console.WriteLine($"Dead-Letter Job: {record.JobId} | Type: {record.JobType} | Reason: {record.FailureReason}");
}
```

---

### Step 5: Graceful Engine Shutdown

Stop the engine cleanly to allow active worker threads to finish processing:

```csharp
// Drain in-flight jobs and shutdown background workers
await processor.StopAsync();
```

---

## 📁 Repository Structure

```text
ConcurrentJobEngine/
├── src/
│   ├── ConcurrentJobEngine.Core/          # Domain interfaces, enums, models, exceptions
│   ├── ConcurrentJobEngine/               # Priority scheduler, worker pool, retry executor, DI
│   └── ConcurrentJobEngine.Sample/        # Interactive runnable CLI sample application
└── tests/
    ├── ConcurrentJobEngine.UnitTests/     # 76 component unit tests
    ├── ConcurrentJobEngine.IntegrationTests/# 7 high-concurrency integration stress tests
    └── ConcurrentJobEngine.Benchmarks/    # BenchmarkDotNet throughput and allocation benchmarks
```

---

## 🧪 Verification & Testing

### Run All Unit & Integration Tests

```bash
dotnet test
```

### Run Benchmarks

```bash
dotnet run -c Release --project tests/ConcurrentJobEngine.Benchmarks/ConcurrentJobEngine.Benchmarks.csproj
```

### Run Interactive Sample Application

```bash
dotnet run --project src/ConcurrentJobEngine.Sample/ConcurrentJobEngine.Sample.csproj
```

---

## 📄 License

Distributed under the **MIT License**. See `LICENSE` for more information.

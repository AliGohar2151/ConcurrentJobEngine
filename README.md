# ConcurrentJobEngine

[![build](https://github.com/AliGohar2151/ConcurrentJobEngine/actions/workflows/ci.yml/badge.svg)](https://github.com/AliGohar2151/ConcurrentJobEngine/actions)
[![license](https://img.shields.io/badge/license-MIT-blue.svg)](LICENSE)
[![.NET](https://img.shields.io/badge/.NET-8.0%20%7C%209.0%20%7C%2010.0-purple.svg)](https://dotnet.microsoft.com/)
[![NuGet](https://img.shields.io/nuget/v/ConcurrentJobEngine.svg)](https://www.nuget.org/packages/ConcurrentJobEngine)
[![NuGet Downloads](https://img.shields.io/nuget/dt/ConcurrentJobEngine.svg)](https://www.nuget.org/packages/ConcurrentJobEngine)

**ConcurrentJobEngine** is an in-process, high-performance concurrent job processing engine for .NET applications. It provides priority-based scheduling, worker pool management, configurable retry policies with exponential backoff and jitter, backpressure control, dead letter storage, runtime metrics, and graceful shutdown capabilities.

---

## Table of Contents

- [Why ConcurrentJobEngine?](#why-concurrentjobengine)
- [Problem Statement](#problem-statement)
- [Solution Overview](#solution-overview)
- [Features](#features)
- [Architecture](#architecture)
- [Installation](#installation)
- [Packages](#packages)
- [Quick Start](#quick-start)
- [Developer Usage Guide](#developer-usage-guide)
  - [1. Service Registration](#1-service-registration)
  - [2. Define Jobs and Handlers](#2-define-jobs-and-handlers)
  - [3. Job Submission with Priorities and Retries](#3-job-submission-with-priorities-and-retries)
  - [4. Status Queries and Dead Letter Store](#4-status-queries-and-dead-letter-store)
  - [5. Graceful Engine Shutdown](#5-graceful-engine-shutdown)
- [Supported Frameworks](#supported-frameworks)
- [Repository Structure](#repository-structure)
- [Verification & Testing](#verification--testing)
- [License](#license)

---

## Why ConcurrentJobEngine?

- **High-Performance Worker Pool**: Executes background workloads concurrently across a configurable pool of worker threads.
- **Priority Scheduling**: Processes critical tasks ahead of normal or low-priority background work with deterministic FIFO ordering.
- **Automatic Retries**: Retries failing jobs using exponential backoff and randomized full jitter to protect downstream services.
- **Dead Letter Queue**: Persists permanently failed or exhausted jobs to an `IDeadLetterStore` for auditability and manual inspection.
- **Backpressure Protection**: Restricts memory overhead during high-traffic spikes by enforcing configurable queue limits.
- **Graceful Shutdown**: Drains in-flight background worker jobs cleanly during host termination within specified timeout limits.
- **First-Class DI Integration**: Native configuration using `Microsoft.Extensions.DependencyInjection` extension methods.
- **Built-in Metrics & Logging**: Structured `ILogger` telemetry and `System.Diagnostics.Metrics` (`Meter`) integration.

---

## Problem Statement

Background processing in .NET applications often starts with unmanaged `Task.Run` calls or basic `Channel<T>` primitives. As application traffic grows, these naive implementations reveal major operational vulnerabilities:

1. **Unbounded Thread and Memory Consumption**: Spawning unconstrained tasks under heavy load quickly saturates system resources, causing memory exhaustion and thread pool starvation.
2. **Missing Priority Handling**: Time-sensitive background tasks (such as payment authorization or password reset emails) get blocked behind slow batch operations (such as nightly report generation).
3. **Improper Error Resilience**: Immediate retries during transient network or service outages cause a thundering herd effect, overloading struggling external dependencies.
4. **Silent Failure and Job Loss**: Unhandled worker exceptions terminate background threads without recording failure reasons or retaining failed payloads.
5. **Abrupt Process Termination**: Shutting down an application drops currently executing jobs, leading to incomplete transactions and corrupted state.

---

## Solution Overview

`ConcurrentJobEngine` addresses these challenges by encapsulating job execution within a structured, decoupled producer-consumer pipeline. It manages worker allocation, queue bounds, task cancellation propagation, retry delays, and dead-letter routing within a clean, thread-safe architecture.

---

## Features

| Feature | Status |
| ------- | ------ |
| Priority Scheduling | ✅ |
| Worker Pool | ✅ |
| Retry Policy | ✅ |
| Dead Letter Queue | ✅ |
| Dependency Injection | ✅ |
| Metrics | ✅ |
| Logging | ✅ |
| Graceful Shutdown | ✅ |
| Cancellation Tokens | ✅ |

---

## Architecture

```text
  [ Client Application / API ]
               │
      SubmitAsync(jobPayload, options)
               │
               ▼
  ┌─────────────────────────┐
  │      JobProcessor       │ ───► (Validates payload & enforces queue limits)
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
                                                        └─► Dead Letter Store (Exhausted)
```

### Request Processing Flow

```text
Client  ──►  JobProcessor  ──►  Priority Scheduler  ──►  Worker Pool  ──►  Handler  ──►  Retry or Dead Letter Store
```

1. **Client**: Invokes `processor.SubmitAsync(payload, options)`.
2. **JobProcessor**: Validates inputs, assigns a unique `Guid`, checks queue capacity, and records initial state.
3. **Priority Scheduler**: Enqueues the job into a priority-ordered queue (`Critical` > `High` > `Normal` > `Low`).
4. **Worker Pool**: Background worker threads dequeue jobs according to priority order.
5. **Handler**: `JobExecutor` resolves `IJobHandler<TJob>` and executes the job with cancellation token management.
6. **Retry / Dead Letter**: Successful jobs transition to `Completed`. Failed jobs evaluate their `RetryOptions`; if attempts are exhausted, the record is routed to the `IDeadLetterStore`.

---

## Installation

### .NET CLI

```bash
dotnet add package ConcurrentJobEngine
```

### Package Manager

```powershell
Install-Package ConcurrentJobEngine
```

### PackageReference

```xml
<PackageReference Include="ConcurrentJobEngine" Version="1.0.3" />
```

---

## Packages

| Package | Description |
| ------- | ----------- |
| `ConcurrentJobEngine` | Full concurrent job processing engine with worker pool, scheduler, retry policy, dependency injection, and metrics. |
| `ConcurrentJobEngine.Core` | Core abstractions, interfaces, enums, and models for advanced scenarios and custom implementations. |

---

## Quick Start

```csharp
using ConcurrentJobEngine.Core.Abstractions;
using ConcurrentJobEngine.Core.Models;
using ConcurrentJobEngine.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;

var services = new ServiceCollection();
services.AddConcurrentJobEngine(opts => { opts.WorkerCount = 4; });
services.AddJobHandler<SendEmailJob, SendEmailJobHandler>();
var provider = services.BuildServiceProvider();

await provider.GetRequiredService<IWorkerPool>().StartAsync();
var processor = provider.GetRequiredService<IJobProcessor>();

var jobId = await processor.SubmitAsync(new SendEmailJob("user@example.com", "Welcome!"));

public record SendEmailJob(string To, string Subject) : IJob;
public class SendEmailJobHandler : IJobHandler<SendEmailJob>
{
    public Task<JobResult> HandleAsync(SendEmailJob job, JobExecutionContext context, CancellationToken ct) =>
        Task.FromResult(JobResult.Success());
}
```

> Continue reading for complete examples.

---

## Developer Usage Guide

### 1. Service Registration

Register `ConcurrentJobEngine` and your job handlers using Microsoft Dependency Injection:

```csharp
using ConcurrentJobEngine.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;

var services = new ServiceCollection();

services.AddConcurrentJobEngine(options =>
{
    options.WorkerCount = 8;                         // Number of parallel worker threads
    options.MaxQueueLimit = 10_000;                  // Maximum queued jobs before backpressure rejection
    options.ShutdownTimeout = TimeSpan.FromSeconds(10); // Timeout for draining in-flight jobs on shutdown
});

// Register strongly-typed job handlers
services.AddJobHandler<SendEmailJob, SendEmailJobHandler>();
services.AddJobHandler<ProcessPaymentJob, ProcessPaymentJobHandler>();

var provider = services.BuildServiceProvider();
```

---

### 2. Define Jobs and Handlers

Define job payload records that implement `IJob` and handlers that implement `IJobHandler<TJob>`:

```csharp
using System.Threading;
using System.Threading.Tasks;
using ConcurrentJobEngine.Core.Abstractions;
using ConcurrentJobEngine.Core.Models;

public record ProcessPaymentJob(string TransactionId, decimal Amount) : IJob;

public class ProcessPaymentJobHandler : IJobHandler<ProcessPaymentJob>
{
    private readonly IPaymentGateway _paymentGateway;

    public ProcessPaymentJobHandler(IPaymentGateway paymentGateway)
    {
        _paymentGateway = paymentGateway;
    }

    public async Task<JobResult> HandleAsync(ProcessPaymentJob job, JobExecutionContext context, CancellationToken cancellationToken)
    {
        bool charged = await _paymentGateway.ChargeAsync(job.TransactionId, job.Amount, cancellationToken);

        if (!charged)
        {
            return JobResult.Failure(FailureReason.ExecutionFailed, "Payment gateway call timed out.");
        }

        return JobResult.Success();
    }
}
```

---

### 3. Job Submission with Priorities and Retries

Start the worker pool and submit jobs with explicit priorities, execution timeouts, and retry policies:

```csharp
using ConcurrentJobEngine.Core.Abstractions;
using ConcurrentJobEngine.Core.Enums;
using ConcurrentJobEngine.Core.Models;

var workerPool = provider.GetRequiredService<IWorkerPool>();
var processor = provider.GetRequiredService<IJobProcessor>();

// Start worker loops
await workerPool.StartAsync();

// Submit job with Critical priority and exponential backoff retry policy
var jobId = await processor.SubmitAsync(
    new ProcessPaymentJob("TX-998231", 149.99m),
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

Console.WriteLine($"Submitted payment job ID: {jobId}");
```

---

### 4. Status Queries and Dead Letter Store

Query runtime status or retrieve dead-lettered job records:

```csharp
// Query status of a specific job
JobStatusInfo? status = await processor.GetStatusAsync(jobId);
Console.WriteLine($"Status: {status?.Status}, Attempts: {status?.AttemptCount}");

// Retrieve dead-letter records
IReadOnlyList<DeadLetterRecord> deadLetters = await processor.GetDeadLetterJobsAsync();
foreach (var record in deadLetters)
{
    Console.WriteLine($"Dead Letter Job {record.JobId} ({record.JobType}): {record.FailureReason}");
}
```

---

### 5. Graceful Engine Shutdown

Stop the engine cleanly to allow active worker threads to finish processing:

```csharp
// Drain in-flight jobs and shut down background workers
await processor.StopAsync();
```

---

## Supported Frameworks

- .NET 8
- .NET 9
- .NET 10

---

## Repository Structure

```text
src/
 ├── ConcurrentJobEngine.Core      # Core abstractions, interfaces, enums, and models
 ├── ConcurrentJobEngine           # Scheduler, worker pool, retry pipeline, and DI extensions
 └── ConcurrentJobEngine.Sample    # Practical demonstration application

tests/
 ├── ConcurrentJobEngine.UnitTests        # Isolated component unit tests (75 tests)
 ├── ConcurrentJobEngine.IntegrationTests # High-concurrency integration and stress tests (7 tests)
 └── ConcurrentJobEngine.Benchmarks       # BenchmarkDotNet performance measurement harness
```

---

## Verification & Testing

### Run Tests

```bash
dotnet test
```

### Run Micro-Benchmarks

```bash
dotnet run -c Release --project tests/ConcurrentJobEngine.Benchmarks/ConcurrentJobEngine.Benchmarks.csproj
```

### Run Sample Application

```bash
dotnet run --project src/ConcurrentJobEngine.Sample/ConcurrentJobEngine.Sample.csproj
```

---

## License

ConcurrentJobEngine is licensed under the MIT License.

See the LICENSE file for details.

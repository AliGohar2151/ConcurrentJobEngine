# High-Performance Concurrent Job Processing Engine

# Developer Experience & API Design

**Project:** ConcurrentJobEngine
**Document:** Developer Experience and Public API Design
**Version:** 1.0
**Status:** Active

---

# 1. Purpose

This document defines how developers will interact with the ConcurrentJobEngine.

The goal is to provide a clean, intuitive, strongly typed, and production-oriented API for building applications that need concurrent background job processing.

The API should make common operations simple while keeping advanced capabilities available when required.

The developer experience should follow this principle:

> Easy to start, explicit when needed, powerful when required.

---

# 2. Design Goals

The public API must prioritize:

- Strong typing
- Discoverability
- Minimal configuration
- Clear naming
- Explicit behavior
- Async-first APIs
- Cancellation support
- Testability
- Dependency injection
- Extensibility
- Performance

The API must hide internal implementation details while exposing meaningful extension points.

---

# 3. Basic Developer Experience

A developer should be able to integrate the engine with minimal code.

Expected setup:

```csharp
var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddConcurrentJobEngine(options =>
{
    options.WorkerCount = 8;
    options.QueueCapacity = 1_000;
});

builder.Services.AddJobHandler<ImageProcessingJob, ImageProcessingJobHandler>();
```

The application should then be able to inject:

```csharp
IJobProcessor
```

and submit jobs.

---

# 4. Basic Job Definition

Jobs should be strongly typed.

Example:

```csharp
public sealed record ImageProcessingJob(
    Guid ImageId,
    string InputPath,
    string OutputPath) : IJob;
```

Jobs should preferably be immutable.

Records are recommended when they naturally represent immutable job data.

---

# 5. Job Handler Definition

Handlers should implement:

```csharp
IJobHandler<TJob>
```

Example:

```csharp
public sealed class ImageProcessingJobHandler
    : IJobHandler<ImageProcessingJob>
{
    public async Task<JobResult> HandleAsync(
        ImageProcessingJob job,
        JobExecutionContext context,
        CancellationToken cancellationToken)
    {
        // Business logic

        return JobResult.Success();
    }
}
```

The handler should contain the business logic for the job.

The handler should not know:

- Which worker executes it.
- Which queue contains it.
- How many workers exist.
- How retries are scheduled.
- How the engine shuts down.

---

# 6. Handler Dependency Injection

Handlers should support constructor dependency injection.

Example:

```csharp
public sealed class ImageProcessingJobHandler
    : IJobHandler<ImageProcessingJob>
{
    private readonly IImageProcessor _imageProcessor;
    private readonly ILogger<ImageProcessingJobHandler> _logger;

    public ImageProcessingJobHandler(
        IImageProcessor imageProcessor,
        ILogger<ImageProcessingJobHandler> logger)
    {
        _imageProcessor = imageProcessor;
        _logger = logger;
    }

    public async Task<JobResult> HandleAsync(
        ImageProcessingJob job,
        JobExecutionContext context,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "Processing image {ImageId}",
            job.ImageId);

        await _imageProcessor.ProcessAsync(
            job.InputPath,
            job.OutputPath,
            cancellationToken);

        return JobResult.Success();
    }
}
```

The engine should resolve handlers through the configured dependency injection container.

---

# 7. Job Submission

The primary API for submitting jobs should be asynchronous.

Example:

```csharp
var jobId = await jobProcessor.SubmitAsync(
    new ImageProcessingJob(
        imageId,
        inputPath,
        outputPath),
    cancellationToken);
```

The submission API should return a unique job identifier.

The caller should be able to use this identifier to:

- Query job status.
- Track execution.
- Retrieve results where supported.
- Investigate failures.

---

# 8. Submission Options

Job submission should support optional configuration.

Example:

```csharp
var jobId = await jobProcessor.SubmitAsync(
    job,
    new JobSubmissionOptions
    {
        Priority = JobPriority.High,
        Timeout = TimeSpan.FromMinutes(5)
    },
    cancellationToken);
```

The options object should remain focused on job-specific behavior.

It should not expose internal worker or queue implementation details.

---

# 9. Job Priority

Priority should be expressed through a simple API.

Example:

```csharp
new JobSubmissionOptions
{
    Priority = JobPriority.Critical
}
```

Supported priorities:

```text
Critical
High
Normal
Low
```

Normal priority should be the default.

The API should not require developers to understand the internal scheduling algorithm.

---

# 10. Job Status

Developers should be able to query job status.

Example:

```csharp
var status = await jobProcessor.GetStatusAsync(
    jobId,
    cancellationToken);
```

Expected statuses:

```text
Submitted
Queued
Running
Completed
Failed
Retrying
Cancelled
TimedOut
DeadLettered
```

The returned status should provide enough information for monitoring without exposing internal implementation details.

---

# 11. Job Status Result

A status response may contain:

```csharp
public sealed record JobStatusInfo(
    Guid JobId,
    JobStatus Status,
    DateTimeOffset CreatedAt,
    DateTimeOffset? StartedAt,
    DateTimeOffset? CompletedAt,
    int AttemptCount,
    FailureReason? FailureReason);
```

The API should avoid exposing mutable internal state.

---

# 12. Job Results

The engine should distinguish between:

- Job execution status
- Job execution result
- Job business output

The initial engine may use:

```csharp
JobResult
```

for execution outcomes.

Example:

```csharp
return JobResult.Success();
```

Failure:

```csharp
return JobResult.Failure(
    FailureReason.ValidationError);
```

Future versions may introduce strongly typed job results if required.

The initial API should avoid unnecessary complexity.

---

# 13. Job Execution Context

The execution context provides metadata about the current execution.

Example:

```csharp
public sealed class JobExecutionContext
{
    public Guid JobId { get; }

    public int AttemptNumber { get; }

    public DateTimeOffset StartedAt { get; }

    public JobPriority Priority { get; }
}
```

The context should contain execution metadata rather than business-specific data.

---

# 14. Cancellation

All relevant APIs should accept `CancellationToken`.

Example:

```csharp
await jobProcessor.SubmitAsync(
    job,
    cancellationToken);
```

Handlers must receive cancellation:

```csharp
Task<JobResult> HandleAsync(
    TJob job,
    JobExecutionContext context,
    CancellationToken cancellationToken);
```

Cancellation should propagate naturally through the execution pipeline.

---

# 15. Timeout Configuration

Timeouts should be configured at the job level.

Example:

```csharp
var options = new JobSubmissionOptions
{
    Timeout = TimeSpan.FromSeconds(30)
};
```

The engine should enforce the timeout through cooperative cancellation.

Handlers should not need to manually implement timeout logic.

---

# 16. Retry Configuration

Retry behavior should be configurable at engine level and optionally overridden per job.

Example:

```csharp
builder.Services.AddConcurrentJobEngine(options =>
{
    options.Retry.MaxAttempts = 3;
});
```

Per-job configuration:

```csharp
var options = new JobSubmissionOptions
{
    Retry = new RetryOptions
    {
        MaxAttempts = 5
    }
};
```

The engine should apply sensible defaults.

---

# 17. Retry Policy Extension

Advanced developers should be able to provide a custom retry policy.

Example:

```csharp
builder.Services.AddConcurrentJobEngine(options =>
{
    options.RetryPolicyType = typeof(CustomRetryPolicy);
});
```

Or through dependency injection:

```csharp
services.AddSingleton<IRetryPolicy, CustomRetryPolicy>();
```

The exact API should be finalized during implementation.

The public API must avoid exposing unnecessary internal retry implementation details.

---

# 18. Backoff Strategy

Backoff should be configurable.

Example:

```csharp
options.Retry.BackoffStrategy =
    BackoffStrategy.Exponential;
```

Advanced users may provide custom strategies.

Example:

```csharp
public sealed class CustomBackoffStrategy
    : IBackoffStrategy
{
    public TimeSpan GetDelay(
        int attemptNumber)
    {
        // Custom calculation
    }
}
```

---

# 19. Queue Configuration

Queue behavior should be configurable through engine options.

Example:

```csharp
builder.Services.AddConcurrentJobEngine(options =>
{
    options.QueueCapacity = 10_000;
});
```

The API may support:

```csharp
options.QueueMode = QueueMode.Bounded;
options.QueueFullMode = QueueFullMode.Wait;
```

Possible full modes:

```text
Wait
Reject
```

The public API should clearly communicate the behavior that occurs when capacity is reached.

---

# 20. Worker Configuration

Worker configuration should be simple.

Example:

```csharp
builder.Services.AddConcurrentJobEngine(options =>
{
    options.WorkerCount = 8;
});
```

If worker count is not specified, the engine should provide a sensible default.

The default should be documented.

---

# 21. Worker Scaling

The initial public API should use a fixed worker count.

Dynamic runtime worker scaling should not be part of the initial API unless a clear requirement emerges.

Future support may allow:

```csharp
await jobProcessor.SetWorkerCountAsync(
    16,
    cancellationToken);
```

This should not be implemented prematurely.

---

# 22. Dependency Injection Registration

The primary integration point should be:

```csharp
IServiceCollection.AddConcurrentJobEngine()
```

Basic:

```csharp
services.AddConcurrentJobEngine();
```

Configured:

```csharp
services.AddConcurrentJobEngine(options =>
{
    options.WorkerCount = 8;
    options.QueueCapacity = 10_000;
});
```

The registration method should register all required engine components.

---

# 23. Handler Registration

The API should provide a strongly typed handler registration method.

Example:

```csharp
services.AddJobHandler<
    ImageProcessingJob,
    ImageProcessingJobHandler>();
```

The registration API should validate the handler contract where possible.

---

# 24. Multiple Job Types

The engine should support multiple job types.

Example:

```csharp
services.AddJobHandler<
    ImageProcessingJob,
    ImageProcessingJobHandler>();

services.AddJobHandler<
    EmailNotificationJob,
    EmailNotificationJobHandler>();

services.AddJobHandler<
    ReportGenerationJob,
    ReportGenerationJobHandler>();
```

The same worker pool may process different job types.

---

# 25. Generic Job Processing

The engine should avoid forcing developers to manually specify handler types during submission.

Preferred:

```csharp
await processor.SubmitAsync(
    new EmailNotificationJob(...),
    cancellationToken);
```

The engine should infer the appropriate handler from the job type.

---

# 26. Job Processor API

The primary public abstraction should be:

```csharp
public interface IJobProcessor
{
    Task<Guid> SubmitAsync<TJob>(
        TJob job,
        CancellationToken cancellationToken = default)
        where TJob : IJob;

    Task<Guid> SubmitAsync<TJob>(
        TJob job,
        JobSubmissionOptions options,
        CancellationToken cancellationToken = default)
        where TJob : IJob;

    Task<JobStatusInfo?> GetStatusAsync(
        Guid jobId,
        CancellationToken cancellationToken = default);
}
```

The API should remain minimal.

Additional methods should only be added when justified by actual use cases.

---

# 27. Engine Lifecycle

Engine lifecycle should be managed by the .NET Generic Host where possible.

The engine should integrate with:

```csharp
IHostedService
```

or:

```csharp
BackgroundService
```

This allows the host to manage:

- Startup
- Shutdown
- Cancellation
- Application lifetime

Developers should not normally need to manually call:

```csharp
engine.Start();
engine.Stop();
```

when using the standard .NET hosting model.

---

# 28. Graceful Shutdown

When the host shuts down:

```text
Application Stop
      |
      v
Stop New Submissions
      |
      v
Complete Queue
      |
      v
Process Existing Jobs
      |
      v
Wait for Workers
      |
      v
Shutdown
```

The engine should integrate naturally with:

```csharp
IHostApplicationLifetime
```

and the host cancellation mechanism.

---

# 29. Observability API

The engine should integrate with standard .NET logging.

Developers should configure logging normally:

```csharp
builder.Logging.AddConsole();
```

The engine should use:

```csharp
ILogger<T>
```

No custom logging framework should be required.

---

# 30. Structured Logging

Logs should use structured properties.

Example:

```csharp
_logger.LogInformation(
    "Job {JobId} completed successfully in {DurationMs} ms",
    jobId,
    duration.TotalMilliseconds);
```

Avoid string interpolation in logging calls.

Bad:

```csharp
_logger.LogInformation(
    $"Job {jobId} completed");
```

---

# 31. Metrics API

The engine should expose metrics through standard .NET observability mechanisms where practical.

Potential integration:

```text
System.Diagnostics.Metrics
```

Metrics should be consumable by external monitoring systems without the engine directly depending on a specific vendor.

---

# 32. Recommended Metrics

The initial metric set should include:

```text
jobs.submitted
jobs.completed
jobs.failed
jobs.retried
jobs.cancelled
jobs.timed_out
jobs.dead_lettered

jobs.active

queue.depth

job.queue_duration
job.execution_duration
```

Metric names should be stable and documented.

---

# 33. Tracing

Distributed tracing should not be a mandatory dependency.

The engine should support integration with standard .NET tracing primitives where appropriate.

Potential integration:

```text
System.Diagnostics.Activity
```

Future versions may create activities around:

- Job submission
- Queue wait
- Job execution
- Retry attempts

---

# 34. Error Handling API

The public API should use clear exceptions for programming and configuration errors.

Runtime job failures should be represented through job execution state rather than forcing callers to catch exceptions from the worker thread.

Example:

```text
Submission Error
    |
    v
Exception to Caller

Job Execution Failure
    |
    v
Job State / Retry / Dead Letter
```

---

# 35. Configuration Validation

Invalid engine configuration should fail early.

Examples:

```text
WorkerCount <= 0
QueueCapacity <= 0
Invalid retry attempts
Invalid timeout
Invalid backoff configuration
```

Configuration errors should be detected during application startup where possible.

---

# 36. Options Pattern

Configuration should use the .NET Options pattern.

Example:

```csharp
public sealed class ConcurrentJobEngineOptions
{
    public int WorkerCount { get; set; }

    public int QueueCapacity { get; set; }

    public RetryOptions Retry { get; set; } = new();

    public TimeSpan ShutdownTimeout { get; set; }
}
```

Options should have sensible defaults.

---

# 37. API Naming Rules

Public APIs should follow standard .NET naming conventions.

Use:

```text
PascalCase
```

for:

- Classes
- Interfaces
- Methods
- Properties
- Enums

Use:

```text
I
```

prefix for interfaces.

Examples:

```text
IJobProcessor
IJobHandler<TJob>
IJobQueue<TJob>
IJobExecutor
```

---

# 38. Async Naming

Asynchronous public methods must end with:

```text
Async
```

Examples:

```text
SubmitAsync
GetStatusAsync
ExecuteAsync
HandleAsync
ShutdownAsync
```

---

# 39. Method Design

Methods should have focused responsibilities.

Avoid methods that:

- Submit jobs
- Execute jobs
- Retry jobs
- Update state
- Log metrics

all in one large method.

Prefer clear orchestration between specialized components.

---

# 40. Configuration Defaults

The engine should provide safe defaults.

Potential defaults:

```text
WorkerCount
→ Environment-aware default

QueueCapacity
→ 1,000

QueueFullMode
→ Wait

DefaultPriority
→ Normal

RetryMaxAttempts
→ 3

ShutdownTimeout
→ 30 seconds
```

Exact defaults must be finalized during implementation and documented.

---

# 41. API Surface Minimization

The public API should expose only what consumers need.

Internal components should remain internal where possible.

Avoid exposing:

- Worker implementation classes
- Internal queue implementations
- Internal synchronization objects
- Internal state dictionaries
- Internal execution loops

---

# 42. Extension Points

The following should be designed as extension points where justified:

```text
IJobHandler<TJob>
IRetryPolicy
IBackoffStrategy
IJobStateStore
IDeadLetterStore
IJobScheduler
```

The engine should not make every internal component replaceable.

---

# 43. Testing Developer Experience

The API should be easy to test.

A developer should be able to construct the engine with:

- Fake job handlers
- Fake queues
- Fake retry policies
- Fake state stores

where necessary.

Public APIs should not force developers to start a full Generic Host for simple unit tests.

---

# 44. Example Complete Application

A simple application should look conceptually like:

```csharp
var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddConcurrentJobEngine(options =>
{
    options.WorkerCount = 8;
    options.QueueCapacity = 10_000;
});

builder.Services.AddJobHandler<
    ImageProcessingJob,
    ImageProcessingJobHandler>();

var host = builder.Build();

await host.StartAsync();

var processor =
    host.Services.GetRequiredService<IJobProcessor>();

var jobId = await processor.SubmitAsync(
    new ImageProcessingJob(
        Guid.NewGuid(),
        "input.jpg",
        "output.jpg"));

Console.WriteLine(
    $"Submitted job: {jobId}");

await host.WaitForShutdownAsync();
```

The final API may differ slightly after implementation.

---

# 45. Advanced Developer Experience

Advanced developers should be able to customize:

- Queue capacity
- Worker count
- Priority scheduling
- Retry policy
- Backoff strategy
- Timeout behavior
- State storage
- Dead-letter storage
- Logging
- Metrics

However, customization should not be required for basic usage.

---

# 46. API Design Principles

The public API must follow:

```text
Simple Defaults
       |
       v
Strong Types
       |
       v
Explicit Configuration
       |
       v
Advanced Extension Points
```

Avoid:

```text
Complex Configuration
       |
       v
Required Boilerplate
       |
       v
Internal Implementation Exposure
```

---

# 47. Versioning

Public APIs must be treated as stable contracts.

Breaking changes should:

- Be intentional.
- Be documented.
- Be versioned appropriately.
- Include migration guidance when necessary.

The project should follow semantic versioning when published as a package.

---

# 48. API Review Checklist

Before finalizing a public API, verify:

- Is the API necessary?
- Is the naming idiomatic for .NET?
- Is the API strongly typed?
- Does it support cancellation?
- Is async behavior clear?
- Does it expose implementation details?
- Is it easy to test?
- Is it easy to discover?
- Can the API evolve without breaking consumers?
- Does it have sensible defaults?

---

# 49. Final Developer Experience Goal

A developer should be able to understand the basic engine workflow in minutes:

```text
1. Register the engine
        |
        v
2. Register a job handler
        |
        v
3. Create a job
        |
        v
4. Submit the job
        |
        v
5. Track its status
```

While an advanced developer should be able to customize the engine deeply without modifying its internal source code.

The final developer experience should communicate:

> **Simple to use. Strongly typed. Async-first. Production-ready. Extensible without being over-engineered.**

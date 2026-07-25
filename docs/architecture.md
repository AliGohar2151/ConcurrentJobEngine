# High-Performance Concurrent Job Processing Engine

## System Architecture

**Project:** ConcurrentJobEngine
**Language:** C#
**Platform:** .NET
**Architecture Style:** Modular Layered Architecture
**Processing Model:** In-Process Concurrent Producer-Consumer
**Primary Queue Primitive:** `System.Threading.Channels.Channel<T>`
**Dependency Injection:** Microsoft.Extensions.DependencyInjection

---

# 1. Architectural Overview

The ConcurrentJobEngine is structured as a modular in-process job processing system.

The architecture separates:

- Job submission
- Job scheduling
- Queue management
- Worker management
- Job execution
- Retry policies
- Timeout handling
- Cancellation
- State management
- Dead-letter processing
- Observability

The primary processing pipeline is:

```text
                    +------------------+
                    |     Producer     |
                    |  Application/API |
                    +--------+---------+
                             |
                             | Submit Job
                             v
                    +------------------+
                    |   Job Processor  |
                    +--------+---------+
                             |
                             v
                    +------------------+
                    |  Job Scheduler   |
                    +--------+---------+
                             |
                             v
                    +------------------+
                    |   Queue Manager  |
                    +--------+---------+
                             |
                             v
                    +------------------+
                    |   Worker Pool    |
                    +--------+---------+
                             |
                             v
                    +------------------+
                    |   Job Executor   |
                    +--------+---------+
                             |
                             v
                    +------------------+
                    |   Job Handler    |
                    +--------+---------+
                             |
                             v
                    +------------------+
                    |  Execution Result|
                    +------------------+
```

The execution pipeline is surrounded by cross-cutting concerns:

```text
                 +-----------------------+
                 |  Cancellation         |
                 |  Timeout              |
                 |  Retry Policy         |
                 |  Logging              |
                 |  Metrics              |
                 |  State Management     |
                 +-----------+-----------+
                             |
                             v
                  +--------------------+
                  |  Job Execution     |
                  +--------------------+
```

---

# 2. Architectural Layers

The system is divided into logical layers.

```text
+------------------------------------------------------+
|                  Host / Application                  |
|                                                      |
| ASP.NET Core / Console / Worker Service / Tests      |
+----------------------------+-------------------------+
                             |
                             v
+------------------------------------------------------+
|                Application Layer                     |
|                                                      |
| Job Processor                                        |
| Job Submission                                       |
| Job Orchestration                                    |
+----------------------------+-------------------------+
                             |
                             v
+------------------------------------------------------+
|                 Scheduling Layer                     |
|                                                      |
| Job Scheduler                                        |
| Priority Scheduling                                  |
| Queue Selection                                      |
| Backpressure                                         |
+----------------------------+-------------------------+
                             |
                             v
+------------------------------------------------------+
|                   Worker Layer                       |
|                                                      |
| Worker Pool                                          |
| Worker Lifecycle                                     |
| Concurrency Control                                  |
+----------------------------+-------------------------+
                             |
                             v
+------------------------------------------------------+
|                  Execution Layer                     |
|                                                      |
| Job Executor                                         |
| Handler Resolution                                   |
| Retry                                                |
| Timeout                                              |
| Cancellation                                         |
+----------------------------+-------------------------+
                             |
                             v
+------------------------------------------------------+
|                Infrastructure Layer                  |
|                                                      |
| In-Memory Queue                                      |
| Job State Store                                      |
| Dead Letter Store                                    |
| Logging                                              |
| Metrics                                              |
+------------------------------------------------------+
```

The dependency direction should point toward abstractions and core domain concepts.

Infrastructure implementations should not leak into the core job-processing abstractions.

---

# 3. Core Architectural Components

The primary components are:

```text
IJobProcessor
    |
    +---- IJobScheduler
    |
    +---- IJobQueue
    |
    +---- IWorkerPool
    |
    +---- IJobExecutor
    |
    +---- IJobStateStore
    |
    +---- IDeadLetterStore
    |
    +---- IRetryPolicy
    |
    +---- IBackoffStrategy
```

The components have distinct responsibilities.

```text
Component              Responsibility
--------------------------------------------------------
Job Processor          Public orchestration API
Scheduler              Determines which job is processed
Queue                 Buffers jobs between producers/workers
Worker Pool            Controls concurrent execution
Worker                 Executes the processing loop
Executor               Orchestrates individual job execution
Handler                Executes business-specific logic
Retry Policy           Determines retry behavior
Backoff Strategy       Calculates retry delays
State Store            Tracks job lifecycle state
Dead Letter Store      Stores permanently failed jobs
```

---

# 4. Application Layer

The application layer provides the main entry point into the engine.

The primary abstraction is:

```csharp
IJobProcessor
```

Conceptually:

```text
Application
     |
     | SubmitAsync(job)
     v
IJobProcessor
     |
     v
Job Scheduler
```

The application layer is responsible for orchestration but does not directly perform job execution.

It should not:

- Manage worker threads directly.
- Implement retry logic.
- Execute job handlers.
- Access queue internals.
- Implement scheduling algorithms.

These responsibilities belong to their respective components.

---

# 5. Job Submission Flow

A job submission follows this sequence:

```text
Application
    |
    v
IJobProcessor.SubmitAsync()
    |
    v
Validate Job
    |
    v
Create Job Metadata
    |
    v
Create Job State
    |
    v
Scheduler
    |
    v
Queue
```

The submission operation should remain asynchronous.

The processor should return the job identity or submission result without waiting for the job to finish executing.

Conceptually:

```text
SubmitAsync()
    |
    +----> Job Accepted
    |
    v
Returns Job ID
```

Execution happens independently.

---

# 6. Job Architecture

A job represents a unit of work.

The conceptual structure is:

```text
Job
|
+-- Id
+-- Type
+-- Payload
+-- Priority
+-- CreatedAt
+-- Status
+-- AttemptCount
+-- RetryOptions
+-- Timeout
+-- Metadata
```

The job model contains execution metadata but should not contain business-specific execution logic.

Business behavior belongs to the corresponding handler.

---

# 7. Generic Job Architecture

The engine should support strongly typed jobs and handlers.

Conceptually:

```csharp
IJobHandler<TJob>
```

The relationship is:

```text
TJob
 |
 v
IJobHandler<TJob>
 |
 v
HandleAsync()
```

Example:

```text
ImageProcessingJob
        |
        v
ImageProcessingJobHandler
```

The engine is responsible for locating and invoking the handler.

The handler is responsible for performing the actual business operation.

---

# 8. Job Handler Architecture

Handlers represent application-specific work.

Conceptually:

```text
IJobHandler<TJob>
{
    Task HandleAsync(
        TJob job,
        JobExecutionContext context,
        CancellationToken cancellationToken);
}
```

Handlers may depend on application services through dependency injection.

Example:

```text
ImageProcessingJobHandler
        |
        +---- ImageService
        |
        +---- StorageService
        |
        +---- Logging
```

The engine itself should remain independent of specific business services.

---

# 9. Job Lifecycle Architecture

The job lifecycle is modeled as a state machine.

```text
                    +-----------+
                    | Submitted |
                    +-----+-----+
                          |
                          v
                    +-----------+
                    |  Queued   |
                    +-----+-----+
                          |
                          v
                    +-----------+
                    |  Running  |
                    +-----+-----+
                     /    |    \
                    /     |     \
                   v      v      v
            +---------+ +------+ +-----------+
            |Completed| |Failed| | Cancelled|
            +---------+ +--+---+ +-----------+
                            |
                            v
                       Retry Policy
                            |
                   +--------+--------+
                   |                 |
                   v                 v
                Retrying        Retry Exhausted
                   |                 |
                   v                 v
                Queued          Dead Letter
```

Timeout is represented as an execution outcome:

```text
Running
   |
   v
TimedOut
   |
   +---- Retry
   |
   +---- Failed
   |
   +---- Dead Letter
```

The state store is responsible for persisting the current lifecycle state.

The state machine rules belong to the core domain model.

---

# 10. Scheduler Architecture

The scheduler determines which queued job should be processed next.

```text
                 Job Scheduler
                      |
          +-----------+-----------+
          |           |           |
          v           v           v
      Critical      High       Normal
       Queue        Queue       Queue
          |           |           |
          +-----------+-----------+
                      |
                      v
                  Low Queue
                      |
                      v
                 Worker Pool
```

The scheduler should be independent from the worker implementation.

Conceptually:

```csharp
IJobScheduler
```

The scheduler consumes jobs from one or more queues and provides jobs to workers.

The scheduling algorithm is replaceable.

---

# 11. Priority Queue Architecture

The initial priority model consists of:

```text
Critical
High
Normal
Low
```

Each priority may map to a dedicated queue.

```text
+---------------------+
| Critical Channel    |
+---------------------+

+---------------------+
| High Channel        |
+---------------------+

+---------------------+
| Normal Channel      |
+---------------------+

+---------------------+
| Low Channel         |
+---------------------+
```

The scheduler selects jobs according to its configured scheduling strategy.

The architecture should allow different strategies:

```text
Priority First
Weighted Priority
Round Robin
Priority Aging
```

The scheduler should not assume a specific strategy internally.

---

# 12. Queue Architecture

The initial queue implementation uses:

```csharp
Channel<T>
```

from:

```text
System.Threading.Channels
```

The conceptual queue abstraction is:

```csharp
IJobQueue<T>
```

The initial implementation is:

```text
InMemoryJobQueue<T>
        |
        v
Channel<T>
```

The queue provides asynchronous producer-consumer communication.

```text
Producer 1 ----+
               |
Producer 2 ----+----> Channel<Job> ----> Worker
               |
Producer 3 ----+
```

The queue must support:

- Asynchronous writing.
- Asynchronous reading.
- Completion.
- Bounded capacity.
- Cancellation.

---

# 13. Producer-Consumer Architecture

The engine follows the producer-consumer pattern.

```text
              Producers
                  |
          +-------+-------+
          |       |       |
          v       v       v
       Producer Producer Producer
          |       |       |
          +-------+-------+
                  |
                  v
            Queue Manager
                  |
                  v
          +-------+-------+
          |       |       |
          v       v       v
       Worker  Worker  Worker
```

Producers should not directly invoke workers.

Workers should not directly communicate with producers.

The queue provides the synchronization boundary.

---

# 14. Worker Pool Architecture

The worker pool controls the number of concurrent jobs.

```text
                  Worker Pool
                      |
       +--------------+--------------+
       |              |              |
       v              v              v
   Worker 1       Worker 2       Worker 3
       |              |              |
       v              v              v
     Job A          Job B          Job C
```

Worker count is configurable.

Workers are represented by asynchronous tasks rather than one dedicated OS thread per worker.

Conceptually:

```text
Worker
   |
   v
Wait for Job
   |
   v
Receive Job
   |
   v
Execute Job
   |
   v
Finalize Job
   |
   v
Wait for Next Job
```

A worker must isolate job-level failures.

An exception from a job must not terminate the worker loop.

---

# 15. Worker Lifecycle

A worker has the following lifecycle:

```text
Created
   |
   v
Starting
   |
   v
Running
   |
   +---- Stop Requested
   |
   v
Stopping
   |
   v
Stopped
```

Worker shutdown is coordinated by the worker pool.

The worker pool is responsible for:

- Creating workers.
- Starting workers.
- Tracking workers.
- Requesting shutdown.
- Waiting for workers.
- Handling shutdown cancellation.

---

# 16. Concurrency Model

The engine uses task-based asynchronous concurrency.

Primary primitives:

```text
Task
async / await
CancellationToken
Channel<T>
SemaphoreSlim
ConcurrentDictionary
Interlocked
```

The conceptual model is:

```text
Many Jobs
    |
    v
Worker Pool
    |
    v
Async Tasks
    |
    v
.NET ThreadPool
```

The engine does not create a dedicated OS thread for every job.

Worker count limits concurrent job execution.

---

# 17. Thread Safety Architecture

Shared state must be protected against concurrent access.

Potential shared state includes:

```text
Job State
Active Jobs
Attempt Counters
Metrics Counters
Worker State
Queue State
```

Possible synchronization mechanisms include:

```text
ConcurrentDictionary
Interlocked
Volatile
lock
SemaphoreSlim
Immutable Collections
```

The preferred approach is to minimize shared mutable state.

Operations that involve multiple state changes must be designed as atomic workflows where required.

For example:

```text
Check State
    |
    v
Validate Transition
    |
    v
Update State
```

The entire operation must be safe under concurrent execution.

---

# 18. Job Execution Architecture

The Job Executor orchestrates execution of a single job.

```text
Worker
   |
   v
Job Executor
   |
   +---- Resolve Handler
   |
   +---- Create Execution Context
   |
   +---- Configure Cancellation
   |
   +---- Configure Timeout
   |
   +---- Apply Retry Policy
   |
   v
Execute Handler
```

The executor owns execution orchestration.

The handler owns business logic.

---

# 19. Execution Pipeline

The execution pipeline is:

```text
Job
 |
 v
Load Job State
 |
 v
Mark Running
 |
 v
Resolve Handler
 |
 v
Create Execution Context
 |
 v
Create Linked Cancellation Token
 |
 v
Apply Timeout
 |
 v
Execute Handler
 |
 +---- Success
 |       |
 |       v
 |   Completed
 |
 +---- Cancellation
 |       |
 |       v
 |   Cancelled
 |
 +---- Timeout
 |       |
 |       v
 |   TimedOut
 |
 +---- Exception
         |
         v
     Retry Policy
         |
      +--+--+
      |     |
      v     v
    Retry  Fail
      |     |
      v     v
   Queued  Dead Letter
```

The executor coordinates the complete pipeline.

---

# 20. Cancellation Architecture

Cancellation is cooperative.

The cancellation token flows through the entire execution chain.

```text
Engine
  |
  v
Worker Pool
  |
  v
Worker
  |
  v
Executor
  |
  v
Handler
  |
  v
External Operation
```

Cancellation sources may include:

```text
Engine Shutdown
Job Cancellation
Timeout
```

These sources may be combined using linked cancellation tokens.

```text
Engine Token --------+
                     |
Job Token -----------+----> Linked Token
                     |
Timeout Token -------+
```

The handler receives the resulting cancellation token.

---

# 21. Timeout Architecture

Timeout handling is implemented using cooperative cancellation.

```text
Job
 |
 v
Executor
 |
 v
Timeout CancellationTokenSource
 |
 v
Handler
 |
 +---- Completes
 |
 +---- Cancellation
 |
 +---- Timeout
```

A timeout is distinguished from an explicit cancellation when determining job outcome.

The resulting state is:

```text
TimedOut
```

The retry policy determines whether a timed-out job should be retried.

---

# 22. Retry Architecture

Retry handling is separated from job execution.

```text
Job Executor
      |
      v
Execution Failure
      |
      v
IRetryPolicy
      |
      +---- Retryable
      |       |
      |       v
      |   Backoff Delay
      |       |
      |       v
      |     Queue
      |
      +---- Non-Retryable
              |
              v
         Dead Letter
```

The retry policy determines:

- Whether an error is retryable.
- Whether retry attempts remain.
- The delay before retrying.

The executor is responsible for executing the policy.

---

# 23. Backoff Architecture

Backoff calculation is separated into its own abstraction.

```text
IRetryPolicy
      |
      v
IBackoffStrategy
      |
      +---- Fixed
      |
      +---- Linear
      |
      +---- Exponential
      |
      +---- Exponential + Jitter
```

Example exponential sequence:

```text
Attempt 1 → 1s
Attempt 2 → 2s
Attempt 3 → 4s
Attempt 4 → 8s
```

The actual delay calculation should be encapsulated within the backoff strategy.

---

# 24. Backpressure Architecture

Queues may be bounded.

```text
Producer
    |
    v
Bounded Queue
    |
    +---- Capacity Available
    |          |
    |          v
    |        Accept
    |
    +---- Capacity Full
               |
               v
          Backpressure
```

The queue behavior can be configured.

```text
Full Queue
    |
    +---- Wait
    |
    +---- Reject
```

Backpressure is applied at the queue boundary.

The worker pool does not need to know how producers are throttled.

---

# 25. Dead-Letter Architecture

Jobs that cannot be processed successfully after retry exhaustion are moved to the dead-letter store.

```text
Job Execution
      |
      v
Failure
      |
      v
Retry Policy
      |
      v
Retry Exhausted
      |
      v
Dead Letter Store
```

The abstraction is:

```csharp
IDeadLetterStore
```

The initial implementation is:

```text
InMemoryDeadLetterStore
```

A dead-letter record contains:

```text
Job ID
Job Type
Payload
Failure Reason
Exception
Attempt Count
First Failure Time
Last Failure Time
```

Dead-lettered jobs remain separate from active processing queues.

---

# 26. State Store Architecture

Job state is managed through an abstraction.

```csharp
IJobStateStore
```

Initial implementation:

```text
InMemoryJobStateStore
```

Conceptual operations:

```text
Create
Get
Update
Remove
```

State storage is independent from job execution.

The executor updates state through the state store rather than directly managing a global collection.

Future implementations may provide persistent storage.

---

# 27. Graceful Shutdown Architecture

Shutdown is coordinated across the engine.

```text
Shutdown Requested
        |
        v
Stop Accepting New Jobs
        |
        v
Complete Queue Writers
        |
        v
Drain / Finish Existing Work
        |
        v
Wait for Workers
        |
        v
Shutdown Complete
```

If the shutdown timeout expires:

```text
Shutdown Timeout
        |
        v
Cancel Remaining Work
        |
        v
Stop Workers
        |
        v
Shutdown Complete
```

The shutdown process must coordinate:

```text
Job Processor
Scheduler
Queues
Worker Pool
Workers
```

---

# 28. Dependency Injection Architecture

The engine integrates with .NET dependency injection.

Conceptually:

```text
IServiceCollection
        |
        v
AddConcurrentJobEngine()
        |
        +---- Job Processor
        +---- Scheduler
        +---- Queue
        +---- Worker Pool
        +---- Executor
        +---- Retry Policy
        +---- Backoff Strategy
        +---- State Store
        +---- Dead Letter Store
        +---- Job Handlers
```

Application-specific job handlers are registered with the dependency injection container.

The engine depends on abstractions rather than concrete application services.

---

# 29. Observability Architecture

Observability is implemented through standard .NET abstractions.

Primary logging abstraction:

```text
ILogger<T>
```

The engine emits structured lifecycle events.

```text
EngineStarted
EngineStopping
EngineStopped

WorkerStarted
WorkerStopped

JobSubmitted
JobQueued
JobStarted
JobCompleted
JobFailed
JobRetrying
JobCancelled
JobTimedOut
JobDeadLettered
```

Relevant context may include:

```text
JobId
JobType
WorkerId
AttemptNumber
Priority
Duration
Status
Exception
```

---

# 30. Metrics Architecture

Metrics are collected independently from job execution logic.

Conceptual metrics:

```text
Counters
    |
    +---- Jobs Submitted
    +---- Jobs Completed
    +---- Jobs Failed
    +---- Jobs Retried
    +---- Jobs Cancelled
    +---- Jobs Timed Out
    +---- Jobs Dead Lettered

Gauges
    |
    +---- Active Jobs
    +---- Queue Depth

Histograms
    |
    +---- Queue Wait Duration
    +---- Execution Duration
```

Latency is conceptually divided into:

```text
Total Job Latency
       |
       +---- Queue Wait Time
       |
       +---- Execution Time
```

Metrics collection must minimize contention with the processing pipeline.

---

# 31. Component Interaction

The complete component interaction is:

```text
                       Application
                            |
                            v
                    +---------------+
                    | Job Processor  |
                    +-------+-------+
                            |
                            v
                    +---------------+
                    | Job Scheduler  |
                    +-------+-------+
                            |
             +--------------+--------------+
             |              |              |
             v              v              v
        Critical         High           Normal
         Queue           Queue           Queue
             |              |              |
             +--------------+--------------+
                            |
                            v
                    +---------------+
                    |  Worker Pool  |
                    +-------+-------+
                            |
                            v
                    +---------------+
                    |    Worker     |
                    +-------+-------+
                            |
                            v
                    +---------------+
                    | Job Executor  |
                    +-------+-------+
                            |
                +-----------+-----------+
                |           |           |
                v           v           v
             Retry       Timeout   Cancellation
                |           |           |
                +-----------+-----------+
                            |
                            v
                    +---------------+
                    | Job Handler   |
                    +-------+-------+
                            |
                            v
                    +---------------+
                    |  Job Result   |
                    +---------------+

Supporting Components:

Job Executor
    |
    +---- Job State Store
    |
    +---- Dead Letter Store
    |
    +---- Logger
    |
    +---- Metrics
```

---

# 32. Dependency Relationships

The logical dependency graph is:

```text
                    IJobProcessor
                          |
                          v
                    IJobScheduler
                          |
                          v
                       IJobQueue
                          |
                          v
                      IWorkerPool
                          |
                          v
                      IWorker
                          |
                          v
                     IJobExecutor
                          |
              +-----------+-----------+
              |           |           |
              v           v           v
        IRetryPolicy  Timeout     Cancellation
              |
              v
       IBackoffStrategy

IJobExecutor
      |
      +---- IJobHandler<TJob>
      |
      +---- IJobStateStore
      |
      +---- IDeadLetterStore
      |
      +---- ILogger
      |
      +---- Metrics
```

The dependency graph is designed to keep responsibilities isolated.

---

# 33. Project Structure

The repository is organized into source code, tests, and documentation.

```text
ConcurrentJobEngine/
│
├── src/
│   │
│   ├── ConcurrentJobEngine.Core/
│   │   │
│   │   ├── Abstractions/
│   │   │   ├── IJob.cs
│   │   │   ├── IJobHandler.cs
│   │   │   ├── IJobProcessor.cs
│   │   │   ├── IJobScheduler.cs
│   │   │   ├── IJobQueue.cs
│   │   │   ├── IWorkerPool.cs
│   │   │   ├── IJobExecutor.cs
│   │   │   ├── IRetryPolicy.cs
│   │   │   ├── IBackoffStrategy.cs
│   │   │   ├── IJobStateStore.cs
│   │   │   └── IDeadLetterStore.cs
│   │   │
│   │   ├── Models/
│   │   │   ├── Job.cs
│   │   │   ├── JobOptions.cs
│   │   │   ├── JobExecutionContext.cs
│   │   │   ├── JobResult.cs
│   │   │   └── DeadLetterRecord.cs
│   │   │
│   │   ├── Enums/
│   │   │   ├── JobStatus.cs
│   │   │   ├── JobPriority.cs
│   │   │   └── FailureReason.cs
│   │   │
│   │   └── Exceptions/
│   │       ├── JobExecutionException.cs
│   │       ├── JobRejectedException.cs
│   │       └── JobTimeoutException.cs
│   │
│   ├── ConcurrentJobEngine/
│   │   │
│   │   ├── Processing/
│   │   │   └── JobProcessor.cs
│   │   │
│   │   ├── Scheduling/
│   │   │   ├── JobScheduler.cs
│   │   │   ├── PriorityScheduler.cs
│   │   │   └── SchedulingStrategy.cs
│   │   │
│   │   ├── Queues/
│   │   │   ├── InMemoryJobQueue.cs
│   │   │   ├── PriorityJobQueue.cs
│   │   │   └── QueueOptions.cs
│   │   │
│   │   ├── Workers/
│   │   │   ├── Worker.cs
│   │   │   └── WorkerPool.cs
│   │   │
│   │   ├── Execution/
│   │   │   ├── JobExecutor.cs
│   │   │   ├── JobHandlerResolver.cs
│   │   │   └── JobExecutionPipeline.cs
│   │   │
│   │   ├── Retry/
│   │   │   ├── RetryPolicy.cs
│   │   │   ├── BackoffStrategy.cs
│   │   │   └── JitterStrategy.cs
│   │   │
│   │   ├── Storage/
│   │   │   ├── InMemoryJobStateStore.cs
│   │   │   └── InMemoryDeadLetterStore.cs
│   │   │
│   │   ├── Observability/
│   │   │   ├── JobMetrics.cs
│   │   │   └── JobLogging.cs
│   │   │
│   │   └── DependencyInjection/
│   │       └── ServiceCollectionExtensions.cs
│   │
│   └── ConcurrentJobEngine.Sample/
│       │
│       ├── Jobs/
│       ├── Handlers/
│       ├── Services/
│       └── Program.cs
│
├── tests/
│   │
│   ├── ConcurrentJobEngine.UnitTests/
│   │
│   ├── ConcurrentJobEngine.IntegrationTests/
│   │
│   └── ConcurrentJobEngine.Benchmarks/
│
├── docs/
│   │
│   ├── PRD.md
│   ├── architecture.md
│   ├── rules.md
│   ├── phases.md
│   ├── design.md
│   └── memory.md
│
├── README.md
├── .gitignore
└── ConcurrentJobEngine.sln
```

---

# 34. Core Project Boundary

The Core project contains contracts and domain-level models.

```text
ConcurrentJobEngine.Core
        |
        +---- Interfaces
        +---- Models
        +---- Enums
        +---- Domain Exceptions
```

The Core project should not depend on concrete infrastructure implementations.

---

# 35. Engine Project Boundary

The Engine project contains the concrete implementations.

```text
ConcurrentJobEngine
        |
        +---- Processing
        +---- Scheduling
        +---- Queues
        +---- Workers
        +---- Execution
        +---- Retry
        +---- Storage
        +---- Observability
        +---- Dependency Injection
```

The Engine project implements the abstractions defined by Core.

---

# 36. Sample Application Boundary

The Sample project demonstrates how an external application consumes the engine.

```text
ConcurrentJobEngine.Sample
        |
        +---- Jobs
        +---- Handlers
        +---- Application Services
        |
        v
ConcurrentJobEngine
```

The sample application should not modify engine internals.

It should consume the engine through its public API.

---

# 37. Test Architecture

The testing architecture is separated into three levels.

```text
Unit Tests
    |
    v
Individual Components

Integration Tests
    |
    v
Complete Processing Pipeline

Benchmarks
    |
    v
Performance Characteristics
```

Unit tests focus on isolated behavior.

Integration tests validate component interaction.

Benchmarks measure performance without being treated as functional tests.

---

# 38. Extensibility Boundaries

The architecture defines explicit extension points.

```text
IJobQueue
       |
       +---- InMemoryJobQueue
       +---- Future External Queue

IJobStateStore
       |
       +---- InMemoryJobStateStore
       +---- Future Persistent Store

IDeadLetterStore
       |
       +---- InMemoryDeadLetterStore
       +---- Future Persistent Store

IRetryPolicy
       |
       +---- DefaultRetryPolicy
       +---- CustomRetryPolicy

IBackoffStrategy
       |
       +---- Fixed
       +---- Linear
       +---- Exponential
       +---- Custom
```

External implementations can be introduced without changing the core execution model.

---

# 39. Future Distributed Architecture Boundary

The initial system is in-process.

The architecture leaves a boundary for future distributed evolution.

Current:

```text
Application
     |
     v
In-Process Queue
     |
     v
Worker Pool
```

Potential future:

```text
Application
     |
     v
Distributed Queue
     |
     +----------+----------+
     |          |          |
     v          v          v
 Worker Node  Worker Node  Worker Node
```

The distributed model is not part of the initial implementation.

The current abstractions should not introduce distributed-system complexity prematurely.

---

# 40. Architectural Summary

The final architectural flow is:

```text
                         APPLICATION
                              |
                              v
                     +----------------+
                     | Job Processor  |
                     +-------+--------+
                             |
                             v
                     +----------------+
                     | Job Scheduler  |
                     +-------+--------+
                             |
                             v
                  +-----------------------+
                  |   Priority Queues     |
                  |                       |
                  | Critical | High       |
                  | Normal   | Low        |
                  +-----------+-----------+
                              |
                              v
                     +----------------+
                     |  Worker Pool   |
                     +-------+--------+
                             |
                             v
                     +----------------+
                     |  Job Executor  |
                     +-------+--------+
                             |
               +-------------+-------------+
               |             |             |
               v             v             v
           Cancellation   Timeout       Retry
               |             |             |
               +-------------+-------------+
                             |
                             v
                     +----------------+
                     |  Job Handler   |
                     +-------+--------+
                             |
                 +-----------+-----------+
                 |                       |
                 v                       v
          Job State Store        Dead Letter Store
```

The architecture is centered around one primary principle:

> **Separate job scheduling from job execution, and separate job execution from business logic.**

The resulting system provides clear boundaries between queues, workers, scheduling, execution policies, and application-specific handlers while keeping the initial implementation in-process and extensible for future evolution.

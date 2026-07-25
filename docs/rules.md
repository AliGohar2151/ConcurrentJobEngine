# High-Performance Concurrent Job Processing Engine

# Engineering Rules

**Project:** ConcurrentJobEngine
**Language:** C#
**Platform:** .NET
**Document:** Engineering and AI Development Rules
**Version:** 1.0
**Status:** Active

---

# 1. Purpose

This document defines the engineering standards, coding conventions, architectural boundaries, and development rules that must be followed when building and modifying the ConcurrentJobEngine.

These rules apply to:

- Human developers
- AI coding assistants
- Code generation
- Refactoring
- Bug fixes
- New features
- Tests
- Documentation that describes implementation

The purpose is to ensure that the project remains:

- Maintainable
- Thread-safe
- Testable
- Performant
- Extensible
- Consistent
- Production-oriented

---

# 2. Core Engineering Principles

The project must follow these principles:

1. Prefer simplicity over unnecessary complexity.
2. Prefer explicit behavior over hidden magic.
3. Keep responsibilities narrowly defined.
4. Keep abstractions meaningful.
5. Avoid premature optimization.
6. Measure performance before optimizing.
7. Prefer composition over inheritance.
8. Minimize shared mutable state.
9. Make concurrency behavior explicit.
10. Preserve thread safety as a first-class requirement.
11. Keep public APIs small and stable.
12. Do not introduce dependencies without a clear reason.
13. Do not solve future problems before they exist.
14. Every production feature must have appropriate tests.
15. Every architectural change must be documented when it affects system boundaries.

---

# 3. Technology Rules

The project must use:

- C#
- Modern .NET
- SDK-style projects
- Nullable reference types
- Implicit usings where appropriate
- Built-in dependency injection
- `System.Threading.Channels` for the initial in-memory queue
- `Microsoft.Extensions.Logging` for logging
- `Microsoft.Extensions.DependencyInjection` for dependency injection

The project should prefer built-in .NET functionality before introducing third-party dependencies.

Third-party packages must have a clear justification.

---

# 4. Language Version

The project must use a modern C# language version compatible with the selected .NET version.

Language features should be adopted when they improve:

- Readability
- Safety
- Performance
- Maintainability

Do not use advanced language features merely to demonstrate that they exist.

Code should remain understandable to an experienced .NET developer.

---

# 5. Nullable Reference Types

Nullable reference types must be enabled.

The codebase must not use the null-forgiving operator (`!`) as a routine way to suppress compiler warnings.

Avoid:

```csharp
var value = service.GetValue()!;
```

Prefer:

```csharp
var value = service.GetValue();

if (value is null)
{
    // Handle expected null case
}
```

If `null` is impossible by design, the API should communicate that through its type contract or validation.

---

# 6. Async/Await Rules

All I/O-bound asynchronous operations must use `async` and `await`.

Do not use:

```csharp
.Result
.Wait()
.GetAwaiter().GetResult()
```

inside asynchronous application paths.

Avoid sync-over-async patterns.

Bad:

```csharp
var result = task.Result;
```

Good:

```csharp
var result = await task;
```

Asynchronous methods should normally end with:

```text
Async
```

Examples:

```csharp
SubmitAsync()
ExecuteAsync()
HandleAsync()
ShutdownAsync()
```

---

# 7. CancellationToken Rules

Long-running and asynchronous operations must support cancellation where cancellation is meaningful.

Cancellation tokens must be propagated through the complete execution pipeline.

Expected flow:

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

Do not silently discard a provided `CancellationToken`.

Avoid:

```csharp
CancellationToken.None
```

when a meaningful cancellation token is already available.

Cancellation must be cooperative.

Do not use thread abortion or unsafe forced termination.

---

# 8. Task Rules

Do not create unnecessary tasks.

Avoid wrapping naturally asynchronous operations inside:

```csharp
Task.Run(...)
```

unless there is a documented reason.

Do not use `Task.Run` as a generic solution for concurrency.

The worker pool should control concurrency at the job-processing level.

CPU-bound workloads and I/O-bound workloads should be treated differently.

---

# 9. Concurrency Rules

Concurrency must be intentional.

Every shared mutable resource must have a clearly defined synchronization strategy.

Before modifying shared state, determine:

1. Who can access it?
2. Can multiple threads access it simultaneously?
3. What synchronization mechanism protects it?
4. What happens during shutdown?
5. What happens if an operation fails halfway through?

Prefer:

- Immutable state
- Local state
- Message passing
- Channels
- Concurrent collections
- Atomic operations

over unnecessary locking.

---

# 10. Locking Rules

Use `lock` only when it provides a clear correctness benefit.

Never lock on:

```csharp
this
typeof(SomeType)
string literals
public objects
```

Prefer a private synchronization object when locking is required.

Avoid holding locks while performing:

- I/O
- `await`
- Long-running operations
- External service calls

Never attempt to use `await` inside a traditional `lock` block.

---

# 11. Thread Safety

Thread safety is mandatory for components that are accessed concurrently.

Components expected to handle concurrent access include:

- Job processor
- Job scheduler
- Job queues
- Worker pool
- State store
- Dead-letter store
- Metrics
- Job lifecycle tracking

Thread safety must be demonstrated through tests where appropriate.

Do not assume a class is thread-safe simply because it uses `ConcurrentDictionary`.

Thread safety applies to the complete operation, not only individual collection operations.

---

# 12. Shared State Rules

Minimize global mutable state.

Avoid:

```csharp
public static Dictionary<string, object>
```

for runtime state.

Prefer dependency-injected services with clearly defined lifetimes.

Do not store mutable runtime state in static fields unless there is a documented architectural reason.

---

# 13. Channel Rules

`Channel<T>` is the preferred primitive for asynchronous producer-consumer communication in the initial implementation.

Use channels for:

- Job queues
- Worker communication
- Asynchronous pipelines

Do not introduce custom queue implementations when `Channel<T>` satisfies the requirement.

Channel configuration must explicitly define:

- Bounded or unbounded behavior
- Capacity
- Full-mode behavior
- Single/multiple producer configuration
- Single/multiple consumer configuration

The configuration must match actual usage.

---

# 14. Queue Rules

Queue implementations must not contain business logic.

A queue is responsible for:

- Accepting jobs
- Providing jobs
- Handling capacity
- Supporting completion
- Supporting cancellation

A queue must not:

- Execute jobs
- Retry jobs
- Resolve handlers
- Apply business rules

Those responsibilities belong elsewhere.

---

# 15. Worker Rules

Workers must have a single primary responsibility:

> Consume jobs and execute them through the job execution pipeline.

Workers must not contain:

- Business-specific logic
- Retry policy implementations
- Job-specific handlers
- Direct application service logic

A worker-level exception must not terminate the entire worker pool unless the error represents an unrecoverable engine-level failure.

Job-level failures must be isolated.

---

# 16. Worker Pool Rules

The worker pool controls concurrency.

Worker count must be configurable.

Do not create one dedicated OS thread per job.

The worker pool should use asynchronous tasks.

Worker lifecycle must be explicitly managed.

Workers must be:

- Started intentionally
- Tracked
- Stopped gracefully
- Cancelled when required
- Awaited during shutdown

Do not create fire-and-forget workers without lifecycle management.

---

# 17. Fire-and-Forget Rules

Fire-and-forget tasks are prohibited unless their lifecycle is explicitly managed.

Bad:

```csharp
_ = ProcessJobAsync(job);
```

If a background task must run independently, it must have:

- Explicit ownership
- Error handling
- Cancellation
- Shutdown behavior
- Lifecycle tracking

Unobserved exceptions must not be allowed.

---

# 18. Job Handler Rules

Business logic belongs inside job handlers.

Handlers should implement:

```csharp
IJobHandler<TJob>
```

Handlers must:

- Be focused on one job type
- Accept cancellation
- Use dependency injection
- Avoid managing worker lifecycle
- Avoid directly manipulating queues
- Avoid directly managing retry loops

A handler should not know how the engine schedules jobs.

---

# 19. Job Handler Idempotency

Job handlers should be designed to be idempotent when external side effects are involved.

This is especially important because retries may cause the same logical operation to execute more than once.

Examples of side effects include:

- Database writes
- Sending emails
- Calling external APIs
- File operations
- Payment operations

The engine does not guarantee exactly-once execution.

Handlers must account for retry semantics where necessary.

---

# 20. Retry Rules

Retry behavior must be centralized.

Do not implement custom retry loops inside individual job handlers.

Bad:

```csharp
for (var i = 0; i < 5; i++)
{
    try
    {
        await DoWorkAsync();
        break;
    }
    catch
    {
        // retry
    }
}
```

Retry decisions belong to the retry policy.

The retry system must define:

- Maximum attempts
- Retryable failures
- Non-retryable failures
- Delay strategy

---

# 21. Retry Safety

Do not retry every exception automatically.

Retry only when the failure is considered potentially recoverable.

Examples that may be retryable:

- Temporary network failure
- Temporary database connectivity failure
- Transient external service failure

Examples that may not be retryable:

- Invalid input
- Validation failure
- Unsupported operation
- Permanent configuration error

Retry behavior must be configurable.

---

# 22. Backoff Rules

Retries should use a configurable backoff strategy.

Supported strategies may include:

- Fixed delay
- Linear backoff
- Exponential backoff
- Exponential backoff with jitter

The retry policy should not calculate backoff directly when a separate strategy abstraction is appropriate.

---

# 23. Timeout Rules

Timeouts must use cooperative cancellation.

Do not terminate threads forcefully.

Timeout behavior must be distinguishable from explicit cancellation.

The system should not automatically classify every `OperationCanceledException` as a timeout.

The source of cancellation must be considered.

---

# 24. Exception Rules

Exceptions must be handled at the correct architectural boundary.

Do not use:

```csharp
catch (Exception)
{
}
```

Silent exception swallowing is prohibited.

If an exception is intentionally ignored, the reason must be documented and the behavior must be safe.

Exceptions must not be used for normal control flow when a result type or explicit state is more appropriate.

---

# 25. Exception Handling Hierarchy

The expected hierarchy is:

```text
Job Handler
      |
      v
Job Executor
      |
      +---- Success
      |
      +---- Cancellation
      |
      +---- Timeout
      |
      +---- Failure
               |
               v
          Retry Policy
               |
          +----+----+
          |         |
        Retry     Final Failure
          |         |
          v         v
        Queue    Dead Letter
```

The worker should not independently decide retry behavior.

The executor coordinates execution outcomes.

---

# 26. State Management Rules

Job state transitions must be explicit.

Do not update job state arbitrarily from multiple unrelated components.

State transitions should follow the defined lifecycle.

Invalid transitions should be rejected or safely ignored according to the domain model.

The state store must be thread-safe.

---

# 27. State Transition Rules

The expected state flow is:

```text
Submitted
    |
    v
Queued
    |
    v
Running
    |
    +----> Completed
    |
    +----> Cancelled
    |
    +----> TimedOut
    |
    +----> Failed
              |
              v
           Retrying
              |
              v
            Queued
              |
              v
        DeadLettered
```

State transitions must not bypass lifecycle rules without explicit justification.

---

# 28. Dependency Injection Rules

Use the built-in .NET dependency injection container.

Prefer constructor injection.

Bad:

```csharp
var service = new SomeService();
```

when the service is a registered dependency.

Good:

```csharp
public JobExecutor(
    IJobStateStore stateStore,
    ILogger<JobExecutor> logger)
{
}
```

Do not use the service locator pattern.

Avoid injecting `IServiceProvider` into application services unless dynamic service resolution is genuinely required.

---

# 29. Dependency Lifetime Rules

Dependency lifetimes must be chosen intentionally.

Use:

```text
Singleton
Scoped
Transient
```

according to the actual lifetime requirements.

Components holding shared runtime state may require singleton lifetime.

Job handlers should use appropriate lifetimes based on their dependencies.

Do not register everything as singleton by default.

---

# 30. Abstraction Rules

Introduce an interface when at least one of the following is true:

- Multiple implementations are expected.
- The component represents an architectural boundary.
- The component requires isolated testing.
- The component is a public extension point.

Do not create interfaces for every class automatically.

Avoid meaningless abstractions such as:

```csharp
IUserService
```

with only one implementation when no architectural boundary exists.

---

# 31. Public API Rules

Public APIs must be intentionally designed.

Before exposing a type publicly, determine:

- Is this part of the supported API?
- Will consumers need it?
- Can it be changed later?
- Does it expose internal implementation details?

Avoid exposing:

- Internal queues
- Worker implementation details
- Mutable internal collections
- Synchronization primitives

Public APIs should remain minimal.

---

# 32. API Compatibility

Breaking public API changes must be intentional.

Before changing a public interface:

1. Check all implementations.
2. Check all consumers.
3. Check tests.
4. Check sample applications.
5. Update documentation.

Do not silently break existing APIs.

---

# 33. Logging Rules

Use:

```csharp
ILogger<T>
```

for logging.

Do not use:

```csharp
Console.WriteLine()
```

for engine-level operational logging.

Logs should be structured.

Include relevant context such as:

- Job ID
- Job type
- Attempt number
- Worker ID
- Status
- Duration

Do not log sensitive payload data unless explicitly required.

---

# 34. Logging Levels

Use logging levels appropriately.

```text
Trace
Debug
Information
Warning
Error
Critical
```

Typical examples:

```text
Information → Engine started
Information → Job completed

Debug → Worker processing details

Warning → Retry scheduled
Warning → Queue capacity reached

Error → Job permanently failed

Critical → Engine-level unrecoverable failure
```

Avoid excessive logging inside high-frequency processing loops.

---

# 35. Metrics Rules

Metrics must not significantly affect job-processing performance.

Avoid expensive operations for every metric update.

Prefer efficient atomic counters where appropriate.

Metrics should measure system behavior rather than expose implementation details.

Important metrics include:

- Throughput
- Queue depth
- Active jobs
- Execution latency
- Queue latency
- Retry count
- Failure count

---

# 36. Performance Rules

Do not optimize based on assumptions.

The process must be:

```text
Implement
    |
    v
Measure
    |
    v
Identify Bottleneck
    |
    v
Optimize
    |
    v
Measure Again
```

Performance claims must be supported by benchmarks.

Do not introduce complex concurrency mechanisms without evidence that they improve performance.

---

# 37. Allocation Rules

Avoid unnecessary allocations in high-frequency paths.

Pay particular attention to:

- Job submission
- Queue operations
- Worker loops
- Metrics updates
- Logging

Do not use `Span<T>`, `Memory<T>`, pooling, or unsafe code merely for complexity.

Use advanced optimization techniques only when benchmarks justify them.

---

# 38. Lock-Free and Low-Level Optimization Rules

Lock-free algorithms are not preferred by default.

Do not use:

- `Interlocked`
- `Volatile`
- `Unsafe`
- Custom lock-free structures

unless their correctness and performance benefits are understood.

Correctness comes before micro-optimization.

---

# 39. Error Handling Rules

Errors must be classified appropriately.

The system should distinguish between:

```text
Validation Error
Transient Failure
Permanent Failure
Cancellation
Timeout
Infrastructure Failure
```

Error classification should drive retry and dead-letter behavior.

---

# 40. Testing Rules

Every significant feature must have automated tests.

Tests should cover:

- Expected behavior
- Failure behavior
- Cancellation
- Timeouts
- Retry behavior
- State transitions
- Concurrent access
- Shutdown behavior

Tests must be deterministic where possible.

Avoid tests that depend on arbitrary delays.

---

# 41. Concurrency Testing Rules

Concurrency tests must not rely solely on:

```csharp
Task.Delay(...)
```

to create race conditions.

Prefer synchronization primitives such as:

- `TaskCompletionSource`
- `Barrier`
- `CountdownEvent`
- `ManualResetEventSlim`
- Controlled channels

Tests should intentionally coordinate execution to reproduce concurrency scenarios.

---

# 42. Test Naming Rules

Test names should clearly describe behavior.

Preferred format:

```text
Method_Scenario_ExpectedResult
```

Example:

```text
SubmitAsync_WhenQueueIsFull_RejectsJob
```

Another example:

```text
ExecuteAsync_WhenHandlerFailsAndRetriesRemain_RequeuesJob
```

Tests should communicate the expected behavior without requiring the reader to inspect the implementation.

---

# 43. Test Isolation

Tests must not depend on execution order.

Each test should create its own required state.

Avoid shared mutable state between tests.

Tests should clean up resources they create.

---

# 44. Benchmark Rules

Benchmarks must be isolated from functional tests.

Benchmark projects should measure:

- Submission throughput
- Queue throughput
- Worker scaling
- Job execution throughput
- Allocation behavior
- Latency

Benchmark results must not be treated as universal production guarantees.

Results should include the environment and workload assumptions when documented.

---

# 45. Architecture Boundary Rules

The following dependency direction must be maintained:

```text
Sample Application
        |
        v
ConcurrentJobEngine
        |
        v
ConcurrentJobEngine.Core
```

The Core project must not depend on the concrete Engine project.

Infrastructure implementations must depend on abstractions where appropriate.

Business-specific sample code must not leak into the engine.

---

# 46. Queue Boundary

The queue must not know about:

- Job handlers
- Retry policies
- Business logic
- Application services

The queue only transports jobs.

---

# 47. Scheduler Boundary

The scheduler must not execute job business logic.

The scheduler decides:

> Which job should be processed next?

The executor decides:

> How should this job be executed?

---

# 48. Executor Boundary

The executor must orchestrate execution but must not contain business-specific logic.

The executor is responsible for:

- Handler resolution
- Execution context
- Cancellation
- Timeout
- Retry coordination
- State updates
- Execution outcomes

The handler is responsible for business work.

---

# 49. Handler Boundary

Handlers must not:

- Access internal queues
- Start workers
- Manage retries manually
- Control engine shutdown
- Modify engine internals

Handlers should interact with the application domain and injected services.

---

# 50. Documentation Rules

Documentation must be updated when:

- Public APIs change.
- Architectural boundaries change.
- New extension points are introduced.
- Major design decisions change.

Do not duplicate the same information across multiple documentation files.

Each document has a specific responsibility:

```text
PRD.md
    What and why

architecture.md
    How the system is structured

rules.md
    How the system must be built

phases.md
    In what order the system is built

design.md
    Developer experience and API design

memory.md
    Current project state
```

---

# 51. AI Development Rules

When an AI assistant modifies this project, it must:

1. Read the relevant documentation before making architectural changes.
2. Respect the boundaries defined in `architecture.md`.
3. Follow the engineering rules in this file.
4. Follow the current implementation phase in `phases.md`.
5. Check `memory.md` for the current project state.
6. Avoid modifying unrelated files.
7. Avoid introducing unnecessary dependencies.
8. Avoid rewriting working code without a reason.
9. Explain significant architectural changes.
10. Update tests when behavior changes.
11. Update documentation when public behavior changes.
12. Never assume an unfinished feature is implemented.
13. Never mark a phase complete unless its completion criteria are satisfied.

---

# 52. AI Scope Rules

When asked to implement a feature, the AI must first determine:

```text
Is the feature:
    |
    +---- In current phase?
    |         |
    |         +---- Yes → Implement
    |
    +---- Future phase?
              |
              +---- Do not implement prematurely
```

If a requested change conflicts with the architecture, the AI should identify the conflict before modifying the system.

---

# 53. AI Code Generation Rules

AI-generated code must:

- Compile.
- Follow existing conventions.
- Use existing abstractions when appropriate.
- Avoid duplicate functionality.
- Include required error handling.
- Include cancellation where applicable.
- Include tests for significant behavior.

The AI must not invent APIs that do not exist.

The AI must inspect existing code before creating new abstractions.

---

# 54. AI Refactoring Rules

Before refactoring:

1. Understand current behavior.
2. Identify tests covering the behavior.
3. Determine whether the refactoring changes public APIs.
4. Preserve existing behavior unless the goal is explicitly to change it.
5. Run or reason about relevant tests.

Do not perform large unrelated refactors during feature development.

---

# 55. AI Architecture Rules

The AI must not:

- Add distributed infrastructure prematurely.
- Add databases without a requirement.
- Add external message brokers to the initial implementation.
- Add microservices unnecessarily.
- Introduce unnecessary design patterns.
- Create abstractions solely for theoretical future use.
- Replace `Channel<T>` without a demonstrated requirement.
- Optimize concurrency without benchmarks.

The initial system must remain an in-process engine.

---

# 56. AI Decision-Making Priority

When making implementation decisions, use this priority:

```text
1. Correctness
       |
       v
2. Thread Safety
       |
       v
3. Maintainability
       |
       v
4. Testability
       |
       v
5. Performance
       |
       v
6. Extensibility
       |
       v
7. Convenience
```

Performance must never justify incorrect or unsafe behavior.

---

# 57. Definition of Done

A feature is considered complete only when:

- Implementation is complete.
- Code compiles.
- Relevant tests exist.
- Tests pass.
- Thread-safety concerns are addressed.
- Cancellation is handled where applicable.
- Errors are handled appropriately.
- Logging is added where operationally useful.
- Documentation is updated when necessary.
- No unrelated changes are introduced.

For performance-related features, benchmarks must also be added when appropriate.

---

# 58. Final Engineering Rule

The project should always prefer:

> **Simple, correct, measurable, and maintainable concurrency over clever, fragile, and premature optimization.**

The goal is not to build the most complicated job engine possible.

The goal is to build a technically strong, production-oriented C# system that demonstrates a deep understanding of:

- Asynchronous programming
- Concurrent programming
- Thread safety
- Producer-consumer systems
- Worker pools
- Scheduling
- Backpressure
- Cancellation
- Retry strategies
- Resilience
- Observability
- Performance engineering
- Software architecture

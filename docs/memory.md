# High-Performance Concurrent Job Processing Engine

# Project Memory

**Project:** ConcurrentJobEngine
**Document:** Dynamic Project Memory
**Version:** 1.0
**Status:** Active
**Last Updated:** 2026-07-25

---

# 1. Project Overview

ConcurrentJobEngine is a high-performance, production-oriented concurrent job processing engine built with modern C# and .NET.

The project is designed to demonstrate advanced software engineering skills in:

- Modern C#
- .NET
- Async/Await
- Concurrent programming
- Thread safety
- Producer-consumer architecture
- `System.Threading.Channels`
- Worker pools
- Job scheduling
- Priority processing
- Backpressure
- Cancellation
- Timeouts
- Retry policies
- Exponential backoff
- Jitter
- Dead-letter processing
- Graceful shutdown
- Dependency injection
- Structured logging
- Metrics
- Distributed tracing concepts
- Automated testing
- Concurrency testing
- Benchmarking
- Performance optimization

The project is intended to be both:

1. A technically strong learning project.
2. A professional portfolio project suitable for showcasing advanced C# and .NET skills.

---

# 2. Source of Truth

The following files define the project:

```text
PRD.md
    |
    | What the project should achieve
    v
architecture.md
    |
    | How the system is structured
    v
rules.md
    |
    | How the system must be built
    v
phases.md
    |
    | In what order the system is implemented
    v
design.md
    |
    | How developers interact with the system
    v
memory.md
    |
    | Current state of the project
```

When conflicts occur:

1. Current project requirements take priority.
2. Architecture defines system boundaries.
3. Engineering rules define implementation constraints.
4. Phase roadmap defines implementation order.
5. Design defines public developer experience.
6. This file records current implementation reality.

---

# 3. Current Project Status

```text
Overall Status: Phase 20 Complete
Current Phase: Phase 21 — Integration Testing
Phase Status: Not Started
Current Task: Implement end-to-end integration tests for job pipeline
Next Phase: Phase 22 — Concurrency Testing
```

---

# 4. Phase Progress

| Phase | Name                             | Status      |
| ----- | -------------------------------- | ----------- |
| 1     | Project Foundation               | Completed   |
| 2     | Core Domain & Abstractions       | Completed   |
| 3     | Job Handler System               | Completed   |
| 4     | Basic Job Execution              | Completed   |
| 5     | Queue Infrastructure             | Completed   |
| 6     | Worker Pool                      | Completed   |
| 7     | Concurrent Processing            | Completed   |
| 8     | Job Lifecycle & State Management | Completed   |
| 9     | Priority Scheduling              | Completed   |
| 10    | Backpressure                     | Completed   |
| 11    | Cancellation                     | Completed   |
| 12    | Timeouts                         | Completed   |
| 13    | Retry Policies                   | Completed   |
| 14    | Backoff & Jitter                 | Completed   |
| 15    | Dead-Letter Processing           | Completed   |
| 16    | Graceful Shutdown                | Completed   |
| 17    | Dependency Injection             | Completed   |
| 18    | Logging & Observability          | Completed   |
| 19    | Metrics                          | Completed   |
| 20    | Unit Testing                     | Completed   |
| 21    | Integration Testing              | Not Started |
| 22    | Concurrency Testing              | Not Started |
| 23    | Benchmarking                     | Not Started |
| 24    | Performance Optimization         | Not Started |
| 25    | Sample Application               | Not Started |
| 26    | API & Developer Experience       | Not Started |
| 27    | Packaging & Documentation        | Not Started |
| 28    | Final Portfolio Release          | Not Started |

---

# 5. Current Phase

## Phase 20 — Unit Testing

**Status:** Completed

### Objective

Expand unit test coverage across all domain models, custom exceptions, state store active count boundary filtering, and worker pool edge cases.

### Required Work

- [x] Create `CoreAndEdgeCasesTests.cs` to test domain models, options, and exceptions
- [x] Add tests for active count status filtering in `InMemoryJobStateStore`
- [x] Add tests for `WorkerPool` double start and idle stop
- [x] Verify build and all unit tests pass cleanly (77 tests passing)

### Completion Criteria

- [x] All domain exceptions, options, and model context properties are tested.
- [x] `InMemoryJobStateStore` state transition validation and active count filtering are verified.
- [x] `WorkerPool` double start throws `InvalidOperationException`.

---

# 6. Current Task

```text
Task:
Implement end-to-end integration tests for full job pipeline (Phase 21).

Priority:
High

Status:
Not Started
```

---

# 7. Next Action

The next implementation action is:

```text
Integration Testing (Phase 21)
```

Tasks to complete:
1. Expand `ConcurrentJobEngine.IntegrationTests` with end-to-end lifecycle workflows.
2. Test submission -> scheduling -> execution -> state persistence -> completion via DI container.
3. Test failure retries and dead-letter routing in full integration setup.

---

# 8. Project Structure Status

## Solution

```text
ConcurrentJobEngine.slnx
Status: Created
```

## Core

```text
ConcurrentJobEngine.Core
Status: Created
```

Purpose:

Contains:

- Domain models
- Interfaces
- Contracts
- Core abstractions
- Domain exceptions

Must remain independent of infrastructure.

---

## Engine

```text
ConcurrentJobEngine
Status: Created
```

Purpose:

Contains:

- Queue implementations
- Worker pool
- Scheduler
- Executor
- Retry implementation
- State management
- Dependency injection
- Logging
- Metrics
- Engine lifecycle

---

## Sample

```text
ConcurrentJobEngine.Sample
Status: Created
```

Purpose:

Demonstrates real-world usage of the engine.

Planned workloads:

- Image processing
- Email notifications
- Report generation

---

## Unit Tests

```text
ConcurrentJobEngine.UnitTests
Status: Created
```

Purpose:

Fast isolated tests for core components.

---

## Integration Tests

```text
ConcurrentJobEngine.IntegrationTests
Status: Created
```

Purpose:

Validate complete processing pipelines.

---

## Benchmarks

```text
ConcurrentJobEngine.Benchmarks
Status: Created
```

Purpose:

Measure:

- Throughput
- Latency
- Allocations
- Worker scaling
- Queue performance

---

# 9. Architecture Decisions

## Decision 001 — In-Process Engine

**Status:** Accepted

The initial implementation will be an in-process job processing engine.

The project will not initially depend on:

- RabbitMQ
- Kafka
- Azure Service Bus
- AWS SQS
- Redis
- External job brokers

### Reason

The primary goal is to demonstrate deep understanding of:

- C# concurrency
- Thread safety
- Async processing
- Worker pools
- Scheduling
- Backpressure

External infrastructure can be considered in a future version.

---

## Decision 002 — Channel-Based Queue

**Status:** Accepted

The initial queue implementation will use:

```text
System.Threading.Channels
```

### Reason

Channels provide an efficient and idiomatic .NET primitive for asynchronous producer-consumer workflows.

---

## Decision 003 — Worker Pool

**Status:** Accepted

The engine will use a configurable worker pool.

The system will not create one dedicated OS thread per job.

Workers will process jobs asynchronously.

---

## Decision 004 — Strongly Typed Jobs

**Status:** Accepted

Jobs will be strongly typed using generic handler contracts.

Expected pattern:

```csharp
IJobHandler<TJob>
```

### Reason

This provides:

- Compile-time safety
- Clear APIs
- Better developer experience
- Strong separation between jobs and handlers

---

## Decision 005 — Dependency Injection

**Status:** Accepted

The engine will integrate with the standard .NET dependency injection system.

Primary integration:

```csharp
IServiceCollection
```

The expected registration API is:

```csharp
services.AddConcurrentJobEngine();
```

---

## Decision 006 — Generic Host Integration

**Status:** Accepted

The engine should integrate with the .NET Generic Host.

The engine lifecycle should be managed through standard hosting abstractions.

Potential integration:

```text
IHostedService
BackgroundService
```

---

## Decision 007 — Async-First Design

**Status:** Accepted

Asynchronous APIs are preferred throughout the system.

Blocking patterns such as:

```csharp
.Result
.Wait()
.GetAwaiter().GetResult()
```

are prohibited in asynchronous execution paths.

---

## Decision 008 — CancellationToken

**Status:** Accepted

Cancellation must propagate through the complete execution pipeline.

Expected flow:

```text
Job Processor
      |
      v
Queue
      |
      v
Worker
      |
      v
Executor
      |
      v
Handler
```

---

## Decision 009 — Retry and Backoff Separation

**Status:** Accepted

Retry decisions and retry delays are separate concerns.

Expected abstractions:

```text
IRetryPolicy
IBackoffStrategy
```

The retry policy determines:

> Should this job be retried?

The backoff strategy determines:

> When should the next attempt happen?

---

## Decision 010 — No Premature Optimization

**Status:** Accepted

Performance optimization will follow:

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
Benchmark Again
```

No low-level optimization will be introduced without justification.

---

# 10. Open Architectural Questions

These decisions are intentionally deferred until implementation reveals the actual requirements.

## Question 001

How should multiple job types share queues?

**Status:** Open

Potential approaches:

```text
Option A:
Single heterogeneous queue

Option B:
Queue per job type

Option C:
Priority-aware shared queue
```

Decision will be made during queue implementation.

---

## Question 002

How should priority scheduling interact with bounded channels?

**Status:** Open

Potential approaches include:

- Multiple priority channels.
- Priority scheduler above multiple queues.
- Custom priority queue.

Decision should be based on correctness and benchmark results.

---

## Question 003

Should job results be persisted?

**Status:** Deferred

Initial implementation may use in-memory state.

Persistent storage may be considered after the core engine is complete.

---

## Question 004

Should the engine guarantee at-least-once or at-most-once execution?

**Status:** Open

The final execution semantics must be explicitly documented before release.

Exactly-once execution is not assumed.

---

# 11. Known Limitations

Current project limitations:

```text
1. No persistent storage.
2. No distributed execution.
3. No external message broker.
4. No automatic horizontal scaling.
5. No durable job recovery after process crash.
6. No exactly-once execution guarantee.
7. No production-grade distributed coordination.
```

These limitations are intentional for the initial project scope.

---

# 12. Performance Goals

The project will establish measurable performance goals after the initial implementation.

Performance evaluation will consider:

- Jobs processed per second.
- Submission throughput.
- Queue latency.
- Execution latency.
- P95 latency.
- P99 latency.
- Memory allocations.
- Worker scalability.
- CPU utilization.

Exact targets will be established after baseline benchmarks exist.

---

# 13. Testing Strategy

Testing will be implemented incrementally.

## Unit Tests

Focus on:

- Domain models.
- State transitions.
- Retry policies.
- Backoff strategies.
- Queue behavior.
- Scheduler behavior.

## Integration Tests

Focus on:

- End-to-end processing.
- Worker pool.
- Job execution.
- Retry.
- Timeout.
- Cancellation.
- Dead-letter processing.
- Shutdown.

## Concurrency Tests

Focus on:

- Multiple producers.
- Multiple consumers.
- Race conditions.
- Concurrent state updates.
- Queue contention.
- Shutdown races.

## Benchmarks

Focus on:

- Submission throughput.
- Queue performance.
- Worker scaling.
- Allocation behavior.
- End-to-end throughput.

---

# 14. Development Rules for Memory Updates

This file must be updated whenever:

- A phase is completed.
- A phase starts.
- A major task is completed.
- An architectural decision is made.
- An architectural decision changes.
- A significant bug is discovered.
- A known limitation is identified.
- A major API changes.
- A performance benchmark is completed.

Do not update this file with temporary details that are no longer relevant.

---

# 15. Phase Completion Template

When a phase is completed, update the relevant section using:

```text
Phase:
Name:
Status: Completed

Completed:
- [x] Task
- [x] Task
- [x] Task

Tests:
- [x] Test suite passes
- [x] Integration tests pass

Notes:
- Important implementation detail

Decisions:
- Decision made during phase

Known Issues:
- Issue, if any

Next Phase:
Phase X — Name

Next Task:
Task description
```

---

# 16. Current Development Context

An AI assistant joining the project should understand the following:

```text
Phase 20 — Unit Testing has been completed.

Added CoreAndEdgeCasesTests.cs testing custom exceptions, options defaults, model properties, InMemoryJobStateStore active count filtering across all 7 statuses, and WorkerPool double-start exception handling (77 total tests passing).

The next task is Phase 21:
Integration Testing.

Focus on end-to-end integration tests in ConcurrentJobEngine.IntegrationTests.
```

---

# 17. AI Resume Instructions

When starting a new AI development session:

1. Read `PRD.md`.
2. Read `architecture.md`.
3. Read `rules.md`.
4. Read `phases.md`.
5. Read `design.md`.
6. Read `memory.md`.

Then determine:

```text
Current Phase
      |
      v
Phase Status
      |
      v
Current Task
      |
      v
Completed Work
      |
      v
Remaining Work
      |
      v
Next Action
```

The AI must continue from the current state.

The AI must not assume that planned work has been completed.

The AI must inspect the actual codebase before modifying existing implementation.

---

# 18. Current Project Snapshot

```text
Project:
ConcurrentJobEngine

Language:
C#

Platform:
.NET

Architecture:
In-Process Concurrent Job Processing Engine

Queue:
System.Threading.Channels

Concurrency:
Async Worker Pool

Dependency Injection:
Microsoft.Extensions.DependencyInjection

Logging:
Microsoft.Extensions.Logging

Observability:
System.Diagnostics.Metrics / Activity where appropriate

Testing:
Unit + Integration + Concurrency

Benchmarking:
Dedicated Benchmark Project

Current Phase:
Phase 21 — Integration Testing

Current Status:
Phase 20 Completed / Phase 21 Not Started

Immediate Next Step:
Implement end-to-end integration tests in IntegrationTests project
```

---

# 19. Project Vision

The final project should demonstrate that the developer understands not only how to write C# code, but how to design and engineer a concurrent production-oriented system.

The completed project should communicate expertise in:

```text
C#
 |
 +-- Object-Oriented Design
 |
 +-- Generics
 |
 +-- Async/Await
 |
 +-- Tasks
 |
 +-- Cancellation
 |
 +-- Thread Safety
 |
 +-- Concurrent Collections
 |
 +-- Channels
 |
 +-- Worker Pools
 |
 +-- Scheduling
 |
 +-- Backpressure
 |
 +-- Resilience
 |
 +-- Retry Policies
 |
 +-- Observability
 |
 +-- Testing
 |
 +-- Benchmarking
 |
 +-- Performance Engineering
 |
 +-- .NET Architecture
```

The ultimate goal is to produce a project that is technically credible, well documented, testable, benchmarked, and strong enough to discuss in a professional C#/.NET interview.

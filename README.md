# High-Performance Concurrent Job Processing Engine

A professional, production-oriented, in-process concurrent job processing engine built in modern C# and .NET.

## Key Features

- **Asynchronous Execution**: Native async/await implementation.
- **Concurrent Processing**: Concurrency control via worker pools.
- **Priority-based Scheduling**: Processing jobs according to Critical, High, Normal, or Low priorities.
- **Robust Error Handling & Resilience**: Extensible retry policies, backoff strategies, and jitter.
- **Backpressure Support**: Configurable queue capacities with wait/reject behavior.
- **Cooperative Cancellation & Timeouts**: Complete linked token flow support across the pipeline.
- **Observability**: Integration with standard .NET logging (`ILogger`), structured logs, and metrics.
- **Clean Architecture**: Decoupled domain, engine, and sample boundaries.

## Architecture

The system is designed around a modular in-process producer-consumer pipeline:

```text
Producer (API/Console) ──> Job Processor ──> Job Scheduler ──> Queues ──> Worker Pool ──> Job Executor ──> Job Handler
```

For detailed architectural details, see the [architecture.md](docs/architecture.md) documentation.

## Project Structure

- `src/ConcurrentJobEngine.Core`: Shared domain models, contracts, and core interfaces.
- `src/ConcurrentJobEngine`: Primary execution engine implementation (queues, workers, schedulers).
- `src/ConcurrentJobEngine.Sample`: Practical demonstration application.
- `tests/ConcurrentJobEngine.UnitTests`: Isolated component unit tests.
- `tests/ConcurrentJobEngine.IntegrationTests`: End-to-end integration tests.
- `tests/ConcurrentJobEngine.Benchmarks`: BenchmarkDotNet performance measurements.

## Getting Started

### Prerequisites
- .NET SDK (10.0+ recommended)

### Build the Solution
```bash
dotnet build
```

### Run Tests
```bash
dotnet test
```

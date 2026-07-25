using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ConcurrentJobEngine.Core.Models;

namespace ConcurrentJobEngine.Core.Abstractions;

/// <summary>
/// Exposes the primary public entry point into the job engine for submitting and tracking jobs.
/// </summary>
public interface IJobProcessor
{
    /// <summary>
    /// Submits a job with default options.
    /// </summary>
    Task<Guid> SubmitAsync<TJob>(
        TJob job,
        CancellationToken cancellationToken = default)
        where TJob : IJob;

    /// <summary>
    /// Submits a job with custom options.
    /// </summary>
    Task<Guid> SubmitAsync<TJob>(
        TJob job,
        JobOptions options,
        CancellationToken cancellationToken = default)
        where TJob : IJob;

    /// <summary>
    /// Gets the current status information of a job by ID.
    /// </summary>
    Task<JobStatusInfo?> GetStatusAsync(
        Guid jobId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Requests cancellation of a job by its unique identifier.
    /// </summary>
    Task CancelAsync(
        Guid jobId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves all jobs that have permanently failed and been routed to the dead-letter store.
    /// </summary>
    Task<IReadOnlyList<DeadLetterRecord>> GetDeadLetterJobsAsync(
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Initiates graceful engine shutdown: stops accepting new job submissions, drains in-flight jobs,
    /// and waits up to <see cref="ConcurrentJobEngineOptions.ShutdownTimeout"/> before force-cancelling.
    /// </summary>
    Task StopAsync(CancellationToken cancellationToken = default);
}

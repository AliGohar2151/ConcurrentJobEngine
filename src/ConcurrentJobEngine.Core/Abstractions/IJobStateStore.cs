using System;
using System.Threading;
using System.Threading.Tasks;
using ConcurrentJobEngine.Core.Models;

namespace ConcurrentJobEngine.Core.Abstractions;

/// <summary>
/// Defines operations to manage active states of jobs within the lifecycle.
/// </summary>
public interface IJobStateStore
{
    /// <summary>
    /// Adds a job to the state store, or updates its state if already present.
    /// </summary>
    Task AddOrUpdateAsync(Job job, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves a job by ID. Returns null if not found.
    /// </summary>
    Task<Job?> GetAsync(Guid jobId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Removes a job's metadata from active tracking.
    /// </summary>
    Task RemoveAsync(Guid jobId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the count of jobs currently in active (Submitted, Queued, or Running) states.
    /// </summary>
    Task<int> GetActiveCountAsync(CancellationToken cancellationToken = default);
}

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ConcurrentJobEngine.Core.Models;

namespace ConcurrentJobEngine.Core.Abstractions;

/// <summary>
/// Handles operations to store and query jobs that have permanently failed processing.
/// </summary>
public interface IDeadLetterStore
{
    /// <summary>
    /// Stores a dead-letter job record.
    /// </summary>
    Task AddAsync(DeadLetterRecord record, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves a dead-letter record by its original job ID.
    /// </summary>
    Task<DeadLetterRecord?> GetAsync(Guid jobId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves all permanently failed jobs from the store.
    /// </summary>
    Task<IReadOnlyList<DeadLetterRecord>> GetAllAsync(CancellationToken cancellationToken = default);
}

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ConcurrentJobEngine.Core.Abstractions;
using ConcurrentJobEngine.Core.Models;

namespace ConcurrentJobEngine.Storage;

/// <summary>
/// Thread-safe in-memory implementation of <see cref="IDeadLetterStore"/>.
/// </summary>
public sealed class InMemoryDeadLetterStore : IDeadLetterStore
{
    private readonly ConcurrentDictionary<Guid, DeadLetterRecord> _store = new();

    /// <inheritdoc/>
    public Task AddAsync(DeadLetterRecord record, CancellationToken cancellationToken = default)
    {
        if (record is null) throw new ArgumentNullException(nameof(record));
        _store[record.JobId] = record;
        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public Task<DeadLetterRecord?> GetAsync(Guid jobId, CancellationToken cancellationToken = default)
    {
        _store.TryGetValue(jobId, out var record);
        return Task.FromResult<DeadLetterRecord?>(record);
    }

    /// <inheritdoc/>
    public Task<IReadOnlyList<DeadLetterRecord>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        IReadOnlyList<DeadLetterRecord> records = [.. _store.Values];
        return Task.FromResult(records);
    }
}

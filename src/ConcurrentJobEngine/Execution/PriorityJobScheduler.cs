using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ConcurrentJobEngine.Core.Abstractions;
using ConcurrentJobEngine.Core.Enums;
using ConcurrentJobEngine.Core.Models;

namespace ConcurrentJobEngine.Execution;

/// <summary>
/// Combines scheduling priority and insertion sequence number to prioritize jobs.
/// </summary>
internal readonly struct PriorityKey : IComparable<PriorityKey>
{
    public JobPriority Priority { get; }
    public long SequenceNumber { get; }

    public PriorityKey(JobPriority priority, long sequenceNumber)
    {
        Priority = priority;
        SequenceNumber = sequenceNumber;
    }

    public int CompareTo(PriorityKey other)
    {
        // Reverse standard compare for priority (higher priority comes first)
        int priorityComparison = other.Priority.CompareTo(Priority);
        if (priorityComparison != 0)
        {
            return priorityComparison;
        }

        // Standard compare for sequence number (lower sequence number comes first - FIFO)
        return SequenceNumber.CompareTo(other.SequenceNumber);
    }
}

/// <summary>
/// Compares two PriorityKeys.
/// </summary>
internal sealed class PriorityComparer : IComparer<PriorityKey>
{
    public int Compare(PriorityKey x, PriorityKey y)
    {
        return x.CompareTo(y);
    }
}

/// <summary>
/// A thread-safe scheduler prioritizing Critical/High priority jobs over Normal/Low, falling back to FIFO order for equal priorities.
/// </summary>
public sealed class PriorityJobScheduler : IJobScheduler
{
    private readonly PriorityQueue<Job, PriorityKey> _queue = new(new PriorityComparer());
    private readonly SemaphoreSlim _semaphore = new(0);
    private readonly object _lock = new();
    private long _sequenceNumber;

    /// <inheritdoc />
    public Task ScheduleAsync(Job job, CancellationToken cancellationToken = default)
    {
        if (job is null)
        {
            throw new ArgumentNullException(nameof(job));
        }

        lock (_lock)
        {
            long seq = Interlocked.Increment(ref _sequenceNumber);
            var key = new PriorityKey(job.Priority, seq);
            _queue.Enqueue(job, key);
        }

        _semaphore.Release();
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public async Task<Job> GetNextJobAsync(CancellationToken cancellationToken = default)
    {
        await _semaphore.WaitAsync(cancellationToken);

        lock (_lock)
        {
            return _queue.Dequeue();
        }
    }
}

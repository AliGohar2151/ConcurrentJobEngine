using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using ConcurrentJobEngine.Core.Abstractions;
using ConcurrentJobEngine.Core.Enums;
using ConcurrentJobEngine.Core.Models;

namespace ConcurrentJobEngine.Storage;

/// <summary>
/// Thread-safe in-memory store for active job states, enforcing valid state transition pathways.
/// </summary>
public sealed class InMemoryJobStateStore : IJobStateStore
{
    private readonly ConcurrentDictionary<Guid, Job> _states = new();
    private readonly ConcurrentDictionary<Guid, JobStatus> _lastSavedStatus = new();

    /// <inheritdoc />
    public Task AddOrUpdateAsync(Job job, CancellationToken cancellationToken = default)
    {
        if (job is null)
        {
            throw new ArgumentNullException(nameof(job));
        }

        Guid jobId = job.Id;
        JobStatus newStatus = job.Status;

        if (_lastSavedStatus.TryGetValue(jobId, out var oldStatus))
        {
            if (oldStatus != newStatus)
            {
                ValidateTransition(oldStatus, newStatus);
            }
        }
        else
        {
            if (newStatus != JobStatus.Submitted)
            {
                throw new InvalidOperationException($"Initial state for job {jobId} must be {JobStatus.Submitted}. Cannot start as {newStatus}.");
            }
        }

        _states[jobId] = job;
        _lastSavedStatus[jobId] = newStatus;

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task<Job?> GetAsync(Guid jobId, CancellationToken cancellationToken = default)
    {
        _states.TryGetValue(jobId, out var job);
        return Task.FromResult(job);
    }

    /// <inheritdoc />
    public Task RemoveAsync(Guid jobId, CancellationToken cancellationToken = default)
    {
        _states.TryRemove(jobId, out _);
        _lastSavedStatus.TryRemove(jobId, out _);
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task<int> GetActiveCountAsync(CancellationToken cancellationToken = default)
    {
        int count = 0;
        foreach (var job in _states.Values)
        {
            if (job.Status == JobStatus.Submitted || job.Status == JobStatus.Queued || job.Status == JobStatus.Running)
            {
                count++;
            }
        }
        return Task.FromResult(count);
    }

    private static void ValidateTransition(JobStatus from, JobStatus to)
    {
        bool isValid = (from, to) switch
        {
            (JobStatus.Submitted, JobStatus.Queued) => true,
            (JobStatus.Submitted, JobStatus.Cancelled) => true,
            (JobStatus.Queued, JobStatus.Running) => true,
            (JobStatus.Queued, JobStatus.Cancelled) => true,
            (JobStatus.Running, JobStatus.Completed) => true,
            (JobStatus.Running, JobStatus.Failed) => true,
            (JobStatus.Running, JobStatus.Cancelled) => true,
            (JobStatus.Running, JobStatus.TimedOut) => true,
            (JobStatus.Failed, JobStatus.Queued) => true,
            (JobStatus.TimedOut, JobStatus.Queued) => true,
            
            // Allow same state transitions (no-op)
            _ when from == to => true,
            
            _ => false
        };

        if (!isValid)
        {
            throw new InvalidOperationException($"Invalid job state transition from {from} to {to}.");
        }
    }
}

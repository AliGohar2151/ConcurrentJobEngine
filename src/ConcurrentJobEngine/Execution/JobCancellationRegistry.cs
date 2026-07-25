using System;
using System.Collections.Concurrent;
using System.Threading;

namespace ConcurrentJobEngine.Execution;

/// <summary>
/// Defines a registry for tracking active CancellationTokenSource references associated with executing jobs.
/// </summary>
public interface IJobCancellationRegistry
{
    /// <summary>
    /// Registers a cancellation token source for a job.
    /// </summary>
    void Register(Guid jobId, CancellationTokenSource cts);

    /// <summary>
    /// Unregisters the cancellation token source for a job.
    /// </summary>
    void Unregister(Guid jobId);

    /// <summary>
    /// Triggers cancellation on the registered token source for a job. Returns true if signaled.
    /// </summary>
    bool Cancel(Guid jobId);
}

/// <summary>
/// Thread-safe implementation of IJobCancellationRegistry.
/// </summary>
public sealed class JobCancellationRegistry : IJobCancellationRegistry
{
    private readonly ConcurrentDictionary<Guid, CancellationTokenSource> _registry = new();

    /// <inheritdoc />
    public void Register(Guid jobId, CancellationTokenSource cts)
    {
        if (cts is null)
        {
            throw new ArgumentNullException(nameof(cts));
        }
        _registry[jobId] = cts;
    }

    /// <inheritdoc />
    public void Unregister(Guid jobId)
    {
        _registry.TryRemove(jobId, out _);
    }

    /// <inheritdoc />
    public bool Cancel(Guid jobId)
    {
        if (_registry.TryGetValue(jobId, out var cts))
        {
            try
            {
                cts.Cancel();
                return true;
            }
            catch (ObjectDisposedException)
            {
                // Already disposed/completed
            }
        }
        return false;
    }
}

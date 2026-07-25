using System.Threading;
using System.Threading.Tasks;

namespace ConcurrentJobEngine.Core.Abstractions;

/// <summary>
/// Defines the management pool for controlling concurrent worker tasks.
/// </summary>
public interface IWorkerPool
{
    /// <summary>
    /// Starts all workers in the pool.
    /// </summary>
    Task StartAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gracefully stops and waits for all active workers to finish draining their queues.
    /// </summary>
    Task StopAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the total number of configured workers in the pool.
    /// </summary>
    int WorkerCount { get; }
}

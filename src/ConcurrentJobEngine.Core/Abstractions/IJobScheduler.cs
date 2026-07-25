using System.Threading;
using System.Threading.Tasks;
using ConcurrentJobEngine.Core.Models;

namespace ConcurrentJobEngine.Core.Abstractions;

/// <summary>
/// Dictates how jobs are scheduled and retrieved for worker consumption.
/// </summary>
public interface IJobScheduler
{
    /// <summary>
    /// Enqueues a job into the scheduling pipeline.
    /// </summary>
    Task ScheduleAsync(Job job, CancellationToken cancellationToken = default);

    /// <summary>
    /// Dequeues the next job that is scheduled to run.
    /// </summary>
    Task<Job> GetNextJobAsync(CancellationToken cancellationToken = default);
}

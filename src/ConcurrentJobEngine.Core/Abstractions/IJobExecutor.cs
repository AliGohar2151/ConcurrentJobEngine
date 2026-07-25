using System.Threading;
using System.Threading.Tasks;
using ConcurrentJobEngine.Core.Models;

namespace ConcurrentJobEngine.Core.Abstractions;

/// <summary>
/// Dictates single job execution orchestration, including context creation, handler resolution, and error/retry processing.
/// </summary>
public interface IJobExecutor
{
    /// <summary>
    /// Executes a job metadata record using its designated handler.
    /// </summary>
    /// <param name="job">The job to run.</param>
    /// <param name="cancellationToken">Linked cancellation token from worker/shutdown/job.</param>
    /// <returns>Execution result.</returns>
    Task<JobResult> ExecuteAsync(Job job, CancellationToken cancellationToken = default);
}

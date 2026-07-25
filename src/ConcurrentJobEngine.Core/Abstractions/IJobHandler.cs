using System.Threading;
using System.Threading.Tasks;
using ConcurrentJobEngine.Core.Models;

namespace ConcurrentJobEngine.Core.Abstractions;

/// <summary>
/// Defines the contract for executing business logic associated with a specific job type.
/// </summary>
/// <typeparam name="TJob">The strongly-typed job type.</typeparam>
public interface IJobHandler<in TJob> where TJob : IJob
{
    /// <summary>
    /// Handles the job execution asynchronously.
    /// </summary>
    /// <param name="job">The job payload instance.</param>
    /// <param name="context">The execution context containing metadata about this attempt.</param>
    /// <param name="cancellationToken">Token to monitor for cooperative cancellation.</param>
    /// <returns>A result representing the success or failure of the execution.</returns>
    Task<JobResult> HandleAsync(
        TJob job,
        JobExecutionContext context,
        CancellationToken cancellationToken);
}

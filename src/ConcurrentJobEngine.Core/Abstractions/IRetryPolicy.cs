using System;
using ConcurrentJobEngine.Core.Models;

namespace ConcurrentJobEngine.Core.Abstractions;

/// <summary>
/// Dictates retry logic decision parameters.
/// </summary>
public interface IRetryPolicy
{
    /// <summary>
    /// Evaluates a job execution failure to decide whether it should be retried.
    /// </summary>
    /// <param name="job">The job that failed.</param>
    /// <param name="exception">The exception encountered during handler execution.</param>
    /// <param name="delay">Calculated delay before the next attempt should be queued.</param>
    /// <returns>True if the job should be retried; false if it's a permanent failure.</returns>
    bool ShouldRetry(Job job, Exception exception, out TimeSpan delay);
}

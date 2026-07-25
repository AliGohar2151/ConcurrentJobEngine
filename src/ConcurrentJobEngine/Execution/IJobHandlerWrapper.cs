using System;
using System.Threading;
using System.Threading.Tasks;
using ConcurrentJobEngine.Core.Abstractions;
using ConcurrentJobEngine.Core.Models;

namespace ConcurrentJobEngine.Execution;

/// <summary>
/// Internal non-generic interface to represent a handler execution wrapper.
/// </summary>
internal interface IJobHandlerWrapper
{
    /// <summary>
    /// Executes the wrapped generic handler.
    /// </summary>
    Task<JobResult> HandleAsync(
        IJob job,
        JobExecutionContext context,
        IServiceProvider serviceProvider,
        CancellationToken cancellationToken);
}

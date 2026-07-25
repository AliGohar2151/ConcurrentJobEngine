using System;
using System.Threading;
using System.Threading.Tasks;
using ConcurrentJobEngine.Core.Abstractions;
using ConcurrentJobEngine.Core.Enums;
using ConcurrentJobEngine.Core.Models;
using Microsoft.Extensions.DependencyInjection;

namespace ConcurrentJobEngine.Execution;

/// <summary>
/// Generic wrapper implementation that resolves the strongly typed handler and invokes it.
/// </summary>
/// <typeparam name="TJob">The strongly-typed job type.</typeparam>
internal sealed class JobHandlerWrapperImpl<TJob> : IJobHandlerWrapper where TJob : IJob
{
    public async Task<JobResult> HandleAsync(
        IJob job,
        JobExecutionContext context,
        IServiceProvider serviceProvider,
        CancellationToken cancellationToken)
    {
        var handler = serviceProvider.GetService<IJobHandler<TJob>>();
        if (handler is null)
        {
            return JobResult.Failure(
                FailureReason.HandlerNotFound,
                $"No job handler registered for job type {typeof(TJob).FullName}.");
        }

        try
        {
            return await handler.HandleAsync((TJob)job, context, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            return JobResult.Failure(
                FailureReason.ExecutionFailed,
                $"Exception thrown during handler execution: {ex.Message}",
                ex);
        }
    }
}

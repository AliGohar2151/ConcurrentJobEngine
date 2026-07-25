using System;
using ConcurrentJobEngine.Core.Abstractions;
using ConcurrentJobEngine.Core.Models;
using ConcurrentJobEngine.Diagnostics;
using ConcurrentJobEngine.Execution;
using ConcurrentJobEngine.Storage;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace ConcurrentJobEngine.DependencyInjection;

/// <summary>
/// Provides extension methods to register the ConcurrentJobEngine in a Microsoft.Extensions.DependencyInjection container.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers all engine services using default options.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddConcurrentJobEngine(this IServiceCollection services)
        => services.AddConcurrentJobEngine(static _ => { });

    /// <summary>
    /// Registers all engine services with a configuration delegate.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configure">A delegate to configure <see cref="ConcurrentJobEngineOptions"/>.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddConcurrentJobEngine(
        this IServiceCollection services,
        Action<ConcurrentJobEngineOptions> configure)
    {
        if (services is null) throw new ArgumentNullException(nameof(services));
        if (configure is null) throw new ArgumentNullException(nameof(configure));

        // Options
        services.Configure(configure);

        // Diagnostics
        services.TryAddSingleton<IEngineMetrics, EngineMetrics>();

        // Infrastructure — all singletons: shared queues, state, and registries
        services.TryAddSingleton<IJobStateStore, InMemoryJobStateStore>();
        services.TryAddSingleton<IDeadLetterStore, InMemoryDeadLetterStore>();
        services.TryAddSingleton<IJobScheduler, PriorityJobScheduler>();
        services.TryAddSingleton<IJobCancellationRegistry, JobCancellationRegistry>();

        // Execution
        services.TryAddSingleton<IJobExecutor, JobExecutor>();
        services.TryAddSingleton<IWorkerPool, WorkerPool>();
        services.TryAddSingleton<IJobProcessor, JobProcessor>();

        return services;
    }

    /// <summary>
    /// Registers a strongly typed job handler as a transient service.
    /// </summary>
    /// <typeparam name="TJob">The strongly-typed job payload type.</typeparam>
    /// <typeparam name="THandler">The handler implementation type.</typeparam>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddJobHandler<TJob, THandler>(this IServiceCollection services)
        where TJob : IJob
        where THandler : class, IJobHandler<TJob>
    {
        services.AddTransient<IJobHandler<TJob>, THandler>();
        return services;
    }

    /// <summary>
    /// Registers the in-memory dead-letter store as a singleton.
    /// This is called implicitly by <see cref="AddConcurrentJobEngine(IServiceCollection)"/>
    /// but is exposed for standalone use when not using the full engine registration.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddInMemoryDeadLetterStore(this IServiceCollection services)
    {
        services.TryAddSingleton<IDeadLetterStore, InMemoryDeadLetterStore>();
        return services;
    }
}

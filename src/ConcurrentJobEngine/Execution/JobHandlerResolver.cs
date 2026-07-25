using System;
using System.Collections.Concurrent;
using ConcurrentJobEngine.Core.Abstractions;

namespace ConcurrentJobEngine.Execution;

/// <summary>
/// A thread-safe cache resolver for resolving non-generic wrappers mapping IJob type to its strongly typed handler.
/// </summary>
internal static class JobHandlerResolver
{
    private static readonly ConcurrentDictionary<Type, IJobHandlerWrapper> _wrappers = new();

    /// <summary>
    /// Gets the execution wrapper for a specific job payload type.
    /// </summary>
    /// <param name="jobType">The type of the job payload.</param>
    /// <returns>An instance of IJobHandlerWrapper.</returns>
    public static IJobHandlerWrapper GetWrapper(Type jobType)
    {
        if (!typeof(IJob).IsAssignableFrom(jobType))
        {
            throw new ArgumentException($"Type {jobType.FullName} must implement the {nameof(IJob)} marker interface.");
        }

        return _wrappers.GetOrAdd(jobType, t =>
        {
            var wrapperType = typeof(JobHandlerWrapperImpl<>).MakeGenericType(t);
            return (IJobHandlerWrapper)Activator.CreateInstance(wrapperType)!;
        });
    }
}

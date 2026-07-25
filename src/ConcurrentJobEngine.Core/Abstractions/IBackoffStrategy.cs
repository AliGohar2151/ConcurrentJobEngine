using System;

namespace ConcurrentJobEngine.Core.Abstractions;

/// <summary>
/// Encapsulates exponential, linear, or fixed delay sequence computations.
/// </summary>
public interface IBackoffStrategy
{
    /// <summary>
    /// Computes the time delay to wait before executing a retry attempt.
    /// </summary>
    /// <param name="attemptCount">The attempt count (starting from 1).</param>
    /// <returns>Computed wait duration.</returns>
    TimeSpan GetDelay(int attemptCount);
}

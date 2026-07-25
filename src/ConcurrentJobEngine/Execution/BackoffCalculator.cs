using System;
using ConcurrentJobEngine.Core.Models;

namespace ConcurrentJobEngine.Execution;

/// <summary>
/// Computes retry delay durations using configurable exponential backoff and optional jitter.
/// </summary>
internal static class BackoffCalculator
{
    private static readonly Random _random = new();

    /// <summary>
    /// Computes the delay to wait before the next retry attempt.
    /// </summary>
    /// <param name="options">The retry options containing backoff configuration.</param>
    /// <param name="attemptNumber">The current attempt count (1-based). The first retry is attempt 1.</param>
    /// <returns>A <see cref="TimeSpan"/> representing how long to wait before retrying.</returns>
    public static TimeSpan ComputeDelay(RetryOptions options, int attemptNumber)
    {
        if (options is null)
        {
            return TimeSpan.Zero;
        }

        if (options.InitialDelay <= TimeSpan.Zero)
        {
            return TimeSpan.Zero;
        }

        // Exponential backoff: InitialDelay * BackoffMultiplier^(attemptNumber - 1)
        var exponent = Math.Max(0, attemptNumber - 1);
        var multiplier = Math.Pow(options.BackoffMultiplier, exponent);
        var delaySeconds = options.InitialDelay.TotalSeconds * multiplier;

        // Cap at MaxDelay
        var delay = TimeSpan.FromSeconds(Math.Min(delaySeconds, options.MaxDelay.TotalSeconds));

        if (!options.UseJitter)
        {
            return delay;
        }

        // Apply full jitter: random value in [0, delay]
        var jitteredSeconds = _random.NextDouble() * delay.TotalSeconds;
        return TimeSpan.FromSeconds(jitteredSeconds);
    }
}

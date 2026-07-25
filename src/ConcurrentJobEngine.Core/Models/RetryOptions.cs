namespace ConcurrentJobEngine.Core.Models;

/// <summary>
/// Configuration options for job retries, including exponential backoff and jitter.
/// </summary>
public sealed class RetryOptions
{
    /// <summary>
    /// Gets or sets the maximum number of execution attempts before the job is considered permanently failed.
    /// </summary>
    public int MaxAttempts { get; set; } = 3;

    /// <summary>
    /// Gets or sets the initial delay before the first retry attempt. Defaults to zero (no delay).
    /// </summary>
    public TimeSpan InitialDelay { get; set; } = TimeSpan.Zero;

    /// <summary>
    /// Gets or sets the maximum delay cap, preventing unbounded backoff growth. Defaults to 5 minutes.
    /// </summary>
    public TimeSpan MaxDelay { get; set; } = TimeSpan.FromMinutes(5);

    /// <summary>
    /// Gets or sets the multiplier applied to the delay on each successive attempt. Defaults to 2.0 (exponential).
    /// </summary>
    public double BackoffMultiplier { get; set; } = 2.0;

    /// <summary>
    /// Gets or sets a value indicating whether randomized jitter is applied to the computed delay.
    /// Jitter distributes retry attempts to avoid thundering-herd spikes. Defaults to false.
    /// </summary>
    public bool UseJitter { get; set; } = false;
}

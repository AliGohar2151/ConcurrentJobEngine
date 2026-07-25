using System;
using ConcurrentJobEngine.Core.Models;
using ConcurrentJobEngine.Execution;
using Xunit;

namespace ConcurrentJobEngine.UnitTests;

/// <summary>
/// Unit tests verifying BackoffCalculator delay computation including exponential growth, caps, and jitter.
/// </summary>
public class JobBackoffTests
{
    [Fact]
    public void ComputeDelay_WhenInitialDelayIsZero_ReturnsZero()
    {
        var opts = new RetryOptions
        {
            InitialDelay = TimeSpan.Zero,
            BackoffMultiplier = 2.0,
            MaxDelay = TimeSpan.FromMinutes(5),
            UseJitter = false
        };

        var delay = BackoffCalculator.ComputeDelay(opts, attemptNumber: 1);
        Assert.Equal(TimeSpan.Zero, delay);
    }

    [Fact]
    public void ComputeDelay_WhenNullOptions_ReturnsZero()
    {
        var delay = BackoffCalculator.ComputeDelay(null!, attemptNumber: 1);
        Assert.Equal(TimeSpan.Zero, delay);
    }

    [Fact]
    public void ComputeDelay_FirstAttempt_ReturnsInitialDelay()
    {
        var opts = new RetryOptions
        {
            InitialDelay = TimeSpan.FromSeconds(5),
            BackoffMultiplier = 2.0,
            MaxDelay = TimeSpan.FromMinutes(5),
            UseJitter = false
        };

        // Attempt 1: 5 * 2^0 = 5s
        var delay = BackoffCalculator.ComputeDelay(opts, attemptNumber: 1);
        Assert.Equal(TimeSpan.FromSeconds(5), delay);
    }

    [Fact]
    public void ComputeDelay_SecondAttempt_DoublesInitialDelay()
    {
        var opts = new RetryOptions
        {
            InitialDelay = TimeSpan.FromSeconds(5),
            BackoffMultiplier = 2.0,
            MaxDelay = TimeSpan.FromMinutes(5),
            UseJitter = false
        };

        // Attempt 2: 5 * 2^1 = 10s
        var delay = BackoffCalculator.ComputeDelay(opts, attemptNumber: 2);
        Assert.Equal(TimeSpan.FromSeconds(10), delay);
    }

    [Fact]
    public void ComputeDelay_ThirdAttempt_QuadruplesInitialDelay()
    {
        var opts = new RetryOptions
        {
            InitialDelay = TimeSpan.FromSeconds(5),
            BackoffMultiplier = 2.0,
            MaxDelay = TimeSpan.FromMinutes(5),
            UseJitter = false
        };

        // Attempt 3: 5 * 2^2 = 20s
        var delay = BackoffCalculator.ComputeDelay(opts, attemptNumber: 3);
        Assert.Equal(TimeSpan.FromSeconds(20), delay);
    }

    [Fact]
    public void ComputeDelay_LargeAttempt_IsCappedAtMaxDelay()
    {
        var opts = new RetryOptions
        {
            InitialDelay = TimeSpan.FromSeconds(5),
            BackoffMultiplier = 2.0,
            MaxDelay = TimeSpan.FromSeconds(30),
            UseJitter = false
        };

        // Attempt 10: 5 * 2^9 = 2560s > 30s cap
        var delay = BackoffCalculator.ComputeDelay(opts, attemptNumber: 10);
        Assert.Equal(TimeSpan.FromSeconds(30), delay);
    }

    [Fact]
    public void ComputeDelay_WithJitter_ReturnsValueWithinZeroToComputedRange()
    {
        var opts = new RetryOptions
        {
            InitialDelay = TimeSpan.FromSeconds(10),
            BackoffMultiplier = 2.0,
            MaxDelay = TimeSpan.FromMinutes(5),
            UseJitter = true
        };

        // Attempt 1: base = 10s, jitter = [0, 10s]
        for (var i = 0; i < 50; i++)
        {
            var delay = BackoffCalculator.ComputeDelay(opts, attemptNumber: 1);
            Assert.True(delay >= TimeSpan.Zero, $"Jitter delay must be >= 0 but was {delay}");
            Assert.True(delay <= TimeSpan.FromSeconds(10), $"Jitter delay must be <= base 10s but was {delay}");
        }
    }
}

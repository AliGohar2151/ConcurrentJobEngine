using System;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using ConcurrentJobEngine.Queues;
using Xunit;

namespace ConcurrentJobEngine.UnitTests;

/// <summary>
/// Unit tests verifying InMemoryJobQueue thread-safety, bounded capacity, cancellation, and draining.
/// </summary>
public class InMemoryJobQueueTests
{
    [Fact]
    public async Task UnboundedQueue_ShouldEnqueueAndDequeue()
    {
        var queue = new InMemoryJobQueue<string>();
        
        Assert.Equal(0, queue.Count);

        await queue.EnqueueAsync("Job1");
        await queue.EnqueueAsync("Job2");
        Assert.Equal(2, queue.Count);

        var first = await queue.DequeueAsync();
        Assert.Equal("Job1", first);
        Assert.Equal(1, queue.Count);

        var second = await queue.DequeueAsync();
        Assert.Equal("Job2", second);
        Assert.Equal(0, queue.Count);
    }

    [Fact]
    public void BoundedQueue_TryEnqueue_ShouldReturnFalse_WhenFull()
    {
        var queue = new InMemoryJobQueue<string>(2);

        Assert.True(queue.TryEnqueue("Job1"));
        Assert.True(queue.TryEnqueue("Job2"));
        Assert.False(queue.TryEnqueue("Job3"));
        Assert.Equal(2, queue.Count);
    }

    [Fact]
    public async Task BoundedQueue_EnqueueAsync_ShouldBlock_WhenFull()
    {
        var queue = new InMemoryJobQueue<string>(1);

        await queue.EnqueueAsync("Job1");

        // Start enqueueing a second job in a background task
        var enqueueTask = Task.Run(async () => await queue.EnqueueAsync("Job2"));

        // Give it a moment to run and block
        await Task.Delay(50);
        Assert.False(enqueueTask.IsCompleted);

        // Dequeue first job, which should unblock the background task
        var item1 = await queue.DequeueAsync();
        Assert.Equal("Job1", item1);

        // Verify background task completes now
        await Task.WhenAny(enqueueTask, Task.Delay(500));
        Assert.True(enqueueTask.IsCompleted);

        var item2 = await queue.DequeueAsync();
        Assert.Equal("Job2", item2);
    }

    [Fact]
    public async Task Complete_ShouldPreventNewWrites_ButAllowDraining()
    {
        var queue = new InMemoryJobQueue<string>();

        await queue.EnqueueAsync("Job1");
        await queue.EnqueueAsync("Job2");

        queue.Complete();

        // New writes should fail
        Assert.False(queue.TryEnqueue("Job3"));
        await Assert.ThrowsAsync<ChannelClosedException>(async () => await queue.EnqueueAsync("Job4"));

        // Old writes should still be readable
        Assert.Equal(2, queue.Count);
        Assert.Equal("Job1", await queue.DequeueAsync());
        Assert.Equal("Job2", await queue.DequeueAsync());

        // Once empty and complete, reading should throw
        await Assert.ThrowsAsync<ChannelClosedException>(async () => await queue.DequeueAsync());
    }

    [Fact]
    public async Task DequeueAsync_ShouldCancel_WithCancellationToken()
    {
        var queue = new InMemoryJobQueue<string>();
        using var cts = new CancellationTokenSource();

        var dequeueTask = Task.Run(async () => await queue.DequeueAsync(cts.Token));

        await Task.Delay(50);
        Assert.False(dequeueTask.IsCompleted);

        cts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => await dequeueTask);
    }
}

using System;
using System.Diagnostics.CodeAnalysis;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using ConcurrentJobEngine.Core.Abstractions;

namespace ConcurrentJobEngine.Queues;

/// <summary>
/// A high-performance, thread-safe asynchronous queue built on top of System.Threading.Channels.
/// </summary>
/// <typeparam name="T">The type of item in the queue.</typeparam>
public sealed class InMemoryJobQueue<T> : IJobQueue<T>
{
    private readonly Channel<T> _channel;

    /// <summary>
    /// Initializes a new instance of the <see cref="InMemoryJobQueue{T}"/> class.
    /// </summary>
    /// <param name="capacity">Optional maximum capacity. If null, the queue is unbounded.</param>
    public InMemoryJobQueue(int? capacity = null)
    {
        if (capacity.HasValue)
        {
            if (capacity.Value <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(capacity), "Capacity must be greater than zero.");
            }

            var options = new BoundedChannelOptions(capacity.Value)
            {
                FullMode = BoundedChannelFullMode.Wait,
                SingleReader = false,
                SingleWriter = false
            };
            _channel = Channel.CreateBounded<T>(options);
        }
        else
        {
            var options = new UnboundedChannelOptions
            {
                SingleReader = false,
                SingleWriter = false
            };
            _channel = Channel.CreateUnbounded<T>(options);
        }
    }

    /// <inheritdoc />
    public ValueTask EnqueueAsync(T item, CancellationToken cancellationToken = default)
    {
        return _channel.Writer.WriteAsync(item, cancellationToken);
    }

    /// <inheritdoc />
    public ValueTask<T> DequeueAsync(CancellationToken cancellationToken = default)
    {
        return _channel.Reader.ReadAsync(cancellationToken);
    }

    /// <inheritdoc />
    public void Complete()
    {
        _channel.Writer.Complete();
    }

    /// <inheritdoc />
    public bool TryEnqueue(T item)
    {
        return _channel.Writer.TryWrite(item);
    }

    /// <inheritdoc />
    public bool TryDequeue([MaybeNullWhen(false)] out T item)
    {
        return _channel.Reader.TryRead(out item);
    }

    /// <inheritdoc />
    public int Count => _channel.Reader.Count;
}

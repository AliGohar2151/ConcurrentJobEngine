using System.Diagnostics.CodeAnalysis;
using System.Threading;
using System.Threading.Tasks;

namespace ConcurrentJobEngine.Core.Abstractions;

/// <summary>
/// Defines an asynchronous thread-safe queue primitive.
/// </summary>
/// <typeparam name="T">The type of item stored in the queue.</typeparam>
public interface IJobQueue<T>
{
    /// <summary>
    /// Writes an item to the queue asynchronously, waiting if the queue is full.
    /// </summary>
    ValueTask EnqueueAsync(T item, CancellationToken cancellationToken = default);

    /// <summary>
    /// Reads an item from the queue asynchronously, waiting if the queue is empty.
    /// </summary>
    ValueTask<T> DequeueAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Marks the queue as complete, preventing further writes but allowing remaining items to be read.
    /// </summary>
    void Complete();

    /// <summary>
    /// Attempts to write an item to the queue immediately.
    /// </summary>
    bool TryEnqueue(T item);

    /// <summary>
    /// Attempts to read an item from the queue immediately.
    /// </summary>
    bool TryDequeue([MaybeNullWhen(false)] out T item);

    /// <summary>
    /// Gets the current number of items in the queue.
    /// </summary>
    int Count { get; }
}

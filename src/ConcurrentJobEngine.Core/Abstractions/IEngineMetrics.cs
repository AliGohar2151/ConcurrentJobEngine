namespace ConcurrentJobEngine.Core.Abstractions;

/// <summary>
/// Defines runtime metrics collection contracts for the concurrent job execution engine.
/// </summary>
public interface IEngineMetrics
{
    /// <summary>
    /// Records a new job submission.
    /// </summary>
    void RecordJobSubmitted();

    /// <summary>
    /// Records a successful job execution and its execution duration.
    /// </summary>
    /// <param name="executionDurationSeconds">The execution duration in seconds.</param>
    void RecordJobCompleted(double executionDurationSeconds);

    /// <summary>
    /// Records a terminal job execution failure.
    /// </summary>
    void RecordJobFailed();

    /// <summary>
    /// Records a job retry attempt.
    /// </summary>
    void RecordJobRetried();

    /// <summary>
    /// Records a job cancellation event.
    /// </summary>
    void RecordJobCancelled();

    /// <summary>
    /// Records a job execution timeout event.
    /// </summary>
    void RecordJobTimedOut();

    /// <summary>
    /// Records a job being routed to the dead-letter store.
    /// </summary>
    void RecordJobDeadLettered();

    /// <summary>
    /// Records a job being dequeued and its queue wait duration.
    /// </summary>
    /// <param name="queueWaitDurationSeconds">The time spent in queue in seconds.</param>
    void RecordJobDequeued(double queueWaitDurationSeconds);

    /// <summary>
    /// Increments the active job counter (submitted or running).
    /// </summary>
    void IncrementActiveJobs();

    /// <summary>
    /// Decrements the active job counter.
    /// </summary>
    void DecrementActiveJobs();

    /// <summary>
    /// Increments the queue depth counter.
    /// </summary>
    void IncrementQueueDepth();

    /// <summary>
    /// Decrements the queue depth counter.
    /// </summary>
    void DecrementQueueDepth();
}

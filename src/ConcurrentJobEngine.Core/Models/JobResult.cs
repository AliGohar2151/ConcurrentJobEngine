using System;
using ConcurrentJobEngine.Core.Enums;

namespace ConcurrentJobEngine.Core.Models;

/// <summary>
/// Encapsulates the execution outcome of a job.
/// </summary>
public sealed class JobResult
{
    /// <summary>
    /// Gets a value indicating whether execution was successful.
    /// </summary>
    public bool IsSuccess { get; }

    /// <summary>
    /// Gets the failure classification reason if execution failed.
    /// </summary>
    public FailureReason? FailureReason { get; }

    /// <summary>
    /// Gets an optional detail message about the result.
    /// </summary>
    public string? Message { get; }

    /// <summary>
    /// Gets an optional exception associated with the failure.
    /// </summary>
    public Exception? Exception { get; }

    private JobResult(bool isSuccess, FailureReason? failureReason, string? message, Exception? exception)
    {
        IsSuccess = isSuccess;
        FailureReason = failureReason;
        Message = message;
        Exception = exception;
    }

    /// <summary>
    /// Creates a successful job result.
    /// </summary>
    public static JobResult Success() => new(true, null, null, null);

    /// <summary>
    /// Creates a failed job result.
    /// </summary>
    /// <param name="reason">The classification of failure.</param>
    /// <param name="message">An optional failure message.</param>
    /// <param name="exception">An optional underlying exception.</param>
    public static JobResult Failure(FailureReason reason, string? message = null, Exception? exception = null) =>
        new(false, reason, message, exception);
}

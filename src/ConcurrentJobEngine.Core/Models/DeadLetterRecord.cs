using System;
using ConcurrentJobEngine.Core.Abstractions;
using ConcurrentJobEngine.Core.Enums;

namespace ConcurrentJobEngine.Core.Models;

/// <summary>
/// Represents an immutable record of a job that has failed execution permanently.
/// </summary>
public sealed record DeadLetterRecord(
    Guid JobId,
    string JobType,
    IJob Payload,
    FailureReason FailureReason,
    string? ExceptionDetails,
    int AttemptCount,
    DateTimeOffset FirstFailureTime,
    DateTimeOffset LastFailureTime);

using System;
using ConcurrentJobEngine.Core.Enums;

namespace ConcurrentJobEngine.Core.Models;

/// <summary>
/// Contains status information about a job that can be retrieved by clients.
/// </summary>
public sealed record JobStatusInfo(
    Guid JobId,
    JobStatus Status,
    DateTimeOffset CreatedAt,
    DateTimeOffset? StartedAt,
    DateTimeOffset? CompletedAt,
    int AttemptCount,
    FailureReason? FailureReason);

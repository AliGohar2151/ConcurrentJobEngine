namespace ConcurrentJobEngine.Core.Enums;

/// <summary>
/// Defines the priority levels of a job for scheduling.
/// </summary>
public enum JobPriority
{
    /// <summary>
    /// Low priority. Processed after higher priority jobs.
    /// </summary>
    Low = 0,

    /// <summary>
    /// Normal priority. Default priority level.
    /// </summary>
    Normal,

    /// <summary>
    /// High priority. Processed before Normal/Low priority jobs.
    /// </summary>
    High,

    /// <summary>
    /// Critical priority. Processed with highest urgency.
    /// </summary>
    Critical
}

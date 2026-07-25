namespace ConcurrentJobEngine.Core.Enums;

/// <summary>
/// Defines the priority levels of a job for scheduling.
/// </summary>
public enum JobPriority
{
    Low = 0,
    Normal,
    High,
    Critical
}

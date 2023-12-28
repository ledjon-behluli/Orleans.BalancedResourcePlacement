namespace Orleans.BalancedResourcePlacement;

internal readonly record struct ResourceStatistics(
    float? CpuUsage,
    float? AvailableMemory,
    long? MemoryUsage,
    long? TotalPhysicalMemory,
    bool IsOverloaded);

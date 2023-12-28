using Orleans.Runtime;

namespace Orleans.BalancedResourcePlacement;

internal sealed class AdaptiveBalancedResourcePlacementDirector : BalancedResourcePlacementDirector
{
    private readonly StatisticsFilter<float> cpuUsageFilter = new();
    private readonly StatisticsFilter<float> availableMemoryFilter = new();
    private readonly StatisticsFilter<long> memoryUsageFilter = new();

    public AdaptiveBalancedResourcePlacementDirector(BalancedResourcePlacementOptions options)
        : base(options) { }

    public override void OnSiloStatisticsChanged(SiloAddress address, SiloRuntimeStatistics statistics)
         => siloStatistics.AddOrUpdate(
            key: address,
            addValue: new ResourceStatistics(
                statistics.CpuUsage,
                statistics.AvailableMemory,
                statistics.MemoryUsage,
                statistics.TotalPhysicalMemory,
                statistics.IsOverloaded),
            updateValueFactory: (_, _) =>
            {
                float estimatedCpuUsage = cpuUsageFilter.Filter(statistics.CpuUsage ?? 0);
                float estimatedAvailableMemory = availableMemoryFilter.Filter(statistics.AvailableMemory ?? 0);
                long estimatedMemoryUsage = memoryUsageFilter.Filter(statistics.MemoryUsage ?? 0);

                return new ResourceStatistics(
                    estimatedCpuUsage,
                    estimatedAvailableMemory,
                    estimatedMemoryUsage,
                    statistics.TotalPhysicalMemory,
                    statistics.IsOverloaded);
            });
}

using Orleans.Runtime;

namespace Orleans.BalancedResourcePlacement;

internal interface ISiloStatisticsListener
{
    void OnSiloStatisticsChanged(SiloAddress address, SiloRuntimeStatistics statistics);
    void OnSiloRemoved(SiloAddress address);
}

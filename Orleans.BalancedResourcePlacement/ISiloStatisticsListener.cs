using Orleans.Runtime;

namespace Orleans.BalancedResourcePlacement;

internal interface ISiloStatisticsListener
{
    void OnSiloStatisticsChanged(SiloAddress updatedSilo, SiloRuntimeStatistics newSiloStats);
    void OnSiloRemoved(SiloAddress removedSilo);
}

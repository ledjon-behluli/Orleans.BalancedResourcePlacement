using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Orleans.Runtime;

namespace Orleans.BalancedResourcePlacement;

internal sealed class SiloRuntimeStatisticsCollector : BackgroundService
{
    private readonly IClusterClient clusterClient;
    private readonly ISiloStatisticsListener statisticsListener;
    private readonly TimeSpan collectionPeriod;
    private readonly Dictionary<SiloAddress, SiloRuntimeStatistics> siloStatistics = new();

    public SiloRuntimeStatisticsCollector(
        IClusterClient clusterClient,
        ISiloStatisticsListener statisticsListner,
        IOptions<BalancedResourcePlacementOptions> options)
    {
        this.clusterClient = clusterClient;
        this.statisticsListener = statisticsListner;
        collectionPeriod = (options?.Value ?? BalancedResourcePlacementOptions.Default).ResourceStatisticsCollectionPeriod;
    }

    protected override async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        IManagementGrain grain = clusterClient.GetGrain<IManagementGrain>(0);

        while (!cancellationToken.IsCancellationRequested)
        {
            var hosts = await grain.GetHosts(onlyActive: true);
            var tasks = hosts.Keys.Select(silo => FetchStatisticsForSiloAsync(grain, silo));
            var statistics = await Task.WhenAll(tasks);

            UpdateSiloStatistics(statistics);

            await Task.Delay(collectionPeriod, cancellationToken);
        }
    }

    private static async Task<(SiloAddress, SiloRuntimeStatistics)> FetchStatisticsForSiloAsync(IManagementGrain grain, SiloAddress silo)
    {
        var statistics = await grain.GetRuntimeStatistics(new[] { silo });
        return (silo, statistics.FirstOrDefault());
    }

    private void UpdateSiloStatistics((SiloAddress, SiloRuntimeStatistics)[] newStatistics)
    {
        foreach (var (silo, newStats) in newStatistics)
        {
            if (siloStatistics.TryGetValue(silo, out SiloRuntimeStatistics oldStats))
            {
                if (!AreStatisticsEqual(oldStats, newStats))
                {
                    siloStatistics[silo] = newStats;
                    statisticsListener.OnSiloStatisticsChanged(silo, newStats);
                }
            }
            else
            {
                siloStatistics.Add(silo, newStats);
                statisticsListener.OnSiloStatisticsChanged(silo, newStats);
            }
        }

        var removedSilos = siloStatistics.Keys.Except(newStatistics.Select(pair => pair.Item1)).ToList();
        foreach (var removedSilo in removedSilos)
        {
            siloStatistics.Remove(removedSilo);
            statisticsListener.OnSiloRemoved(removedSilo);
        }
    }

    private static bool AreStatisticsEqual(SiloRuntimeStatistics stats1, SiloRuntimeStatistics stats2)
        => stats1.CpuUsage == stats2.CpuUsage &&
           stats1.AvailableMemory == stats2.AvailableMemory &&
           stats1.MemoryUsage == stats2.MemoryUsage &&
           stats1.TotalPhysicalMemory == stats2.TotalPhysicalMemory;
}
using Microsoft.Extensions.Hosting;
using Orleans.Runtime;

using SiloStatisticsArray = 
    System.Collections.Immutable.ImmutableArray<
        System.ValueTuple<
            Orleans.Runtime.SiloAddress, 
            Orleans.Runtime.SiloRuntimeStatistics>>;

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
        BalancedResourcePlacementOptions options)
    {
        this.clusterClient = clusterClient;
        statisticsListener = statisticsListner;
        collectionPeriod = options.ResourceStatisticsCollectionPeriod;
    }

    protected override async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        IManagementGrain grain = clusterClient.GetGrain<IManagementGrain>(0);

        while (!cancellationToken.IsCancellationRequested)
        {
            var hosts = await grain.GetHosts(onlyActive: true);
            var addresses = hosts.Keys.ToArray();
            var statistics = await grain.GetRuntimeStatistics(addresses);

            // GetRuntimeStatistics returns an ordered SiloRuntimeStatistics array, therefor we can associate the stats-silo
            SiloStatisticsArray array = SiloStatisticsArray.Empty;
            for (int i = 0; i < addresses.Length; i++)
            {
                array = array.Add((addresses[i], statistics[i]));
            }

            UpdateSiloStatistics(array);

            await Task.Delay(collectionPeriod, cancellationToken);
        }
    }

    private void UpdateSiloStatistics(SiloStatisticsArray array)
    {
        foreach (var (silo, stats) in array)
        {
            if (siloStatistics.TryGetValue(silo, out SiloRuntimeStatistics? oldStats))
            {
                if (!AreStatisticsEqual(oldStats, stats))
                {
                    siloStatistics[silo] = stats;
                    statisticsListener.OnSiloStatisticsChanged(silo, stats);
                }
            }
            else
            {
                siloStatistics.Add(silo, stats);
                statisticsListener.OnSiloStatisticsChanged(silo, stats);
            }
        }

        var removedSilos = siloStatistics.Keys.Except(array.Select(x => x.Item1)).ToList();
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
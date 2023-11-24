﻿using Orleans.Runtime;
using Orleans.Runtime.Placement;
using System.Collections.Concurrent;

namespace Orleans.BalancedResourcePlacement;

internal sealed class BalancedResourcePlacementDirector : IPlacementDirector, ISiloStatisticsListener
{
    private readonly ConcurrentDictionary<SiloAddress, SiloRuntimeStatistics> siloStatistics = new();
    private readonly BalancedResourcePlacementOptions options;

    public BalancedResourcePlacementDirector(BalancedResourcePlacementOptions options)
        => this.options = options;

    public Task<SiloAddress> OnAddActivation(PlacementStrategy strategy, PlacementTarget target, IPlacementContext context)
    {
        var compatibleSilos = context.GetCompatibleSilos(target);

        if (IPlacementDirector.GetPlacementHint(target.RequestContextData, compatibleSilos) is { } placementHint)
        {
            return Task.FromResult(placementHint);
        }

        if (compatibleSilos.Length == 0)
        {
            throw new OrleansException($"Cannot place grain with Id = [{target.GrainIdentity}], because there are no compatible silos.");
        }

        //if (compatibleSilos.Length == 1)
        //{
        //    return Task.FromResult(compatibleSilos[0]);
        //}

        if (siloStatistics.IsEmpty)
        {
            return Task.FromResult(RandomSilo(compatibleSilos));
        }

        var scores = new Dictionary<SiloAddress, float>();
        foreach (var silo in compatibleSilos)
        {
            if (siloStatistics.TryGetValue(silo, out var stats))
            {
                float score = stats.IsOverloaded ? 0f : CalculateScore(stats);
                scores[silo] = score;
            }
        }

        var selectedSilo = scores.OrderByDescending(kv => kv.Value).First().Key; // select the silo with the highest score.
        return Task.FromResult(selectedSilo ?? RandomSilo(compatibleSilos));
    }

    private float CalculateScore(SiloRuntimeStatistics stats)
    {
        float normalizedCpuUsage = stats.CpuUsage.HasValue ? stats.CpuUsage.Value / 100f : 0f;

        if (stats.TotalPhysicalMemory.HasValue)
        {
            float normalizedAvailableMemory = stats.AvailableMemory.HasValue ? stats.AvailableMemory.Value / stats.TotalPhysicalMemory.Value : 0f;
            float normalizedMemoryUsage = stats.MemoryUsage.HasValue ? stats.MemoryUsage.Value / stats.TotalPhysicalMemory.Value : 0f;
            float normalizedTotalPhysicalMemory = stats.TotalPhysicalMemory.HasValue ? stats.TotalPhysicalMemory.Value / (1024 * 1024 * 1024) : 0f;

            return (options.CpuUsageWeight * normalizedCpuUsage) +
                   (options.AvailableMemoryWeight * normalizedAvailableMemory) +
                   (options.MemoryUsageWeight * normalizedMemoryUsage) +
                   (options.TotalPhysicalMemoryWeight * normalizedTotalPhysicalMemory);
        }

        return options.CpuUsageWeight * normalizedCpuUsage;
    }

    public void OnSiloStatisticsChanged(SiloAddress address, SiloRuntimeStatistics statistics)
        => siloStatistics.AddOrUpdate(address, statistics, (_, _) => statistics);

    public void OnSiloRemoved(SiloAddress address)
        => siloStatistics.TryRemove(address, out _);

    private static SiloAddress RandomSilo(SiloAddress[] addresses)
        => addresses[Random.Shared.Next(addresses.Length)];
}
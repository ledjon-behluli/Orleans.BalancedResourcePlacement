using Orleans.Runtime;
using Orleans.Runtime.Placement;
using Microsoft.Extensions.DependencyInjection;

namespace Orleans.BalancedResourcePlacement;

public static class BalancedResourcePlacementExtensions
{
    /// <summary>
    /// Register the <see cref="BalancedResourcePlacementStrategy"/> on the <see cref="ISiloBuilder"/>.
    /// </summary>
    /// <param name="siloBuilder">SiloBuilder to register against.</param>
    /// <param name="isGlobal">Specifies wether this placement strategy should be the default one across all activations for any grain type.</param>
    /// <param name="optionsBuilder">An optional builder to change <see cref="BalancedResourcePlacementOptions"/> to be used.</param>
    public static ISiloBuilder AddBalancedResourcePlacement(this ISiloBuilder siloBuilder, bool isGlobal = false, Action<BalancedResourcePlacementOptions>? optionsBuilder = null)
    {
        BalancedResourcePlacementOptions options = new()
        {
            ResourceStatisticsCollectionPeriod = TimeSpan.FromSeconds(5),
            CpuUsageWeight = 0.3f,
            AvailableMemoryWeight = 0.4f,
            MemoryUsageWeight = 0.2f,
            TotalPhysicalMemoryWeight = 0.1f
        };

        optionsBuilder?.Invoke(options);

        if (options.CpuUsageWeight + 
            options.MemoryUsageWeight + 
            options.AvailableMemoryWeight + 
            options.TotalPhysicalMemoryWeight != 100f)
        {
            throw new InvalidOperationException($"Invalid {nameof(BalancedResourcePlacementOptions)} provided. The total sum accross all the weights can not differ from 100.0");
        }

        siloBuilder.Services.AddSingleton(options);

        if (isGlobal)
        {
            siloBuilder.Services.AddSingleton<PlacementStrategy, BalancedResourcePlacementStrategy>();
        }

        siloBuilder.Services.AddSingletonNamedService<PlacementStrategy, BalancedResourcePlacementStrategy>(typeof(BalancedResourcePlacementStrategy).Name);
        siloBuilder.Services.AddSingletonKeyedService<Type, IPlacementDirector, BalancedResourcePlacementDirector>(typeof(BalancedResourcePlacementStrategy));

        siloBuilder.Services.AddSingleton(sp => (ISiloStatisticsListener)sp.GetServiceByKey<Type, IPlacementDirector>(typeof(BalancedResourcePlacementStrategy)));

        siloBuilder.Services.AddHostedService<SiloRuntimeStatisticsCollector>();

        return siloBuilder;
    }
}
using Orleans.Runtime;
using Orleans.Runtime.Placement;
using Microsoft.Extensions.DependencyInjection;
using Orleans.Statistics;
using System.Runtime.InteropServices;
using System.IO;

namespace Orleans.BalancedResourcePlacement;

public static class Extensions
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
            ResourceStatisticsCollectionPeriod = TimeSpan.FromSeconds(1),
            CpuUsageWeight = 0.3f,
            AvailableMemoryWeight = 0.4f,
            MemoryUsageWeight = 0.2f,
            TotalPhysicalMemoryWeight = 0.1f
        };

        optionsBuilder?.Invoke(options);

        if (options.CpuUsageWeight + 
            options.MemoryUsageWeight + 
            options.AvailableMemoryWeight + 
            options.TotalPhysicalMemoryWeight != 1.0f)
        {
            throw new InvalidOperationException($"Invalid {nameof(BalancedResourcePlacementOptions)} provided. The total sum across all the weights must equal 1.0");
        }

        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            throw new NotSupportedException("Neither Orleans nor this package supports collection of resource statistics on OSX.");
        }

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            siloBuilder.Services.AddSingleton<WindowsEnvironmentStatistics>();
            siloBuilder.Services.AddSingleton<IHostEnvironmentStatistics>(sp => sp.GetRequiredService<WindowsEnvironmentStatistics>());
            siloBuilder.Services.AddSingleton<ILifecycleParticipant<ISiloLifecycle>, WindowsEnvironmentStatisticsLifecycleAdapter>();
        }

        if (isGlobal)
        {
            siloBuilder.Services.AddSingleton<PlacementStrategy, BalancedResourcePlacementStrategy>();
        }

        siloBuilder.Services.AddSingleton(options);
        siloBuilder.AddPlacementDirector<BalancedResourcePlacementStrategy, BalancedResourcePlacementDirector>();
        siloBuilder.Services.AddSingleton(sp => (ISiloStatisticsListener)sp.GetRequiredKeyedService<IPlacementDirector>(typeof(BalancedResourcePlacementStrategy)));
        siloBuilder.Services.AddHostedService<SiloRuntimeStatisticsCollector>();

        return siloBuilder;
    }
}
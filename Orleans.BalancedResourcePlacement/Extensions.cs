using Orleans.Runtime;
using Orleans.Runtime.Placement;
using Microsoft.Extensions.DependencyInjection;

namespace Orleans.BalancedResourcePlacement;

public static class Extensions
{
    // todo: take options into consideration
    public static ISiloBuilder AddBalancedResourcePlacement(this ISiloBuilder builder, bool isGlobal = false)
        => builder.ConfigureServices(services => services.AddBalancedResourcePlacement(isGlobal));

    public static IServiceCollection AddBalancedResourcePlacement(this IServiceCollection services, bool isGlobal = false)
    {
        if (isGlobal)
        {
            services.AddSingleton<PlacementStrategy, BalancedResourcePlacementStrategy>();
        }

        services.AddSingletonNamedService<PlacementStrategy, BalancedResourcePlacementStrategy>(typeof(BalancedResourcePlacementStrategy).Name);
        services.AddSingletonKeyedService<Type, IPlacementDirector, BalancedResourcePlacementDirector>(typeof(BalancedResourcePlacementStrategy));

        services.AddSingleton(sp => (ISiloStatisticsListener)sp.GetServiceByKey<Type, IPlacementDirector>(typeof(BalancedResourcePlacementStrategy)));

        services.AddHostedService<SiloRuntimeStatisticsCollector>();

        return services;
    }
}
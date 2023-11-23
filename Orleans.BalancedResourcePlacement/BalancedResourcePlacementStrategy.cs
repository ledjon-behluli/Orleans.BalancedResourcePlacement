using Orleans.Placement;
using Orleans.Runtime;

namespace Orleans.BalancedResourcePlacement;

/// <summary>
/// Marks a grain class as using the <see cref="BalancedResourcePlacementStrategy"/> strategy, which attempts to balance grain placement across servers based upon the utilized cluster resources.
/// </summary>
/// <inheritdoc cref="BalancedResourcePlacementStrategy"/>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
public sealed class BalancedResourcePlacementAttribute : PlacementAttribute
{
    public BalancedResourcePlacementAttribute() :
        base(BalancedResourcePlacementStrategy.Singleton)
    { }
}

/// <summary>
/// A placement strategy which attempts to achieve approximately even load based upon the utilized cluster resources. 
/// It assigns weights to <see cref="SiloRuntimeStatistics"/> to prioritize different properties and calculates a normalized score for each silo.
/// The silo with the highest score is chosen for placing the activation. 
/// Normalization ensures that each property contributes proportionally to the overall score.
/// You can adjust the weights based on your specific requirements and priorities for load balancing.
/// </summary>
/// <remarks>
/// The intention of this placement strategy is to place new grain activations on a server based upon utilized cluster resources.
/// <list type="number">
/// <item><description><b>Cpu usage:</b> The default weight (0.3), indicates that CPU usage is important but not the sole determinant in placement decisions.</description></item>
/// <item><description><b>Available memory:</b> The default weight (0.4), emphasizes the importance of nodes with ample available memory.</description></item>
/// <item><description><b>Memory usage:</b> Is important for understanding the current load on a node. The default weight (0.2), ensures consideration without making it overly influential.</description></item>
/// <item><description><b>Total physical memory:</b> Represents the overall capacity of a node. The default weight (0.1), contributes to a more long-term resource planning perspective.</description></item>
/// </list>
/// This placement strategy is configured by adding the <see cref="BalancedResourcePlacementAttribute"/> attribute to a grain.
/// </remarks>
[Immutable, Serializable, GenerateSerializer, SuppressReferenceTracking]
public sealed class BalancedResourcePlacementStrategy : PlacementStrategy 
{
    internal static readonly BalancedResourcePlacementStrategy Singleton = new();
}

public sealed class BalancedResourcePlacementOptions
{
    public TimeSpan ResourceStatisticsCollectionPeriod { get; set; }

    public float CpuUsageWeight { get; set; }
    public float AvailableMemoryWeight { get; set; }
    public float MemoryUsageWeight { get; set; }
    public float TotalPhysicalMemoryWeight { get; set; }
}

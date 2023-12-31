using Orleans.Placement;
using Orleans.Runtime;

namespace Orleans.BalancedResourcePlacement;

/// <summary>
/// <para>A placement strategy which attempts to achieve approximately even load based on cluster resources. 
/// It assigns weights to <see cref="SiloRuntimeStatistics"/> to prioritize different properties and calculates a normalized score for each silo.
/// The silo with the highest score is chosen for placing the activation. 
/// Normalization ensures that each property contributes proportionally to the overall score.
/// You can adjust the weights based on your specific requirements and priorities for load balancing.</para>
/// <para>In addition to normalization, an online adaptive filter provides a smoothing effect 
/// (filters out high frequency components) and avoids rapid signal drops by transforming it into a polynomial alike decay process.
/// Special emphasis is placed upon the startup of a newly joined silo in relation to the decay process. A polynomial decay closer to being
/// linear is used at the earlier cycles which subsequently transforms into a more exponential alike decay. This contributes to avoiding resource saturation on the silos and especially newly joined silos.</para>
/// </summary>
/// <remarks>
/// Details of the properties used to make the placement decisions and their default values are given below:
/// <list type="number">
/// <item><b>Cpu usage:</b> The default weight (0.3), indicates that CPU usage is important but not the sole determinant in placement decisions.</item>
/// <item><b>Available memory:</b> The default weight (0.4), emphasizes the importance of nodes with ample available memory.</item>
/// <item><b>Memory usage:</b> Is important for understanding the current load on a node. The default weight (0.2), ensures consideration without making it overly influential.</item>
/// <item><b>Total physical memory:</b> Represents the overall capacity of a node. The default weight (0.1), contributes to a more long-term resource planning perspective.</item>
/// </list>
/// </remarks>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
public sealed class BalancedResourcePlacementAttribute : PlacementAttribute
{
    public BalancedResourcePlacementAttribute() :
        base(BalancedResourcePlacementStrategy.Singleton)
    { }
}

[Immutable, Serializable, GenerateSerializer, SuppressReferenceTracking]
internal sealed class BalancedResourcePlacementStrategy : PlacementStrategy 
{
    public static readonly BalancedResourcePlacementStrategy Singleton = new();
}

public sealed class BalancedResourcePlacementOptions
{
    /// <summary>
    /// The period of time to wait until the next resource statistics collection is triggered. 
    /// </summary>
    /// <remarks>Default is 1 second</remarks>
    public TimeSpan ResourceStatisticsCollectionPeriod { get; set; }
    /// <summary>
    /// The importance of the CPU utilization by the silo [percentage].
    /// </summary>
    /// <remarks>Default is 30%</remarks>
    public float CpuUsageWeight { get; set; }
    /// <summary>
    /// The importance of the amount of memory available to the silo [bytes].
    /// </summary>
    /// <remarks>Default is 40%</remarks>
    public float AvailableMemoryWeight { get; set; }
    /// <summary>
    /// The importance of the used memory by the silo [bytes].
    /// </summary>
    /// <remarks>Default is 20%</remarks>
    public float MemoryUsageWeight { get; set; }
    /// <summary>
    /// The importance of the total physical memory available to the silo [bytes].
    /// </summary>
    /// <remarks>Default is 10%</remarks>
    public float TotalPhysicalMemoryWeight { get; set; }
}

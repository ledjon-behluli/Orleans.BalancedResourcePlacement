<p align="center">
  <img src="https://github.com/ledjon-behluli/Orleans.BalancedResourcePlacement/blob/master/OrleansLogo.png" alt="Orleans.BalancedResourcePlacement" width="200px"> 
  <h1>Orleans.BalancedResourcePlacement</h1>
</p>

[![NuGet](https://img.shields.io/nuget/v/Orleans.BalancedResourcePlacement.svg?style=flat)](https://www.nuget.org/packages/Orleans.BalancedResourcePlacement)

This is an Orleans [Grain Placement](https://learn.microsoft.com/en-us/dotnet/orleans/grains/grain-placement) strategy which attempts to achieve approximately even load based on cluster resources. It assigns weights to `SiloRuntimeStatistics` to prioritize different properties and calculates a normalized score for each silo. The silo with the highest score is chosen for placing the activation. Normalization ensures that each property contributes proportionally to the overall score. You can adjust the weights based on your specific requirements and priorities for load balancing.

`ActivationCountBasedPlacement` is useful when you want to achieve an evenly distributed activation count, but that does not mean that the resources are utilized evenly across the silos.
Thats what this package tries to do! It can prove to be useful having different weights for different silos, when talking about [heterogeneous silos](https://learn.microsoft.com/en-us/dotnet/orleans/host/heterogeneous-silos)
where different server resources and different performance characteristics.

> Windows OS statistics are supported also, in addition to Linux stats, which are collected by Orleans itself.

## Usage

Simply apply `AddBalancedResourcePlacement` in the `ISiloBuilder`, specify that it should be the global/default strategy to be used for all activations, and your good to go.

```csharp
Host.CreateDefaultBuilder(args)
    .UseOrleans(builder => builder
        .UseLocalhostClustering()
        . // your other stuff
        .AddBalancedResourcePlacement(isGlobal: true))  // <- this
```

By default `isGlobal` is set to `false`, but you can override it. In case you don't want it to be the default strategy, but still want to use it for certain grain types, you can apply the `BalancedResourcePlacementAttribute` on any grain implementation.

```csharp
[BalancedResourcePlacement] // <-- this
public class EchoGrain : Grain, IEchoGrain
{
    public Task<string> Ping() => Task.FromResult("Pong");
}
```

## Options

You can tweak the `BalancedResourcePlacementOptions` as per your needs, as the `AddBalancedResourcePlacement` comes with an optional options builder. By default, the values you can see below, are the default once.

```csharp

AddBalancedResourcePlacement(optionsBuilder: options =>
{
    options.ResourceStatisticsCollectionPeriod = TimeSpan.FromSeconds(5);
    options.CpuUsageWeight = 0.3f;
    options.AvailableMemoryWeight = 0.4f;
    options.MemoryUsageWeight = 0.2f;
    options.TotalPhysicalMemoryWeight = 0.1f;
});

public sealed class BalancedResourcePlacementOptions
{
    public TimeSpan ResourceStatisticsCollectionPeriod { get; set; }

    public float CpuUsageWeight { get; set; }
    public float AvailableMemoryWeight { get; set; }
    public float MemoryUsageWeight { get; set; }
    public float TotalPhysicalMemoryWeight { get; set; }
}
```

## Notes

* If only 1 compatible silo is available in the cluster, than by default that silo is picked as the target of activation. Also, since statistics are not available straightaway, or 2 silos evaluate to the same score, than random placement is used for the activation of the grain.
* The total sum across all the weights must equal **1.0**, otherwise an `InvalidOperationException` is throw upon startup.
* Orleans, performs statistics collection for RAM, and CPU only on Linux OS. This package <u>adds support for Windows OS too</u>. In case your silo(s) are hosted on OSX, than a `NotSupportedException` is throw upon startup.

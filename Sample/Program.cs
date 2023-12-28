using NS;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Orleans.BalancedResourcePlacement;

var host = Host.CreateDefaultBuilder(args)
    .UseOrleans(builder =>
        builder
            .UseLocalhostClustering()
            .AddBalancedResourcePlacement(
                isGlobal: false, // set to 'true', if you want this strategy to apply globally
                optionsBuilder: builder =>
                {
                    builder.UseAdaptiveFiltering = false;  // set to 'true', if you want to use adaptiv filtering
                }))
    .ConfigureLogging(builder => builder.AddConsole())
    .Build();

await host.StartAsync();

var grainFactory = host.Services.GetRequiredService<IGrainFactory>();
int id = 0;

while (id < 100)
{
    var result = await grainFactory.GetGrain<IEchoGrain>(id).Ping();
    Console.WriteLine(result);
    id++;
    await Task.Delay(500);
}

Console.WriteLine("Orleans is running.\nPress Enter to terminate...");
Console.ReadLine();
Console.WriteLine("Orleans is stopping...");

await host.StopAsync();

namespace NS
{
    public interface IEchoGrain : IGrainWithIntegerKey
    {
        Task<string> Ping();
    }

    [BalancedResourcePlacement] // not needed if 'isGlobal = true'
    public class EchoGrain : Grain, IEchoGrain
    {
        public Task<string> Ping() => Task.FromResult("Pong");
    }
}
using Bogus;
using Microsoft.Extensions.Options;
using Orleans.BalancedResourcePlacement;
using Orleans.Configuration;
using Orleans.Runtime;
using Orleans.Statistics;
using Spectre.Console;
using System.Reflection;

var filter = new SiloRuntimeStatisticsFilter();
var faker = new Faker();
var table = new Table();

table.AddColumn("Iteration");
table.AddColumn("Simulated (CpuUsage | AvailableMemory | MemoryUsage)");
table.AddColumn("Filtered (CpuUsage | AvailableMemory | MemoryUsage)");
table.AddColumn("Difference (%)");

for (int i = 0; i < 50; i++)
{
    var simulatedStats = CreateSiloRuntimeStatistics(faker);
    var filteredStats = filter.Update(simulatedStats);

    table.AddRow(
        (i + 1).ToString(),
        $"{simulatedStats.CpuUsage} | {simulatedStats.AvailableMemory} | {simulatedStats.MemoryUsage}",
        $"{filteredStats.CpuUsage} | {filteredStats.AvailableMemory} | {filteredStats.MemoryUsage}",
        GetDifferencePercentage(simulatedStats, filteredStats));

    AnsiConsole.WriteLine($"Iteration: {i}");
    AnsiConsole.Clear();

    await Task.Delay(100); // simulating a delay between measurements
}

AnsiConsole.Write(table);
Console.ReadKey();

static string GetDifferencePercentage(SiloRuntimeStatistics simulated, ResourceStatistics filtered)
{
    var cpu = Diff(simulated.CpuUsage, filtered.CpuUsage);
    var availableMemory = Diff(simulated.AvailableMemory, filtered.AvailableMemory);
    var memoryUsage = Diff(simulated.MemoryUsage, filtered.MemoryUsage);

    return $"{cpu}% | {availableMemory}% | {memoryUsage}%";

    static double Diff(float? simulated, float? filtered)
    {
        if (simulated.HasValue && filtered.HasValue)
        {
            return Math.Round(Math.Abs((filtered.Value - simulated.Value) / simulated.Value) * 100.0, 1);
        }

        return 0.0;
    }
}

static SiloRuntimeStatistics CreateSiloRuntimeStatistics(Faker faker)
{
    var constructorInfo = typeof(SiloRuntimeStatistics)
        .GetConstructors(BindingFlags.NonPublic | BindingFlags.Instance)
        .FirstOrDefault();

    if (constructorInfo != null)
    {
        var appStats = new FakeAppEnvironmentStatistics(faker);
        var hostStats = new FakeHostEnvironmentStatistics(faker, appStats.MemoryUsage);

        var parameters = new object[]
        {
            0,
            0,
            appStats ,
            hostStats,
            new OptionsWrapper<LoadSheddingOptions>(new LoadSheddingOptions() { LoadSheddingEnabled = false }),
            DateTime.UtcNow
        };

        return (SiloRuntimeStatistics)constructorInfo.Invoke(parameters);
    }

    throw new InvalidOperationException("Could not find the internal constructor of SiloRuntimeStatistics");
}

class FakeAppEnvironmentStatistics(Faker faker) : IAppEnvironmentStatistics
{
    public long? MemoryUsage { get; } = faker.Random.Long(
        (long)(0.25 * Constants.PhysicalMemory), 
        (long)(0.45 * Constants.PhysicalMemory));
}

class FakeHostEnvironmentStatistics(Faker faker, long? memUsage) : IHostEnvironmentStatistics
{
    public long? TotalPhysicalMemory { get; } = Constants.PhysicalMemory;
    public float? CpuUsage { get; } = faker.Random.Float(65, 75);
    public long? AvailableMemory { get; } = Constants.PhysicalMemory - memUsage;
}

class Constants
{
    public const long PhysicalMemory = (long)16 * 1024 * 1024 * 1024;
}
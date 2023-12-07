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

table.AddColumn("CpuUsage (Simulated)");
table.AddColumn("CpuUsage (Filtered)");
table.AddColumn("Difference (%)");

table.AddColumn("MemoryUsage (Simulated)");
table.AddColumn("MemoryUsage (Filtered)");
table.AddColumn("Difference (%)");

float cpuIncrement = 0.1f;
long memoryIncrement = 10 * Constants._1MB;

float currentCpuUsage = 40.0f;
long currentMemoryUsage = (long)(0.40 * Constants._16GB);

for (int i = 0; i < 10_000; i++)
{
    Console.WriteLine("Iteration: " + i);

    var simulatedStats = CreateSiloRuntimeStatistics(ref currentCpuUsage, ref currentMemoryUsage);
    var filteredStats = filter.Update(simulatedStats);
    var diffs = GetDifferencePercentage(simulatedStats, filteredStats);

    table.AddRow(
        (i + 1).ToString(),
        Formatter.ForDisplay(simulatedStats.CpuUsage),
        Formatter.ForDisplay(filteredStats.CpuUsage),
        diffs.Item1,
        Formatter.ForDisplay(simulatedStats.MemoryUsage),
        Formatter.ForDisplay(filteredStats.MemoryUsage),
        diffs.Item2);

    currentCpuUsage += cpuIncrement;
    if (currentCpuUsage > 75.0f)
    {
        cpuIncrement = -0.1f;
    }
    if (currentCpuUsage < 40.0f)
    {
        cpuIncrement = 0.1f;
    }

    currentMemoryUsage += memoryIncrement;
    if (currentMemoryUsage > (long)(0.75 * Constants._16GB))
    {
        memoryIncrement = -10 * Constants._1MB;
    }
    if (currentMemoryUsage < (long)(0.40 * Constants._16GB))
    {
        memoryIncrement = 10 * Constants._1MB;
    }
}

Console.WriteLine("Enter any key");
Console.ReadKey();

AnsiConsole.Write(table);
Console.ReadKey();

static SiloRuntimeStatistics CreateSiloRuntimeStatistics(ref float currentCpuUsage, ref long currentMemoryUsage)
{
    var constructorInfo = typeof(SiloRuntimeStatistics)
        .GetConstructors(BindingFlags.NonPublic | BindingFlags.Instance)
        .FirstOrDefault();

    if (constructorInfo != null)
    {
        var appStats = new FakeAppEnvironmentStatistics(currentMemoryUsage);
        var hostStats = new FakeHostEnvironmentStatistics(currentCpuUsage, appStats.MemoryUsage);

        var parameters = new object[]
        {
            0,
            0,
            appStats,
            hostStats,
            new OptionsWrapper<LoadSheddingOptions>(new LoadSheddingOptions() { LoadSheddingEnabled = false }),
            DateTime.UtcNow
        };

        return (SiloRuntimeStatistics)constructorInfo.Invoke(parameters);
    }

    throw new InvalidOperationException("Could not find the internal constructor of SiloRuntimeStatistics");
}

static (string, string) GetDifferencePercentage(SiloRuntimeStatistics simulated, ResourceStatistics filtered)
{
    var cpu = Diff(simulated.CpuUsage, filtered.CpuUsage);
    var memoryUsage = Diff(simulated.MemoryUsage, filtered.MemoryUsage);

    return ($"{cpu}%", $"{ memoryUsage}%");

    static double Diff(float? simulated, float? filtered)
    {
        if (simulated.HasValue && filtered.HasValue)
        {
            return Math.Round(Math.Abs((filtered.Value - simulated.Value) / simulated.Value) * 100.0, 1);
        }

        return 0.0;
    }
}

class FakeAppEnvironmentStatistics(long? memUsage) : IAppEnvironmentStatistics
{
    public long? MemoryUsage { get; } = memUsage;
}

class FakeHostEnvironmentStatistics(float? cpuUsage, long? memUsage) : IHostEnvironmentStatistics
{
    public long? TotalPhysicalMemory { get; } = Constants._16GB;
    public float? CpuUsage { get; } = cpuUsage;
    public long? AvailableMemory { get; } = Constants._16GB - memUsage;
}


class Constants
{
    public const long _16GB = (long)16 * 1024 * 1024 * 1024;
    public const long _1MB = (long)1024 * 1024;
}

class Formatter
{
    public static string ForDisplay(float? value) => Math.Round(value ?? 0, 1).ToString();
}
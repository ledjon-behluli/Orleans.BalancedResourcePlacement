using Bogus;
using Microsoft.Extensions.Options;
using Orleans.BalancedResourcePlacement;
using Orleans.Configuration;
using Orleans.Runtime;
using Orleans.Statistics;
using Spectre.Console;
using System.Reflection;

const int iterations = 10_000;

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

float currentCpuUsage = 5.0f;
long currentMemoryUsage = (long)(0.1 * Constants._16GB);

using (StreamWriter writer = new("output.txt"))
{
    foreach (string column in new string[] { "Iteration", "CpuUsage (Simulated)", "CpuUsage (Filtered)", "Difference (%)" })
    {
        writer.Write(column + "\t");
    }

    writer.WriteLine();
    bool _1stFlag = false;
    bool _2ndFlag = false;

    for (int i = 0; i < iterations; i++)
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

        WriteRow(writer,
            (i + 1).ToString(),
            Formatter.ForDisplay(simulatedStats.CpuUsage),
            Formatter.ForDisplay(filteredStats.CpuUsage),
            diffs.Item1);

        //Algorithm.SteadyIncreaseSteadyDecrease(ref cpuIncrement, ref memoryIncrement, ref currentCpuUsage, ref currentMemoryUsage);
        //Algorithm.SteadyIncreaseSharpDecrease(ref cpuIncrement, ref memoryIncrement, ref currentCpuUsage, ref currentMemoryUsage);
        //Algorithm.ExponentialIncreaseLinearDecrease(ref cpuIncrement, ref memoryIncrement, ref currentCpuUsage, ref currentMemoryUsage);
        //Algorithm.LinearIncreaseWithSuddenPeriodicBouncingFluctuations(ref cpuIncrement, ref memoryIncrement, ref currentCpuUsage, ref currentMemoryUsage, ref bouncing);
        Algorithm.LinearIncreaseWithSuddenSingleDownUpFluctuation(ref cpuIncrement, ref memoryIncrement, ref currentCpuUsage, ref currentMemoryUsage, ref _1stFlag, ref _2ndFlag);
    }
}

//Console.WriteLine("Enter any key to see results...");
//Console.ReadKey();

//AnsiConsole.Write(table);
//Console.ReadKey();


SaveValuesForPlotting(iterations);

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

    return ($"{cpu}%", $"{memoryUsage}%");

    static double Diff(float? simulated, float? filtered)
    {
        if (simulated.HasValue && filtered.HasValue)
        {
            return Math.Round(Math.Abs((filtered.Value - simulated.Value) / simulated.Value) * 100.0, 1);
        }

        return 0.0;
    }
}

static void SaveValuesForPlotting(int rowCount)
{
    string filePath = "C:\\Code Repositories\\Orleans.BalancedResourcePlacement\\KalmanTest\\bin\\Debug\\net8.0\\";

    string[] iteration = new string[rowCount];
    string[] cpuUsageSimulated = new string[rowCount];
    string[] cpuUsageFiltered = new string[rowCount];
    string[] differencePercentage = new string[rowCount];

    using (StreamReader reader = new(filePath + "output.txt"))
    {
        reader.ReadLine();

        for (int i = 0; i < rowCount; i++)
        {
            string? line = reader.ReadLine();
            if (line is null)
            {
                break;
            }

            string[] columns = line.Split('\t');
            iteration[i] = columns[0];
            cpuUsageSimulated[i] = columns[1];
            cpuUsageFiltered[i] = columns[2];
            differencePercentage[i] = columns[3].Replace("%", "");
        }
    }

    using (StreamWriter writer = new(filePath + "values.txt"))
    {
        writer.WriteLine("iterations = [" + string.Join(", ", iteration) + "]");
        writer.WriteLine("cpu_usage_simulated = [" + string.Join(", ", cpuUsageSimulated) + "]");
        writer.WriteLine("cpu_usage_filtered = [" + string.Join(", ", cpuUsageFiltered) + "]");
        writer.WriteLine("difference_percentage = [" + string.Join(", ", differencePercentage) + "]");
    }
}

static void WriteRow(StreamWriter writer, params string[] values)
{
    foreach (string value in values)
    {
        writer.Write(value + "\t");
    }
    writer.WriteLine();
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

class Algorithm
{
    public static void LinearIncreaseLinearDecrease(
        ref float cpuIncrement, 
        ref long memoryIncrement, 
        ref float currentCpuUsage, 
        ref long currentMemoryUsage)
    {
        if (currentCpuUsage > 75.0f)
        {
            cpuIncrement = -0.1f;
        }
        if (currentCpuUsage < 40.0f)
        {
            cpuIncrement = 0.1f;
        }

        currentCpuUsage += cpuIncrement;

        if (currentMemoryUsage > (long)(0.75 * Constants._16GB))
        {
            memoryIncrement = -10 * Constants._1MB;
        }
        if (currentMemoryUsage < (long)(0.40 * Constants._16GB))
        {
            memoryIncrement = 10 * Constants._1MB;
        }

        currentMemoryUsage += memoryIncrement;
    }

    public static void LinearIncreaseSharpDecrease(
        ref float cpuIncrement,
        ref long memoryIncrement,
        ref float currentCpuUsage,
        ref long currentMemoryUsage)
    {
        if (currentCpuUsage > 75.0f)
        {
            currentCpuUsage = 40.0f;
        }

        currentCpuUsage += cpuIncrement;

        if (currentMemoryUsage > (long)(0.75 * Constants._16GB))
        {
            currentMemoryUsage = (long)(0.40 * Constants._16GB);
        }

        currentMemoryUsage += memoryIncrement;
    }

    public static void ExponentialIncreaseLinearDecrease(
        ref float cpuIncrement,
        ref long memoryIncrement,
        ref float currentCpuUsage,
        ref long currentMemoryUsage)
    {
        if (currentCpuUsage > 75.0f)
        {
            cpuIncrement = -0.01f;
        }
        if (currentCpuUsage < 40.0f)
        {
            cpuIncrement = 0.005f * currentCpuUsage;
        }

        currentCpuUsage += cpuIncrement;

        if (currentMemoryUsage > (long)(0.75 * Constants._16GB))
        {
            memoryIncrement = (long)-0.01 * Constants._1MB;
        }
        else
        {
            memoryIncrement = (long)(0.005 * currentMemoryUsage);
        }

        currentMemoryUsage += memoryIncrement;
    }

    public static void LinearIncreaseWithSuddenPeriodicBouncingFluctuations(
        ref float cpuIncrement,
        ref long memoryIncrement,
        ref float currentCpuUsage,
        ref long currentMemoryUsage,
        ref bool bouncing)
    {
        if (currentCpuUsage >= 100.0f)
        {
            return;
        }

        if (bouncing && currentCpuUsage > 45.0f)
        {
            cpuIncrement = -0.005f * currentCpuUsage;
        }
        else if (currentCpuUsage > 50.0f && !bouncing)
        {
            bouncing = true;
        }
        else if (currentCpuUsage < 45.0f && bouncing)
        {
            currentCpuUsage = 50.0f;
            cpuIncrement = 0.05f;
            bouncing = false;
        }
        else
        {
            cpuIncrement = 0.05f;
        }

        currentCpuUsage += cpuIncrement;
    }

    public static void LinearIncreaseWithSuddenSingleDownUpFluctuation(
       ref float cpuIncrement,
       ref long memoryIncrement,
       ref float currentCpuUsage,
       ref long currentMemoryUsage,
       ref bool jumped1,
       ref bool jumped2)
    {
        if (currentCpuUsage >= 100.0f)
        {
            return;
        }

        if (jumped1 && currentCpuUsage > 40.0f)
        {
            cpuIncrement = -0.005f * currentCpuUsage;
        }
        else if (currentCpuUsage > 60.0f && !jumped1 && !jumped2)
        {
            jumped1 = true;
        }
        else if (currentCpuUsage < 40.0f && jumped1)
        {
            cpuIncrement = 0.005f * currentCpuUsage;

            jumped1 = false;
            jumped2 = true;
        }
        else if (currentCpuUsage > 40.0f && currentCpuUsage < 60.0f && !jumped1 && jumped2)
        {
            cpuIncrement = 0.005f * currentCpuUsage;
        }
        else
        {
            cpuIncrement = 0.05f;
        }

        currentCpuUsage += cpuIncrement;
    }
}
using Spectre.Console;
using System.Reflection;
using Orleans.BalancedResourcePlacement;

const bool saveResultForPlotting = true;
const int iterations = 250;

var filter = new StatisticsFilter<float>();

float cpuIncrement = 0.1f;
float simulatedCpuUsage = 5.0f;

bool _1stFlag = false;
bool _2ndFlag = false;
List<(int, int)> trafficHours =
      [(0, 1), (16, 24), (39, 40)];
      //[(0, 8), (20, 21), (40, 48)];
      //[(0, 2), (6, 8), (12, 14), (18, 22)];
      //[(0, 2), (4, 6), (8, 10), (12, 14), (16, 18), (20, 22)];
      //[(0, 1), (3, 4), (6, 7), (9, 10), (12, 13), (15, 16), (18, 19), (21, 22), (24, 25), (27, 28), (30, 31), (33, 34), (36, 37), (39, 40), (42, 43), (45, 46), (48, 49)];
int maxHour = trafficHours.SelectMany(pair => new[] { pair.Item1, pair.Item2 }).Max();

Action<int> patternInvoker = new(iteration =>
{
    //UsagePattern.LinearIncreaseLinearDecrease(ref cpuIncrement, ref simulatedCpuUsage);
    //UsagePattern.LinearIncreaseSharpDecrease(ref cpuIncrement, ref simulatedCpuUsage);
    //UsagePattern.ExponentialIncreaseLinearDecrease(ref cpuIncrement, ref simulatedCpuUsage);
    //UsagePattern.LinearIncreaseWithSuddenPeriodicBouncingFluctuations(ref cpuIncrement, ref simulatedCpuUsage, ref _1stFlag);
    //UsagePattern.LinearIncreaseWithSuddenSingleDownUpFluctuation(ref cpuIncrement, ref simulatedCpuUsage, ref _1stFlag, ref _2ndFlag);
    UsagePattern.SemiPeriodicHighLowTrafficWithRandomness(ref simulatedCpuUsage, iteration, iterations, maxHour, trafficHours);
});

if (!saveResultForPlotting)
{
    var table = new Table();

    table.AddColumn("Iteration");
    table.AddColumn("Simulated");
    table.AddColumn("Filtered");

    for (int i = 0; i < iterations; i++)
    {
        Console.WriteLine($"Iteration: {i}");

        float filteredCpuUsage = filter.Filter(simulatedCpuUsage);
        float diff = 100.0f * Math.Abs((filteredCpuUsage - simulatedCpuUsage) / simulatedCpuUsage);

        table.AddRow(
            (i + 1).ToString(),
            Formatter.ForDisplay(simulatedCpuUsage),
            Formatter.ForDisplay(filteredCpuUsage));

        patternInvoker(i);
    }

    AnsiConsole.Write(table);
    Console.ReadKey();
}
else
{
    using (StreamWriter writer = new("output.txt"))
    {
        foreach (string column in new string[] { "Iteration", "Simulated", "Filtered", "Diff (%)" })
        {
            writer.Write(column + "\t");
        }

        writer.WriteLine();

        for (int i = 0; i < iterations; i++)
        {
            Console.WriteLine($"Iteration: {i}");

            float filteredCpuUsage = filter.Filter(simulatedCpuUsage);
            float diff = 100.0f * Math.Abs((filteredCpuUsage - simulatedCpuUsage) / simulatedCpuUsage);

            WriteRow(writer,
                (i + 1).ToString(),
                Formatter.ForDisplay(simulatedCpuUsage),
                Formatter.ForDisplay(filteredCpuUsage));

            patternInvoker(i);
        }
    }

    SaveSamplingForPlotting(iterations);

    static void WriteRow(StreamWriter writer, params string[] values)
    {
        foreach (string value in values)
        {
            writer.Write(value + "\t");
        }
        writer.WriteLine();
    }

    static void SaveSamplingForPlotting(int rowCount)
    {
        string? filePath = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);

        string[] iteration = new string[rowCount];
        string[] cpuUsageSimulated = new string[rowCount];
        string[] cpuUsageFiltered = new string[rowCount];

        using (StreamReader reader = new(filePath + "\\output.txt"))
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
            }
        }

        using (StreamWriter writer = new(filePath + "\\plot.txt"))
        {
            writer.WriteLine(
               $"""
            import matplotlib.pyplot as plt

            iterations = [{string.Join(", ", iteration)}]
            simulated = [{string.Join(", ", cpuUsageSimulated)}]
            filtered = [{string.Join(", ", cpuUsageFiltered)}]

            plt.figure(figsize=(100, 60))

            plt.plot(iterations, simulated, marker='o', label='Simulated')
            plt.plot(iterations, filtered, marker='o',  label='Filtered')

            plt.title('Cpu Usage Comparison')
            plt.xlabel('Iteration')
            plt.ylabel('Values')
            plt.legend()
            plt.grid(True)
            plt.show()
            """);
        }

        File.Delete(filePath + "\\output.txt");
    }
}

class Formatter
{
    public static string ForDisplay(float? value) => Math.Round(value ?? 0, 1).ToString();
}

class UsagePattern
{
    public static void LinearIncreaseLinearDecrease(
        ref float cpuIncrement,
        ref float cpuUsage)
    {
        if (cpuUsage > 75.0f)
        {
            cpuIncrement = -0.1f;
        }
        if (cpuUsage < 40.0f)
        {
            cpuIncrement = 0.1f;
        }

        cpuUsage += cpuIncrement;
    }

    public static void LinearIncreaseSharpDecrease(
        ref float cpuIncrement,
        ref float cpuUsage)
    {
        if (cpuUsage > 75.0f)
        {
            cpuUsage = 40.0f;
        }

        cpuUsage += cpuIncrement;
    }

    public static void ExponentialIncreaseLinearDecrease(
        ref float cpuIncrement,
        ref float cpuUsage)
    {
        if (cpuUsage > 75.0f)
        {
            cpuIncrement = -0.01f;
        }
        if (cpuUsage < 40.0f)
        {
            cpuIncrement = 0.005f * cpuUsage;
        }

        cpuUsage += cpuIncrement;
    }

    public static void LinearIncreaseWithSuddenPeriodicBouncingFluctuations(
        ref float cpuIncrement,
        ref float cpuUsage,
        ref bool bouncing)
    {
        if (cpuUsage >= 100.0f)
        {
            return;
        }

        if (bouncing && cpuUsage > 45.0f)
        {
            cpuIncrement = -0.005f * cpuUsage;
        }
        else if (cpuUsage > 50.0f && !bouncing)
        {
            bouncing = true;
        }
        else if (cpuUsage < 45.0f && bouncing)
        {
            cpuUsage = 50.0f;
            cpuIncrement = 0.05f;
            bouncing = false;
        }
        else
        {
            cpuIncrement = 0.05f;
        }

        cpuUsage += cpuIncrement;
    }

    public static void LinearIncreaseWithSuddenSingleDownUpFluctuation(
       ref float cpuIncrement,
       ref float cpuUsage,
       ref bool jumped1,
       ref bool jumped2)
    {
        if (cpuUsage >= 100.0f)
        {
            return;
        }

        if (jumped1 && cpuUsage > 10.0f)
        {
            cpuIncrement = Multiply(-cpuUsage);
        }
        else if (cpuUsage > 60.0f && !jumped1 && !jumped2)
        {
            jumped1 = true;
        }
        else if (cpuUsage < 10.0f && jumped1)
        {
            cpuIncrement = Multiply(cpuUsage);

            jumped1 = false;
            jumped2 = true;
        }
        else if (cpuUsage > 10.0f && cpuUsage < 60.0f && !jumped1 && jumped2)
        {
            cpuIncrement = Multiply(cpuUsage);
        }
        else
        {
            cpuIncrement = 0.05f;
        }

        cpuUsage += cpuIncrement;

        static float Multiply(float cpuUsage)
        {
            return 0.05f * cpuUsage;
        }
    }

    public static void SemiPeriodicHighLowTrafficWithRandomness(
        ref float cpuUsage,
        int currentIteration,
        int totalIterations,
        int maxHourSpan,
        List<(int, int)> trafficHours)
    {
        float percentOfDay = (float)currentIteration / totalIterations;
        int hour = (int)(maxHourSpan * percentOfDay) % maxHourSpan;

        if (IsHighTraffic(hour, trafficHours))
        {
            cpuUsage = 80f;
            cpuUsage = SuperImposeNoisySin(
                noiseAmplitudeMin: 2.0f,
                noiseAmplitudeMax: 8.0f,
                cpuUsage: ref cpuUsage);
        }
        else
        {
            cpuUsage = 20f;
            cpuUsage = SuperImposeNoisySin(
                noiseAmplitudeMin: 0.5f,
                noiseAmplitudeMax: 2.0f,
                cpuUsage: ref cpuUsage);
        }

        if (cpuUsage > 100)
        {
            cpuUsage = 100;
        }

        static float SuperImposeNoisySin(float noiseAmplitudeMin, float noiseAmplitudeMax, ref float cpuUsage)
        {
            const float frequency = 0.2f;

            float signalAmplitude = Random.Shared.NextSingle();
            float noiseAmplitude = noiseAmplitudeMin + (noiseAmplitudeMax * Random.Shared.NextSingle());

            return cpuUsage += (float)(
                signalAmplitude * Math.Sin(2 * Math.PI * frequency) +
                noiseAmplitude * Random.Shared.NextSingle());
        }

        static bool IsHighTraffic(int currentHour, List<(int, int)> trafficHours)
        {
            foreach (var (startHour, endHour) in trafficHours)
            {
                if (currentHour >= startHour && currentHour <= endHour)
                {
                    return true;
                }
            }
            return false;
        }
    }
}
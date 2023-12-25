using Spectre.Console;
using System.Reflection;
using Orleans.BalancedResourcePlacement;

const int iterations = 2500;
const bool save = true;

var filter = new DKalmanFilter<float>();
var table = new Table();

table.AddColumn("Iteration");
table.AddColumn("Simulated");
table.AddColumn("Filtered");
table.AddColumn("Diff (%)");

float cpuIncrement = 0.1f;
float simulatedCpuUsage = 5.0f;

using (StreamWriter writer = new("output.txt"))
{
    foreach (string column in new string[] { "Iteration", "Simulated", "Filtered", "Diff (%)" })
    {
        writer.Write(column + "\t");
    }

    writer.WriteLine();

    bool _1stFlag = false;
    bool _2ndFlag = false;

    for (int i = 0; i < iterations; i++)
    {
        Console.WriteLine("Iteration: " + i);

        float filteredCpuUsage = filter.Filter(simulatedCpuUsage);
        float diff = 100.0f * Math.Abs((filteredCpuUsage - simulatedCpuUsage) / simulatedCpuUsage);

        table.AddRow(
            (i + 1).ToString(),
            Formatter.ForDisplay(simulatedCpuUsage),
            Formatter.ForDisplay(filteredCpuUsage),
            Formatter.ForDisplay(diff) + "%");

        WriteRow(writer,
            (i + 1).ToString(),
            Formatter.ForDisplay(simulatedCpuUsage),
            Formatter.ForDisplay(filteredCpuUsage),
            Formatter.ForDisplay(diff) + "%");

        //Algorithm.LinearIncreaseLinearDecrease(ref cpuIncrement, ref simulatedCpuUsage);
        //Algorithm.LinearIncreaseSharpDecrease(ref cpuIncrement, ref simulatedCpuUsage);
        //Algorithm.ExponentialIncreaseLinearDecrease(ref cpuIncrement, ref simulatedCpuUsage);
        //Algorithm.LinearIncreaseWithSuddenPeriodicBouncingFluctuations(ref cpuIncrement, ref simulatedCpuUsage, ref _1stFlag);
        Algorithm.LinearIncreaseWithSuddenSingleDownUpFluctuation(ref cpuIncrement, ref simulatedCpuUsage, ref _1stFlag, ref _2ndFlag);
    }
}

if (save)
{
    SaveValuesForPlotting(iterations);
}
else
{
    Console.WriteLine("Enter any key to see results...");
    Console.ReadKey();

    AnsiConsole.Write(table);
    Console.ReadKey();
}

static void SaveValuesForPlotting(int rowCount)
{
    string? filePath = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);

    string[] iteration = new string[rowCount];
    string[] cpuUsageSimulated = new string[rowCount];
    string[] cpuUsageFiltered = new string[rowCount];
    string[] differencePercentage = new string[rowCount];

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
            differencePercentage[i] = columns[3].Replace("%", "");
        }
    }

    using (StreamWriter writer = new(filePath + "\\values.txt"))
    {
        writer.WriteLine("iterations = [" + string.Join(", ", iteration) + "]");
        writer.WriteLine("simulated = [" + string.Join(", ", cpuUsageSimulated) + "]");
        writer.WriteLine("filtered = [" + string.Join(", ", cpuUsageFiltered) + "]");
        writer.WriteLine("diff_perc = [" + string.Join(", ", differencePercentage) + "]");
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

class Formatter
{
    public static string ForDisplay(float? value) => Math.Round(value ?? 0, 1).ToString();
}

class Algorithm
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

        if (jumped1 && cpuUsage > 40.0f)
        {
            cpuIncrement = Multiply(-cpuUsage);
        }
        else if (cpuUsage > 60.0f && !jumped1 && !jumped2)
        {
            jumped1 = true;
        }
        else if (cpuUsage < 40.0f && jumped1)
        {
            cpuIncrement = Multiply(cpuUsage);

            jumped1 = false;
            jumped2 = true;
        }
        else if (cpuUsage > 40.0f && cpuUsage < 60.0f && !jumped1 && jumped2)
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
            return 0.005f * cpuUsage;
        }
    }

    public static void Sinosoid()
    {
        //var noisySin = Math.Sin(k * 3.14 * 5 / 180) + (double)rnd.Next(50) / 100;
    }
}
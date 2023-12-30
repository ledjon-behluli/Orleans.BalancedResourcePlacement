using ScottPlot;
using System.Drawing;
using Orleans.BalancedResourcePlacement;

const int iterations = 1000;

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

double[] simulatedData = new double[iterations];
double[] filteredData = new double[iterations];

for (int i = 0; i < iterations; i++)
{
    Console.WriteLine($"Iteration: {i}");

    float filteredCpuUsage = filter.Filter(simulatedCpuUsage);

    simulatedData[i] = Math.Round(simulatedCpuUsage, 1);
    filteredData[i] = Math.Round(filteredCpuUsage, 1);

    UsagePattern.LinearIncreaseLinearDecrease(ref cpuIncrement, ref simulatedCpuUsage);
    //UsagePattern.LinearIncreaseSharpDecrease(ref cpuIncrement, ref simulatedCpuUsage);
    //UsagePattern.ExponentialIncreaseLinearDecrease(ref cpuIncrement, ref simulatedCpuUsage);
    //UsagePattern.LinearIncreaseWithSuddenPeriodicBouncingFluctuations(ref cpuIncrement, ref simulatedCpuUsage, ref _1stFlag);
    //UsagePattern.LinearIncreaseWithSuddenSingleDownUpFluctuation(ref cpuIncrement, ref simulatedCpuUsage, ref _1stFlag, ref _2ndFlag);
    //UsagePattern.SemiPeriodicHighLowTrafficWithRandomness(ref simulatedCpuUsage, i, iterations, maxHour, trafficHours);
}

Plot plt = new();

plt.AddSignal(label: "Simulated", ys: simulatedData, color: Color.Blue);
plt.AddSignal(label: "Filtered", ys: filteredData, color: Color.Orange);

new FormsPlotViewer(plt).ShowDialog();

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
            SuperImposeNoisySin(
                noiseAmplitudeMin: 2.0f,
                noiseAmplitudeMax: 8.0f,
                cpuUsage: ref cpuUsage);
        }
        else
        {
            cpuUsage = 20f;
            SuperImposeNoisySin(
                noiseAmplitudeMin: 0.5f,
                noiseAmplitudeMax: 2.0f,
                cpuUsage: ref cpuUsage);
        }

        if (cpuUsage > 100)
        {
            cpuUsage = 100;
        }

        static void SuperImposeNoisySin(float noiseAmplitudeMin, float noiseAmplitudeMax, ref float cpuUsage)
        {
            const float frequency = 0.2f;

            float signalAmplitude = Random.Shared.NextSingle();
            float noiseAmplitude = noiseAmplitudeMin + (noiseAmplitudeMax * Random.Shared.NextSingle());

            cpuUsage += (float)(
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
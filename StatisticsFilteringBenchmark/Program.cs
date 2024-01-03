using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Running;
using Orleans.BalancedResourcePlacement;

BenchmarkRunner.Run<Bench>();

[SimpleJob]
[MemoryDiagnoser]
[HideColumns("Error", "StdDev", "Median", "RatioSD")]
public class Bench
{
    const int samplePoints = 1000;

    readonly DualModeKalmanFilter<float> filter = new();

    readonly float[] values = new float[samplePoints];
    readonly float[] measurements = new float[samplePoints];

    [GlobalSetup]
    public void Setup()
    {
        for (int i = 0; i < samplePoints; i++)
        {
            measurements[i] = (float)(Math.Sin(0.1 * i) * 100.0f);
        }
    }

    [Benchmark(Baseline = true)]
    public void AssignMeasurements()
    {
        for (int i = 0; i < samplePoints; i++)
        {
            values[i] = measurements[i];
        }
    }

    [Benchmark]
    public void FilterThanAssignMeasurements()
    {
        for (int i = 0; i < samplePoints; i++)
        {
            float val = filter.Filter(measurements[i]);
            values[i] = val;
        }
    }
}
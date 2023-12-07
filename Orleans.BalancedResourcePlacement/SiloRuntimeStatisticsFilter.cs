using MathNet.Filtering.Kalman;
using MathNet.Numerics.LinearAlgebra;
using Orleans.Runtime;

namespace Orleans.BalancedResourcePlacement;

public record struct ResourceStatistics(
    float? CpuUsage,
    float? AvailableMemory,
    long? MemoryUsage,
    long? TotalPhysicalMemory,
    bool IsOverloaded);

public sealed class SiloRuntimeStatisticsFilter
{
    private const int Dimensions = 3;

    private readonly Matrix<double> F = Matrix<double>.Build.DenseIdentity(Dimensions);
    private readonly Matrix<double> H = Matrix<double>.Build.DenseIdentity(Dimensions);
    private readonly Matrix<double> R = Matrix<double>.Build.DenseIdentity(Dimensions);

    private readonly DiscreteKalmanFilter filter;

    public SiloRuntimeStatisticsFilter()
    {
        var initialState = Matrix<double>.Build.DenseOfColumnVectors(Vector<double>.Build.Dense(Dimensions, 0.0));
        var initialCovariance = Matrix<double>.Build.DenseIdentity(Dimensions);

        filter = new(initialState, initialCovariance);
    }

    public ResourceStatistics Update(SiloRuntimeStatistics measurement)
    {
        Matrix<double> z = FromStats(measurement);

        filter.Predict(F);
        filter.Update(z, H, R);

        return ToStats(filter.State, measurement);
    }

    private static Matrix<double> FromStats(SiloRuntimeStatistics stats)
        => Matrix<double>.Build.DenseOfArray(new double[,]
        {
            { stats.CpuUsage ?? 0.0 },
            { stats.AvailableMemory ?? 0.0 },
            { stats.MemoryUsage ?? 0.0 }
        });

    private static ResourceStatistics ToStats(Matrix<double> matrix, SiloRuntimeStatistics measurement)
        => new(
            CpuUsage: (float)matrix[0, 0],
            AvailableMemory: (float)matrix[1, 0],
            MemoryUsage: (long)matrix[2, 0],
            TotalPhysicalMemory: measurement.TotalPhysicalMemory,
            IsOverloaded: measurement.IsOverloaded);
}

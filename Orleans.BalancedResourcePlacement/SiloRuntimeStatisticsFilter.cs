using MathNet.Filtering.Kalman;
using MathNet.Numerics;
using MathNet.Numerics.LinearAlgebra;
using Orleans.Runtime;
using System.Runtime.CompilerServices;

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
    private readonly KalmanFilter kalmanFilter;

    public SiloRuntimeStatisticsFilter()
    {
        //var initialState = Matrix<double>.Build.DenseOfColumnVectors(Vector<double>.Build.Dense(Dimensions, 0.0));
        //var initialCovariance = Matrix<double>.Build.DenseIdentity(Dimensions);

        //filter = new(initialState, initialCovariance);

        kalmanFilter = new();
    }

    public ResourceStatistics Update(SiloRuntimeStatistics measurement)
    {
        var esstCpu = kalmanFilter.Filter(measurement.CpuUsage ?? 0f);

        Matrix<double> z = FromStats(measurement);

        return new(esstCpu,
            measurement.AvailableMemory,
            measurement.MemoryUsage,
            measurement.TotalPhysicalMemory,
            measurement.IsOverloaded);
    }

    //public ResourceStatistics Update(SiloRuntimeStatistics measurement)
    //{
    //    Matrix<double> z = FromStats(measurement);

    //    filter.Predict(F);
    //    filter.Update(z, H, R);

    //    return ToStats(filter.State, measurement);
    //}

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

internal sealed class KalmanFilter
{
    private const float measurementNoiseCovariance = 1f;

    private float estimate = 0f;
    private float errorCovariance = 1f;

    public float Filter(float measurement)
    {
        // Prediction Step:
        // -------------------------------------------
        // Formula: x_k = A * x_k-1 + B * u_k
        // Assumptions:
        //  1. There is no control signal, therefor u_k = 0
        //  2. The state transition matrix (A) is unitary (no transitions are expected, there state won't change),
        //     therefor A = 1 (e.g: cpu usage change can't be known)
        // Simplification: x_k = x_k-1
        float predictedEstimate = estimate;

        // Formula: P_k = A * P_k-1 * A_T + Q 
        // Assumptions:
        //  1. Since A = 1, the transpose A_T = 1
        //  2. The process noise covariance matrix (Q) is unitary (systems state is not expected to deviate from the model)
        //     therefor Q = 0 (see: Assumption 2 above)
        // Simplification: P_k = P_k-1
        float predictedErrorCovariance = errorCovariance;


        // Correction Step:
        // -------------------------------------------
        // Formula: K_k = (P_k * H_T) / (H * P_k * H_T + R)
        // Assumptions:
        //  1. The state-to-measurement matrix (H) is unitary (acts as a bridge between the internal model - A, and the external measurements - R),
        //     therefor H = 1 (indicates that the measurements directly correspond to the state variables without any transformations or scaling factors)
        //  2. The measurement covariance matrix (R) is unitary (must be as choosing 0 would mean would lead all the consequent estimates to remaining as the initial state)
        //     therefor R = 1
        // Simplification: K_k = P_k / (P_k + 1)
        float gain = predictedErrorCovariance / (predictedErrorCovariance + measurementNoiseCovariance);

        // Formula: x_k+1 = x_k + K_k * (z_k - x_k); where z_k is the new 'measurement'
        estimate = predictedEstimate + gain * (measurement - predictedEstimate);
        // Formula: P_k+1 = (1 - K_k) * P_k
        errorCovariance = (1f - gain) * predictedErrorCovariance;

        return estimate;
    }
}
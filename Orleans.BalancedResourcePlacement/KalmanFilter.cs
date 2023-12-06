using Orleans.Runtime;

namespace Orleans.BalancedResourcePlacement;

public record struct ResourceStatistics(
    float? CpuUsage,
    float? AvailableMemory,
    long? MemoryUsage,
    long? TotalPhysicalMemory,
    bool IsOverloaded);

public sealed class KalmanFilter
{
    /// <summary>
    /// Measurement matrix
    /// </summary>
    private readonly float[,] H = Matrix.Identity();
    /// <summary>
    /// State transition matrix
    /// </summary>
    private readonly float[,] F = Matrix.Identity();
    /// <summary>
    /// Process noise covariance matrix
    /// </summary>
    private readonly float[,] Q = Matrix.Identity();
    /// <summary>
    /// Measurement noise covariance matrix
    /// </summary>
    private readonly float[,] R = Matrix.Identity();

    /// <summary>
    /// Error covariance matrix
    /// </summary>
    private float[,] P = Matrix.Identity();
    /// <summary>
    /// State vector
    /// </summary>
    private float[,] x = new float[,] { { 0.0f }, { 0.0f }, { 0.0f }, { 0.0f } };

    public ResourceStatistics Update(SiloRuntimeStatistics measurement)
    {
        float[,] z = ConvertToVector(measurement);

        // Prediction step
        x = Matrix.Multiply(F, x);     // x_hat = F * x
        P = Matrix.Add(Matrix.Multiply(Matrix.Multiply(F, P), Matrix.Transpose(F)), Q);  // P_hat = F * P * F' + Q

        // Update step
        var y = Matrix.Subtract(z, Matrix.Multiply(H, x));                           // y = z - H * x_hat
        var S = Matrix.Add(Matrix.Multiply(Matrix.Multiply(H, P), Matrix.Transpose(H)), R);  // S = H * P_hat * H' + R
        var K = Matrix.Multiply(Matrix.Multiply(P, Matrix.Transpose(H)), Matrix.Inverse(S));  // K = P_hat * H' * inv(S)

        x = Matrix.Add(x, Matrix.Multiply(K, y));  // x = x_hat + K * y
        P = Matrix.Multiply(Matrix.Subtract(Matrix.Identity(), Matrix.Multiply(K, H)), P);  // P = (I - K * H) * P_hat

        return ConvertToStatistics(x, measurement.IsOverloaded);
    }

    private static float[,] ConvertToVector(SiloRuntimeStatistics stats)
        => new float[,]
        {
            { stats.CpuUsage ?? 0.0f },
            { stats.AvailableMemory ?? 0.0f },
            { stats.MemoryUsage ?? 0.0f },
            { stats.TotalPhysicalMemory ?? 0.0f }
        };

    private static ResourceStatistics ConvertToStatistics(float[,] vector, bool isOverloaded)
        => new(
            CpuUsage: vector[0, 0],
            AvailableMemory: vector[1, 0],
            MemoryUsage: (long)vector[2, 0],
            TotalPhysicalMemory: (long)vector[3, 0],
            IsOverloaded: isOverloaded);

    static class Matrix
    {
        public static float[,] Identity()
        {
            var result = new float[4, 4];
            for (int i = 0; i < 4; i++)
            {
                result[i, i] = 1.0f;
            }
            return result;
        }

        public static float[,] Transpose(float[,] matrix)
        {
            int rows = matrix.GetLength(0);
            int cols = matrix.GetLength(1);
            var result = new float[cols, rows];

            for (int i = 0; i < 4; i++)
            {
                for (int j = 0; j < 4; j++)
                {
                    result[j, i] = matrix[i, j];
                }
            }
            return result;
        }

        public static float[,] Multiply(float[,] a, float[,] b)
        {
            int rowsA = a.GetLength(0);
            int colsA = a.GetLength(1);
            int colsB = b.GetLength(1);
            var result = new float[rowsA, colsB];

            for (int i = 0; i < rowsA; i++)
            {
                for (int j = 0; j < colsB; j++)
                {
                    float sum = 0.0f;
                    for (int k = 0; k < colsA; k++)
                    {
                        sum += a[i, k] * b[k, j];
                    }
                    result[i, j] = sum;
                }
            }

            return result;
        }

        public static float[,] Add(float[,] a, float[,] b)
        {
            int rows = a.GetLength(0);
            int cols = a.GetLength(1);
            var result = new float[rows, cols];

            for (int i = 0; i < rows; i++)
            {
                for (int j = 0; j < cols; j++)
                {
                    result[i, j] = a[i, j] + b[i, j];
                }
            }

            return result;
        }

        public static float[,] Subtract(float[,] a, float[,] b)
        {
            int rows = a.GetLength(0);
            int cols = a.GetLength(1);
            var result = new float[rows, cols];

            for (int i = 0; i < rows; i++)
            {
                for (int j = 0; j < cols; j++)
                {
                    result[i, j] = a[i, j] - b[i, j];
                }
            }

            return result;
        }

        public static float[,] Inverse(float[,] matrix)
        {
            // Implement matrix inversion if needed
            // For simplicity, assuming the matrix is invertible and returning the input matrix
            return matrix;
        }
    }
}
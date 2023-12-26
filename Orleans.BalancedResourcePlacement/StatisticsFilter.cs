using System.Numerics;

namespace Orleans.BalancedResourcePlacement;

internal sealed class StatisticsFilter<T> where T : unmanaged, INumber<T>
{
    private bool useFastFilter = true;

    private readonly KalmanFilter slowFilter = new(T.Zero);
    private readonly KalmanFilter fastFilter = new(T.CreateChecked(0.01));

    public T Filter(T? measurement)
    {
        T slowEstimate = slowFilter.Filter(measurement);
        T fastEstimate = fastFilter.Filter(measurement);

        if (measurement > slowEstimate)
        {
            useFastFilter = true;
            return fastEstimate;
        }
        else
        {
            if (useFastFilter)
            {
                slowFilter.Reset();
                useFastFilter = false;
            }

            return slowEstimate;
        }
    }

    sealed class KalmanFilter
    {
        private readonly T measurementNoiseCovariance = T.One;
        private readonly T processNoiseCovariance;

        private T priorEstimate;
        private T priorErrorCovariance;

        public KalmanFilter(T processNoiseCovariance)
        {
            this.processNoiseCovariance = processNoiseCovariance;
            Reset();
        }

        public void Reset()
        {
            priorEstimate = T.Zero;
            priorErrorCovariance = T.One;
        }

        public T Filter(T? measurement)
        {
            // Prediction Step:
            // -------------------------------------------
            // Formula: x_k = A * x_k-1 + B * u_k
            // Assumptions:
            //  1. There is no control signal, therefor u_k = 0
            //  2. The state transition matrix (A) is unitary (no transitions are expected, there state won't change),
            //     therefor A = 1 (e.g: cpu usage change can't be known)
            // Simplification: x_k = x_k-1
            T estimate = priorEstimate;

            // Formula: P_k = A * P_k-1 * A_T + Q 
            // Assumptions:
            //  1. Since A = 1, the transpose A_T = 1
            //  2. The process noise covariance matrix (Q) stays, as we dont know how the system will change between two subsequent measurements.
            // Simplification: P_k = P_k-1 + Q
            T errorCovariance = priorErrorCovariance + processNoiseCovariance;


            // Correction Step:
            // -------------------------------------------
            // Formula: K_k = (P_k * H_T) / (H * P_k * H_T + R)
            // Assumptions:
            //  1. The state-to-measurement matrix (H) is unitary (acts as a bridge between the internal model - A, and the external measurements - R),
            //     therefor H = 1 (indicates that the measurements directly correspond to the state variables without any transformations or scaling factors)
            //  2. The measurement covariance matrix (R) is unitary (must be as choosing 0 would mean would lead all the consequent estimates to remaining as the initial state,
            //     therefor R = 1
            // Simplification: K_k = P_k / (P_k + 1)
            T gain = errorCovariance / (errorCovariance + measurementNoiseCovariance);

            // Formula: x_k+1 = x_k + K_k * (z_k - x_k); where z_k is the new 'measurement'
            priorEstimate = estimate + gain * ((measurement ?? T.Zero) - estimate);
            // Formula: P_k+1 = (1 - K_k) * P_k
            // NOTE: [1 - gain] can never become 0, because 'gain' can only be 1 when 'errorCovariance' is infinity.
            priorErrorCovariance = (T.One - gain) * errorCovariance;

            return priorEstimate;
        }
    }
}
using System.Numerics;

namespace Orleans.BalancedResourcePlacement;

internal sealed class DualModeKalmanFilter<T> where T : unmanaged, INumber<T>
{
    private readonly KalmanFilter slowFilter = new(T.Zero);
    private readonly KalmanFilter fastFilter = new(T.CreateChecked(0.01));

    private FilterRegime regime = FilterRegime.Slow;

    public T Filter(T? measurement)
    {
        T _measurement = measurement ?? T.Zero;

        T slowEstimate = slowFilter.Filter(_measurement);
        T fastEstimate = fastFilter.Filter(_measurement);

        if (_measurement > slowEstimate)
        {
            if (regime == FilterRegime.Slow)
            {
                // since we now got a measurement we can use it to set the filter's 'estimate',
                // in addition we set the 'error covariance' to 0, indicating we want to fully
                // trust the measurement (now the new 'estimate') to reach the actual signal asap.
                fastFilter.SetState(_measurement, T.Zero);

                // ensure we recalculate since we changed the 'error covariance'
                fastEstimate = fastFilter.Filter(_measurement); 

                regime = FilterRegime.Fast;
            }

            return fastEstimate;
        }
        else
        {
            if (regime == FilterRegime.Fast)
            {
                // since the slow filter will accumulate the changes, we want to reset its state
                // so that it aligns with the current peak of the fast filter so we get a slower
                // decay that is always aligned with the latest fast filter state and not the overall
                // accumulated state of the whole signal over its lifetime.
                slowFilter.SetState(fastFilter.PriorEstimate, fastFilter.PriorErrorCovariance);

                // ensure we recalculate since we changed both the 'estimate' and 'error covariance'
                slowEstimate = slowFilter.Filter(_measurement);  

                regime = FilterRegime.Slow;
            }

            return slowEstimate;
        }
    }

    enum FilterRegime
    {
        Slow,
        Fast
    }

    sealed class KalmanFilter
    {
        private readonly T processNoiseCovariance;

        public T PriorEstimate { get; private set; } = T.Zero;
        public T PriorErrorCovariance { get; private set; } = T.One;

        public KalmanFilter(T processNoiseCovariance)
        {
            this.processNoiseCovariance = processNoiseCovariance;
        }

        public void SetState(T estimate, T errorCovariance)
        {
            PriorEstimate = estimate;
            PriorErrorCovariance = errorCovariance;
        }

        public T Filter(T measurement)
        {
            // Prediction Step:
            // -------------------------------------------
            // Formula: ^x_k = A * x_k-1 + B * u_k
            // Assumptions:
            //  1. There is no control signal, therefor u_k = 0
            //  2. The state transition matrix (A) is unitary (no transitions are expected, there state won't change),
            //     therefor A = 1 (e.g: cpu usage change can't be known)
            // Simplification: ^x_k = x_k-1
            T estimate = PriorEstimate;

            // Formula: ^P_k = A * P_k-1 * A_T + Q 
            // Assumptions:
            //  1. Since A = 1, the transpose A_T = 1
            //  2. The process noise covariance matrix (Q) stays, as refers to unpredictable changes in the system that are not explicitly modeled (all of it).
            // Simplification: ^P_k = P_k-1 + Q
            T errorCovariance = PriorErrorCovariance + processNoiseCovariance;


            // Correction Step:
            // -------------------------------------------
            // Formula: K_k = (P_k * H_T) / (H * P_k * H_T + R)
            // Assumptions:
            //  1. The observation matrix (H) is unitary (acts as a bridge between the internal model - A, and the external measurements - R),
            //     therefor H = 1 (indicates that the measurements directly correspond to the state variables without any transformations or scaling factors)
            //  2. The measurement covariance matrix (R) is unitary (must be as choosing 0 would mean would lead all the consequent estimates to remaining as the initial state,
            //     therefor R = 1
            // Simplification: K_k = P_k / (P_k + 1);
            T gain = errorCovariance / (errorCovariance + T.One);

            // Formula: ^x_k = x_k + K_k * (z_k - H * x_k)
            //  1. z_k is the measurementdd
            //  2. H = 1
            // Simplification: ^x_k = x_k + K_k * (z_k - x_k)
            T newEstimate = estimate + gain * (measurement - estimate);

            // Formula: ^P_k = (I - K_k * H) * P_k;
            //  1. [1 - gain] can never become 0, because 'gain' can only be 1 when 'errorCovariance' is infinity.
            //  2. I = 1, H = 1
            // Simplification: ^P_k = (1 - K_k) * P_k;
            T newErrorCovariance = (T.One - gain) * errorCovariance;

            PriorEstimate = newEstimate;
            PriorErrorCovariance = newErrorCovariance;

            return newEstimate;
        }
    }
}
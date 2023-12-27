using System.Numerics;

namespace Orleans.BalancedResourcePlacement;

internal sealed class StatisticsFilter<T> where T : unmanaged, INumber<T>
{
    private bool useFastFilter = false;

    private readonly KalmanFilter slowFilter = new(T.Zero);
    private readonly KalmanFilter fastFilter = new(T.CreateChecked(0.01));

    public T Filter(T? measurement)
    {
        T _measurement = measurement ?? T.Zero;

        T slowEstimate = slowFilter.Filter(_measurement);
        T fastEstimate = fastFilter.Filter(_measurement);

        if (_measurement > slowEstimate)
        {
            if (!useFastFilter)
            {
                fastFilter.CopyState(slowFilter);
                //fastFilter.SetEstimate(_measurement);
                useFastFilter = true;
            }
            
            return fastEstimate;
        }
        else
        {
            if (useFastFilter)
            {
                slowFilter.CopyState(fastFilter);
                useFastFilter = false;
            }

            return slowEstimate;
        }
    }

    sealed class KalmanFilter
    {
        private readonly T measurementNoiseCovariance = T.One;
        private readonly T processNoiseCovariance;

        private T priorEstimate = T.Zero;
        private T priorErrorCovariance = T.One;

        public KalmanFilter(T processNoiseCovariance)
        {
            this.processNoiseCovariance = processNoiseCovariance;
        }

        //public void SetEstimate(T measurement)
        //{
        //    priorEstimate = measurement;

        //    // since we got not a measurement we can set the error covariance to 0,
        //    // indicating we want to fully trust the measurement because we want to,
        //    // reach the actual signal as fast as possible.
        //    priorErrorCovariance = T.Zero;  
        //}

        public void CopyState(KalmanFilter otherFilter)
        {
            priorEstimate = otherFilter.priorEstimate;
            priorErrorCovariance = otherFilter.priorErrorCovariance;
        }

        public void BeginReset()
        {
            
        }

        public T Filter(T measurement)
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
            priorEstimate = estimate + gain * (measurement - estimate);
            // Formula: P_k+1 = (1 - K_k) * P_k
            // NOTE: [1 - gain] can never become 0, because 'gain' can only be 1 when 'errorCovariance' is infinity.
            priorErrorCovariance = (T.One - gain) * errorCovariance;

            return priorEstimate;
        }
    }
}

internal static class TaskUtility
{
    internal static async Task RepeatEvery(Func<Task> func,
        TimeSpan interval, CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                await func();
            }
            catch
            {

            }

            Task task = Task.Delay(interval, cancellationToken);

            try
            {
                await task;
            }
            catch (TaskCanceledException)
            {
                return;
            }
        }
    }
}
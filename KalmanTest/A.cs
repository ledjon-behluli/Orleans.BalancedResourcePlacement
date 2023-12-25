
using System.Numerics;

public sealed class DKalmanFilter<T> where T : unmanaged, INumber<T>
{
    private readonly T _estimatedMeasurementNoiseCovariance = T.One;
    private readonly T _estimatedProcessNoiseCovariance = T.Zero;
    private readonly T _controlWeight = T.Zero;
    private T _priorEstimate;
    private T _priorErrorCovariance;
    
    public DKalmanFilter()
    {
        _priorEstimate = T.Zero;
        _priorErrorCovariance = T.One;
    }

    /// <summary>
    /// Returns an optimal estimate (x<sub>k</sub>) for the supplied measurement (z<sub>k</sub>).
    /// </summary>
    /// <param name="measurement">The measurement (z<sub>k</sub>).</param>
    /// <returns>An optimal estimate (x<sub>k</sub>).</returns>
    public T Filter(T measurement)
    {
        return Filter(measurement, measurement);
    }

    /// <summary>
    /// Returns an optimal estimate (x<sub>k</sub>) for the supplied control (u<sub>k</sub>) and measurement (z<sub>k</sub>).
    /// </summary>
    /// <param name="control">The control (u<sub>k</sub>).</param>
    /// <param name="measurement">The measurement (z<sub>k</sub>).</param>
    /// <returns>An optimal estimate (x<sub>k</sub>).</returns>
    public T Filter(T control, T measurement)
    {
        // prediction
        T estimate = PredictEstimate(T.One - _controlWeight, _priorEstimate, _controlWeight, control);
        T errorCovariance = PredictErrorCovariance(T.One - _controlWeight, _priorErrorCovariance, _estimatedProcessNoiseCovariance);

        // correction
        T gain = CalculateGain(errorCovariance, _estimatedMeasurementNoiseCovariance);
        T estimateCorrected = CorrectEstimate(estimate, gain, measurement);
        T errorCovarianceCorrected = CorrectErrorCovariance(gain, errorCovariance);

        _priorErrorCovariance = errorCovarianceCorrected;
        return _priorEstimate = estimateCorrected;
    }

    /// <summary>
    /// Returns a prediction of the optimal estimate (predicted x<sub>k</sub>) given the control (u<sub>k</sub>) using a linear stochastic equation.
    /// </summary>
    /// <param name="priorWeight">The weight (A) for the prior estimate (prior x<sub>k</sub>).</param>
    /// <param name="priorEstimate">The prior estimate (prior x<sub>k</sub>).</param>
    /// <param name="controlWeight">The weight (B) for the control (u<sub>k</sub>).</param>
    /// <param name="control">The control (u<sub>k</sub>).</param>
    /// <returns>A prediction of the optimal estimate (predicted x<sub>k</sub>).</returns>
    private static T PredictEstimate(T priorWeight, T priorEstimate, T controlWeight, T control)
    {
        return priorWeight * priorEstimate + controlWeight * control;
    }

    /// <summary>
    /// Returns a prediction of the error covariance estimate (predicted P<sub>k</sub>).
    /// </summary>
    /// <param name="priorWeight">The weight (A) of the prior estimate (prior x<sub>k</sub>).</param>
    /// <param name="priorErrorCovariance">The prior error covariance (prior P<sub>k</sub>).</param>
    /// <param name="estimatedProcessNoiseCovariance">The estimated process noise covariance (Q).</param>
    /// <returns>An prediction of the error covariance (predicted P<sub>k</sub>).</returns>
    private static T PredictErrorCovariance(T priorWeight, T priorErrorCovariance, T estimatedProcessNoiseCovariance)
    {
        return priorWeight * priorErrorCovariance + estimatedProcessNoiseCovariance;
    }

    /// <summary>
    /// Returns the Kalman Gain (K<sub>k</sub>) used to correct predicted estimates.
    /// </summary>
    /// <param name="errorCovariance">The predicted error covariance (predicted P<sub>k</sub>).</param>
    /// <param name="estimatedMeasurementNoiseCovariance">The estimated measurement noise covariance (R).</param>
    /// <returns>The Kalman Gain (K<sub>k</sub>).</returns>
    private static T CalculateGain(T errorCovariance, T estimatedMeasurementNoiseCovariance)
    {
        return errorCovariance / (errorCovariance + estimatedMeasurementNoiseCovariance);
    }

    /// <summary>
    /// Returns an optimal estimate (x<sub>k</sub>) of the measurement (z<sub>k</sub>).
    /// </summary>
    /// <param name="estimate">The predicted estimate (predicted x<sub>k</sub>).</param>
    /// <param name="gain">The Kalman Gain (K<sub>k</sub>).</param>
    /// <param name="measurement">The measurement (z<sub>k</sub>).</param>
    /// <returns>An optimal estimate (x<sub>k</sub>).</returns>
    private static T CorrectEstimate(T estimate, T gain, T measurement)
    {
        return estimate + gain * (measurement - estimate);
    }

    /// <summary>
    /// Returns an optimal error covariance estimate (P<sub>k</sub>).
    /// </summary>
    /// <param name="gain">The Kalman Gain (<sub>k</sub>).</param>
    /// <param name="errorCovariance">The predicted error covariance estimate (predicted P<sub>k</sub>).</param>
    /// <returns>An optimal error covariance estimate (P<sub>k</sub>).</returns>
    private static T CorrectErrorCovariance(T gain, T errorCovariance)
    {
        return (T.One - gain) * errorCovariance;
    }
}
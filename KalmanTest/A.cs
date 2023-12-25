
using System.Numerics;

public sealed class DKalmanFilter<T> where T : unmanaged, INumber<T>
{
    private readonly T _estimatedMeasurementNoiseCovariance = T.One;

    private T _priorEstimate;
    private T _priorErrorCovariance;
    
    public DKalmanFilter()
    {
        _priorEstimate = T.Zero;
        _priorErrorCovariance = T.One;
    }

    public T Filter(T measurement)
    {
        // prediction
        T estimate = _priorEstimate;
        T errorCovariance = _priorErrorCovariance;

        // correction
        T gain = errorCovariance / (errorCovariance + _estimatedMeasurementNoiseCovariance);
        T estimateCorrected = estimate + gain * (measurement - estimate);
        T errorCovarianceCorrected = (T.One - gain) * errorCovariance;

        _priorErrorCovariance = errorCovarianceCorrected;
        return _priorEstimate = estimateCorrected;
    }
}
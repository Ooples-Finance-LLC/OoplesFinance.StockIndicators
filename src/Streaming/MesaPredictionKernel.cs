namespace OoplesFinance.StockIndicators.Streaming;

/// <summary>Burg autoregression with a separate temporal Hann history for each lag coefficient.</summary>
internal sealed class MesaPredictionKernel : IDisposable
{
    private readonly int _window, _order, _horizon, _smooth;
    private readonly double _h1, _h2, _h3, _l1, _l2, _l3;
    private readonly PooledRingBuffer<double> _samples;
    private readonly PooledRingBuffer<double>[] _coefficients;
    private readonly double[] _forward, _backward, _ar, _priorAr, _forecast, _weights;
    private readonly double _weightSum;
    private double _price1, _price2, _hp1, _hp2, _ssf1, _ssf2, _previousPrediction;
    private int _index;

    internal MesaPredictionKernel(int horizon, int order, int smooth, int window)
    {
        _window = Math.Max(2, window); _order = Math.Min(Math.Max(1, order), _window-1);
        _horizon = Math.Max(1, horizon); _smooth = Math.Max(1, smooth);
        var highAngle = Math.Sqrt(2)*Math.PI/_window; var highPole = Math.Exp(-highAngle);
        _h2 = 2*highPole*Math.Cos(highAngle); _h3 = -highPole*highPole; _h1 = (1+_h2-_h3)/4;
        var lowAngle = Math.Sqrt(2)*Math.PI/_smooth; var lowPole = Math.Exp(-lowAngle);
        _l2 = 2*lowPole*Math.Cos(lowAngle); _l3 = -lowPole*lowPole; _l1 = 1-_l2-_l3;
        _samples = new(_window);
        _coefficients = new PooledRingBuffer<double>[_order];
        for (var k = 0; k < _order; k++) _coefficients[k] = new(_smooth);
        _forward = new double[_window]; _backward = new double[_window];
        _ar = new double[_order+1]; _priorAr = new double[_order+1];
        _forecast = new double[_window+_horizon]; _weights = new double[_smooth];
        for (var k = 0; k < _smooth; k++) { _weights[k] = 1-Math.Cos(2*Math.PI*(k+1)/(_smooth+1)); _weightSum += _weights[k]; }
    }

    internal (double Ssf, double Predict, double PrePredict) Next(double value, bool final)
    {
        var hp = _index < 4 ? 0 : _h1*((value-_price1)-(_price1-_price2))+_h2*_hp1+_h3*_hp2;
        var ssf = _l1*(hp+_hp1)/2+_l2*_ssf1+_l3*_ssf2;
        Array.Clear(_ar, 0, _ar.Length);
        for (var j = 0; j < _window; j++)
            _forecast[j] = EhlersStreamingWindow.GetOffsetValue(_samples, ssf, _window-1-j);
        double scale = 0;
        for (var j = 0; j < _window; j++) scale = Math.Max(scale, Math.Abs(_forecast[j]));
        if (scale > 64*2.2204460492503131e-16*Math.Max(Math.Abs(value), Math.Abs(_price1)))
        {
            for (var j = 0; j < _window-1; j++) { _forward[j] = _forecast[j+1]/scale; _backward[j] = _forecast[j]/scale; }
            for (var order = 1; order <= _order; order++)
            {
                var pairs = _window-order;
                double cross = 0, energy = 0;
                for (var j = 0; j < pairs; j++) { cross += _forward[j]*_backward[j]; energy += _forward[j]*_forward[j]+_backward[j]*_backward[j]; }
                var reflection = energy == 0 ? 0 : Math.Max(-1, Math.Min(1, 2*cross/energy));
                Array.Copy(_ar, _priorAr, _ar.Length);
                for (var k = 1; k < order; k++) _ar[k] = _priorAr[k]-reflection*_priorAr[order-k];
                _ar[order] = reflection;
                // Traverse forwards: next forward residual has not yet been overwritten.
                for (var j = 0; j < pairs-1; j++)
                {
                    var backward = _backward[j]-reflection*_forward[j];
                    _forward[j] = _forward[j+1]-reflection*_backward[j+1];
                    _backward[j] = backward;
                }
            }
        }
        for (var k = 1; k <= _order; k++)
        {
            double weighted = 0;
            for (var lag = 0; lag < _smooth; lag++)
                weighted += _weights[lag]*EhlersStreamingWindow.GetOffsetValue(_coefficients[k-1], _ar[k], lag);
            _priorAr[k] = weighted/_weightSum;
        }
        for (var step = 0; step < _horizon; step++)
        {
            double prediction = 0;
            for (var k = 1; k <= _order; k++) prediction += _priorAr[k]*_forecast[_window+step-k];
            _forecast[_window+step] = prediction;
        }
        var prePredict = _forecast[_window+_horizon-1];
        var predict = (prePredict+_previousPrediction)/2;
        if (final)
        {
            _samples.TryAdd(ssf, out _);
            for (var k = 1; k <= _order; k++) _coefficients[k-1].TryAdd(_ar[k], out _);
            _price2 = _price1; _price1 = value; _hp2 = _hp1; _hp1 = hp;
            _ssf2 = _ssf1; _ssf1 = ssf; _previousPrediction = prePredict; _index++;
        }
        return (ssf, predict, prePredict);
    }
    internal void Reset()
    {
        _samples.Clear(); foreach (var history in _coefficients) history.Clear();
        _price1 = _price2 = _hp1 = _hp2 = _ssf1 = _ssf2 = _previousPrediction = 0; _index = 0;
    }
    public void Dispose() { _samples.Dispose(); foreach (var history in _coefficients) history.Dispose(); }
}

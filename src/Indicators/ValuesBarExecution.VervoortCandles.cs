#if !NETFRAMEWORK
namespace OoplesFinance.StockIndicators.Indicators;
internal static partial class ValuesBarExecution
{
    private sealed class GeneratedVervoortCandleState(bool longTerm, int length, double factor, bool customRange) : IIndicatorState, IDisposable
    {
        private readonly VervoortCandleWindow _window = new(longTerm,
            longTerm ? MovingAvgType.TripleExponentialMovingAverage : MovingAvgType.ZeroLagTripleExponentialMovingAverage, length, factor);
        private double _previousNamed;
        private bool _started;
        public double Update(in Bar bar)
        {
            var named = PriceMean.Of(bar.Open, bar.High, bar.Low, bar.Close);
            var high = bar.High; var low = bar.Low;
            if (customRange && !CalculationsHelper.IsWithinBarRange(named, low, high))
            {
                var previous = _started ? _previousNamed : named;
                high = Math.Max(previous, named); low = Math.Min(previous, named);
            }
            var point = _window.Next(named, high, low, bar.Open, bar.Close, true);
            _previousNamed = named; _started = true;
            return point.Value;
        }
        public void Reset() { _window.Reset(); _previousNamed = 0; _started = false; }
        public void Dispose() => _window.Dispose();
    }
    private sealed class GeneratedVervoortSmoothedState(int band, int range, int cascade, int smooth, double multiplier, bool customRange) : IMultiOutputState, IDisposable
    {
        private readonly VervoortSmoothedWindow _window = new(band, range, cascade, smooth, multiplier);
        private double _previousNamed;
        private bool _started;
        public void Update(in Bar bar, Span<double> outputs)
        {
            var named = PriceMean.Of(bar.High, bar.Low, bar.Close);
            var high = bar.High; var low = bar.Low;
            if (customRange && !CalculationsHelper.IsWithinBarRange(named, low, high))
            {
                var previous = _started ? _previousNamed : named;
                high = Math.Max(previous, named); low = Math.Min(previous, named);
            }
            // The legacy smoothed formula recomputes typical price after range adjustment.
            var typical = new ExactMeanAccumulator(); typical.Add(high); typical.Add(low); typical.Add(bar.Close);
            var point = _window.Next(bar.Close, typical.Mean(3), high, low, true);
            outputs[0] = point.Line; outputs[1] = point.Stochastic;
            _previousNamed = named; _started = true;
        }
        public void Reset() { _window.Reset(); _previousNamed = 0; _started = false; }
        public void Dispose() => _window.Dispose();
    }
}
#endif

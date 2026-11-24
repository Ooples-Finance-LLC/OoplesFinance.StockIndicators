#if !NETFRAMEWORK
namespace OoplesFinance.StockIndicators.Indicators;

internal static partial class ValuesBarExecution
{
    // Sinc's startup repeats the newest close against the complete kernel.
    // Preserve that original state until every published batch row has a full
    // window; the existing analytic engine can then use its unchanged publisher.
    private sealed class SincGridBatchState : IIndicatorState
    {
        private readonly IIndicatorState _original;
        private readonly WindowedSinc _indicator;
        private readonly int _count;
        private readonly BoundedOutput _target;
        private readonly double[] _startup;
        private AnalyticGridBatchState? _batch;
        private int _received;
        private bool _fallback;

        internal SincGridBatchState(IIndicatorState original, WindowedSinc indicator, int count, BoundedOutput target)
        {
            _original = original; _indicator = indicator; _count = count; _target = target;
            _startup = new double[indicator.Taps - 1];
        }

        public void Reset()
        {
            _received = 0;
            if (_batch is not null) _batch.Reset();
            else _original.Reset();
        }

        public double Update(in Bar bar)
        {
            if (_fallback) return _original.Update(in bar);
            if (_received == _indicator.Taps) return _batch!.Update(in bar);

            double value = _original.Update(in bar);
            if (_received != 0) _startup[_received - 1] = bar.Close;
            if (++_received != _indicator.Taps) return value;

            _batch ??= new AnalyticGridBatchState(_original, _indicator.BatchCoefficient, _indicator.Taps, _count - 1,
                new BoundedOutput(_target.Values, _target.Origin + 1,
                    Math.Max(_target.WriteStart, _target.Origin + _indicator.Taps)));
            // An uncertified engine immediately updates its original state.
            // Check before seeding, or these already-consumed closes would be
            // appended a second time. Rejecting latches the saved original row.
            if (!_batch.HasCertifiedPositiveMass)
            {
                _batch = null;
                _fallback = true;
                return value;
            }
            foreach (double close in _startup)
            {
                var seed = new Bar(default, 0, 0, 0, close, 0);
                _batch.Update(in seed);
            }
            return value;
        }
    }
}
#endif

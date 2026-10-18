#if !NETFRAMEWORK
namespace OoplesFinance.StockIndicators.Indicators;

internal static partial class ValuesBarExecution
{
    // Same integer-weight proof as the GPU regression recurrence. The private
    // ring also provides exact fallback seeding when a later input fails proof.
    internal struct RegressionValueState(WindowLinearRegression indicator, int count) : IIndicatorState
    {
        private Core.SmaCpuKernel.Certificate _proof = new(checked((int)(2L * indicator.Period * indicator.Period)));
        private double[]? _window;
        private int _position, _seen;
        private double _sum, _lagged;
        private IIndicatorState? _fallback;

        public void Reset()
        {
            (_fallback as IDisposable)?.Dispose();
            _fallback = null;
            _position = _seen = 0;
            _sum = _lagged = 0;
            _proof = new(checked((int)(2L * indicator.Period * indicator.Period)));
        }

        public double Update(in Bar bar)
        {
            if (_fallback is not null) return _fallback.Update(in bar);
            var weights = Weights(indicator.Period, indicator.Output);
            return Update(in bar, in weights);
        }

        internal double Update(in Bar bar, in (double First, double Step, double Denominator) full)
        {
            if (_fallback is not null) return _fallback.Update(in bar);
            if (indicator.Period == 1)
                return indicator.Output is WindowRegressionOutput.Slope or WindowRegressionOutput.Angle || bar.Close == 0 ? 0 : bar.Close;
            if (!_proof.Include(bar.Close))
            {
                _fallback = (IIndicatorState)indicator.CreateState();
                // Rebuild only the retained input window, in chronological order.
                // Local regression coordinates do not require the earlier prefix.
                for (int j = 0; j < _seen; j++)
                {
                    int slot = _seen < indicator.Period ? j : (_position + j) % _window!.Length;
                    var prior = new Bar(default, 0, 0, 0, _window![slot], 0);
                    _fallback.Update(in prior);
                }
                return _fallback.Update(in bar);
            }
            _window ??= new double[Math.Min(indicator.Period, Math.Max(1, count))];
            _window[_position] = bar.Close;
            if (++_position == _window.Length) _position = 0;
            if (_seen < indicator.Period) _seen++;
            _sum += bar.Close;
            double value;
            if (_seen == 1)
                value = indicator.Output is WindowRegressionOutput.Slope or WindowRegressionOutput.Angle ? 0 : _sum;
            else
            {
                var weights = _seen == indicator.Period ? full : Weights(_seen, indicator.Output);
                double numerator = weights.First * _sum + weights.Step * _lagged;
                value = numerator == 0 ? 0 : numerator / weights.Denominator;
                if (indicator.Output == WindowRegressionOutput.Angle) value = Math.Atan(value) * (180 / Math.PI);
            }
            _lagged += _sum;
            if (_seen == indicator.Period)
            {
                double expired = _window[_position];
                _lagged -= expired * indicator.Period;
                _sum -= expired;
            }
            return value == 0 ? 0 : value;
        }

        internal static (double First, double Step, double Denominator) Weights(int count, WindowRegressionOutput output)
        {
            double n = count;
            return output switch
            {
                WindowRegressionOutput.Slope or WindowRegressionOutput.Angle => (n - 1, -2, n * (n * n - 1) / 6),
                WindowRegressionOutput.Intercept => (2 - n, 3, n * (n + 1) / 2),
                WindowRegressionOutput.Forecast => (2 * n - 2, -3, n * (n - 1) / 2),
                _ => (2 * n - 1, -3, n * (n + 1) / 2)
            };
        }

    }

    // Mutable value storage prevents a heap state per worker. In particular,
    // this must not be readonly: Update advances the embedded ring and moments.
    private struct RegressionKernel(RegressionValueState state,
        (double First, double Step, double Denominator) full) : IBoundedKernel<RegressionKernel>
    {
        public static RegressionKernel Create(int period, int count, IIndicator indicator, in BoundedOutput target) =>
            new(new((WindowLinearRegression)indicator, count),
                RegressionValueState.Weights(period, ((WindowLinearRegression)indicator).Output));
        public double Update(in Bar bar) => state.Update(in bar, in full);
    }
}
#endif

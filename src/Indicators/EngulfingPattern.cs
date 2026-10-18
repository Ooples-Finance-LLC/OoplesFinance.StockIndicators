using OoplesFinance.StockIndicators.Validation;

namespace OoplesFinance.StockIndicators.Indicators;

/// <summary>
/// Recognizes opposite-color engulfing candle bodies, without a trend filter.
/// Returns +100 for bullish, -100 for bearish, and zero otherwise.
/// </summary>
/// <remarks>
/// The containing body may share one endpoint with the previous body, but must extend
/// past the other. Equal open and close is considered bullish. Shadows are ignored.
/// The first two bars return zero, matching the TA-Lib.NETCore engulfing lookback.
/// </remarks>
public sealed class EngulfingPattern : IndicatorBase, IIndicatorValidationContract
{
    /// <inheritdoc/>
    public override int WarmupBars => 2;

    /// <inheritdoc/>
    protected internal override object CreateState() => new State();

    /// <inheritdoc/>
    public IEnumerable<IndicatorValidationRule> ValidationRules =>
    [
        IndicatorValidationRule.Bounds(0, -100, 100),
        IndicatorValidationRule.Reference(0, Reference, 0, 0)
    ];

    // Independent interval-containment formulation; production uses directional endpoints.
    private static IReadOnlyList<double> Reference(IReadOnlyList<Bar> bars)
    {
        var result = new double[bars.Count];
        for (var i = 2; i < bars.Count; i++)
        {
            var previous = bars[i - 1];
            var current = bars[i];
            var previousUp = previous.Close >= previous.Open;
            var currentUp = current.Close >= current.Open;
            if (previousUp == currentUp) continue;
            var low = Math.Min(current.Open, current.Close);
            var high = Math.Max(current.Open, current.Close);
            var previousLow = Math.Min(previous.Open, previous.Close);
            var previousHigh = Math.Max(previous.Open, previous.Close);
            if (low <= previousLow && high >= previousHigh && (low < previousLow || high > previousHigh))
                result[i] = currentUp ? 100 : -100;
        }
        return result;
    }

    internal sealed class State : IIndicatorState
    {
        private double _open;
        private double _close;
        private int _seen;

        public void Reset() { _open = _close = 0; _seen = 0; }

        public double Update(in Bar bar)
        {
            var result = 0d;
            if (_seen == 2)
            {
                if (bar.Close >= bar.Open && _close < _open &&
                    bar.Open <= _close && bar.Close >= _open &&
                    (bar.Open < _close || bar.Close > _open)) result = 100;
                else if (bar.Close < bar.Open && _close >= _open &&
                    bar.Open >= _close && bar.Close <= _open &&
                    (bar.Open > _close || bar.Close < _open)) result = -100;
            }
            _open = bar.Open;
            _close = bar.Close;
            if (_seen < 2) _seen++;
            return result;
        }
    }
}

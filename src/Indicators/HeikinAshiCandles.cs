using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Validation;

namespace OoplesFinance.StockIndicators.Indicators;

/// <summary>Heikin-Ashi OHLCV candles with separately rounded complete averages.</summary>
/// <remarks>Close averages current OHLC; the first open averages raw open and close,
/// subsequent opens average the previous published open and close. High/low include
/// both transformed values, and volume is unchanged. Every field is present from
/// the first candle. Chaining replaces the raw close and retains the other fields.</remarks>
public sealed class HeikinAshiCandles : MultiOutputIndicatorBase, IIndicatorValidationContract
{
    /// <summary>Creates the candle transformation.</summary>
    public HeikinAshiCandles()
        : base(5) { }

    /// <summary>Recursive opening price.</summary>
    public IIndicatorOutput Open => Outputs[0];

    /// <summary>Maximum of raw high and transformed open/close.</summary>
    public IIndicatorOutput High => Outputs[1];

    /// <summary>Minimum of raw low and transformed open/close.</summary>
    public IIndicatorOutput Low => Outputs[2];

    /// <summary>Average of raw OHLC.</summary>
    public IIndicatorOutput Close => Outputs[3];

    /// <summary>Unchanged raw volume.</summary>
    public IIndicatorOutput Volume => Outputs[4];

    /// <inheritdoc/>
    public override IIndicatorOutput PrimaryOutput => Close;

    /// <inheritdoc/>
    protected internal override object CreateState() => new State();

    /// <inheritdoc/>
    public IEnumerable<IndicatorValidationRule> ValidationRules =>
        Enumerable
            .Range(0, 5)
            .Select(slot =>
                IndicatorValidationRule.Reference(
                    slot,
                    bars => Reference(bars)[slot],
                    IndicatorErrorBudget.Exact
                )
            );

    private static double[][] Reference(IReadOnlyList<Bar> bars)
    {
        var result = Enumerable.Range(0, 5).Select(_ => new double[bars.Count]).ToArray();
        for (var i = 0; i < bars.Count; i++)
        {
            var bar = bars[i];
            var close = (
                (
                    ReferenceFraction.FromDouble(bar.Open)
                    + ReferenceFraction.FromDouble(bar.High)
                    + ReferenceFraction.FromDouble(bar.Low)
                    + ReferenceFraction.FromDouble(bar.Close)
                ) / new ReferenceFraction(4)
            ).ToDouble();
            var previousOpen = i == 0 ? bar.Open : result[0][i - 1];
            var previousClose = i == 0 ? bar.Close : result[3][i - 1];
            var open = (
                (
                    ReferenceFraction.FromDouble(previousOpen)
                    + ReferenceFraction.FromDouble(previousClose)
                ) / new ReferenceFraction(2)
            ).ToDouble();
            result[0][i] = open;
            result[1][i] = Math.Max(bar.High, Math.Max(open, close));
            result[2][i] = Math.Min(bar.Low, Math.Min(open, close));
            result[3][i] = close;
            result[4][i] = bar.Volume;
        }
        return result;
    }

    private sealed class State : IMultiOutputState
    {
        private double _open,
            _close;
        private bool _started;

        public void Reset()
        {
            _open = 0;
            _close = 0;
            _started = false;
        }

        public void Update(in Bar bar, Span<double> output)
        {
            var closing = new ExactMeanAccumulator();
            closing.Add(bar.Open);
            closing.Add(bar.High);
            closing.Add(bar.Low);
            closing.Add(bar.Close);
            var opening = new ExactMeanAccumulator();
            opening.Add(_started ? _open : bar.Open);
            opening.Add(_started ? _close : bar.Close);
            _open = opening.Mean(2);
            _close = closing.Mean(4);
            _started = true;
            output[0] = _open;
            output[1] = Math.Max(bar.High, Math.Max(_open, _close));
            output[2] = Math.Min(bar.Low, Math.Min(_open, _close));
            output[3] = _close;
            output[4] = bar.Volume;
        }
    }
}

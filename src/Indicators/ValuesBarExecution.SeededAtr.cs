#if !NETFRAMEWORK
using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Validation;

namespace OoplesFinance.StockIndicators.Indicators;

internal static partial class ValuesBarExecution
{
    private static Bar FillSeededAtr(Bar[] source, double[][] output, IIndicator indicator,
        CancellationToken cancellation, OwnedBarBuffer? owned)
    {
        int period = indicator switch
        {
            SeededAverageTrueRange atr => atr.Period,
            NormalizedSeededAverageTrueRange atr => atr.Period,
            AverageTrueRangeWithDetails atr => atr.Period,
            _ => throw new InvalidOperationException("Unqualified seeded ATR plan.")
        };
        bool details = indicator is AverageTrueRangeWithDetails;
        bool normalized = indicator is NormalizedSeededAverageTrueRange;
        var state = new CompactSeededAtrWindow(period);
        Bar latest = default;
        for (int i = 0; i < source.Length; i++)
        {
            cancellation.ThrowIfCancellationRequested();
            latest = source[i];
            if (!AllFieldsFinite(in latest)) IndicatorInputDomain.Finite.Validate(in latest);
            if (owned is not null) owned[i] = latest;
            state.Add(in latest);
            if (details)
            {
                output[0][i] = state.Range.Mean(1);
                output[1][i] = state.Average.Mean(1);
                output[2][i] = state.Percent(latest.Close);
                output[3][i] = state.HasRange ? 1 : 0;
                output[4][i] = state.HasAverage ? 1 : 0;
                output[5][i] = state.HasAverage && latest.Close != 0 ? 1 : 0;
            }
            else output[0][i] = normalized ? state.Percent(latest.Close) : state.Average.Mean(1);
        }
        // The common executor validates outputs after all OHLCV inputs. An
        // overflowing unpublished range/average must not reject normalized ATR.
        return latest;
    }
}

// Local to the three seeded ATR facade mappings. Other consumers of
// SeededAtrWindow keep their established arithmetic and execution paths.
internal sealed class CompactSeededAtrWindow(int period)
{
    private bool _started;
    private double _previousClose;
    private int _seedCount;
    private JurikDyadic _seed;
    internal JurikDyadic Range { get; private set; }
    internal JurikDyadic Average { get; private set; }
    internal bool HasRange { get; private set; }
    internal bool HasAverage => _seedCount == period;

    internal double Percent(double close)
    {
        if (!HasAverage || close == 0 || Average.Sign == 0) return 0;
        var numerator = Average;
        numerator.Multiply(100);
        return numerator.Ratio(JurikDyadic.FromDouble(close));
    }

    internal void Reset()
    {
        _started = HasRange = false;
        _previousClose = 0;
        _seedCount = 0;
        _seed = Range = Average = default;
    }

    internal void Add(in Bar bar)
    {
        if (!_started)
        {
            _started = true;
            _previousClose = bar.Close;
            return;
        }
        HasRange = true;
        // Correct rounding is monotone: max(round(a), round(b), round(c))
        // equals round(max(a,b,c)). No exact-difference certificate is needed
        // for a range whose contract already rounds at this boundary.
        double range = Math.Max(bar.High - bar.Low,
            Math.Max(Math.Abs(bar.High - _previousClose), Math.Abs(bar.Low - _previousClose)));
        if (double.IsFinite(range)) Range = JurikDyadic.FromDouble(range);
        else
        {
            var extended = FearGreedWindow.TrueRange(bar.High, bar.Low, _previousClose);
            var value = JurikDyadic.FromDouble(extended.Mantissa);
            value.ScaleByPowerOfTwo(extended.UpperShift);
            Range = value;
        }
        if (_seedCount < period)
        {
            _seed.AddExact(Range);
            if (++_seedCount == period)
            {
                Average = _seed.Rounded(period);
                _seed = default;
            }
        }
        else
        {
            // Preserve the single rounding after the exact weighted sum and
            // division; separately rounding product or sum would change ATR.
            var sum = Average;
            sum.Multiply(period - 1);
            sum.AddExact(Range);
            Average = sum.Rounded(period);
        }
        _previousClose = bar.Close;
    }
}
#endif

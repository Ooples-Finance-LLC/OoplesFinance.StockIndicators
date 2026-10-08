using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Validation;

namespace OoplesFinance.StockIndicators.Indicators;

/// <summary>Recognizes a long bullish candle opening below the prior low and closing strictly above the midpoint inside the prior long bearish body. Returns 100 on a match, otherwise zero.</summary>
public sealed class PiercingLineCandle : IndicatorBase, IIndicatorValidationContract
{
    /// <summary>Creates a recognizer with a positive prior-body period below Int32.MaxValue.</summary>
    public PiercingLineCandle(int period = 10)
    {
        if (period < 1 || period == int.MaxValue)
            throw new ArgumentOutOfRangeException(nameof(period));
        Period = period;
    }

    /// <summary>Prior candles used for each mean-body threshold.</summary>
    public int Period { get; }

    /// <inheritdoc/>
    public override int WarmupBars => Period + 1;

    /// <inheritdoc/>
    protected internal override object CreateState() =>
        new PenetrationCandleState(Period, true, 0.5);

    /// <inheritdoc/>
    public IEnumerable<IndicatorValidationRule> ValidationRules =>
        [
            IndicatorValidationRule.Bounds(0, -100, 100),
            IndicatorValidationRule.Reference(
                0,
                bars => PenetrationCandleReference.Evaluate(bars, Period, true, 0.5),
                0,
                0
            ),
        ];
}

/// <summary>Recognizes a bearish candle opening above the prior high and closing strictly inside the prior long bullish body beyond the specified penetration. Returns -100 on a match, otherwise zero.</summary>
public sealed class DarkCloudCoverCandle : IndicatorBase, IIndicatorValidationContract
{
    /// <summary>Creates a recognizer with a positive prior-body period below Int32.MaxValue.</summary>
    public DarkCloudCoverCandle(int period = 10, double penetration = 0.5)
    {
        if (period < 1 || period == int.MaxValue)
            throw new ArgumentOutOfRangeException(nameof(period));
        if ((double.IsNaN(penetration) || double.IsInfinity(penetration)) || penetration < 0)
            throw new ArgumentOutOfRangeException(nameof(penetration));
        Penetration = penetration;
        Period = period;
    }

    /// <summary>Prior candles used for each mean-body threshold.</summary>
    public int Period { get; }

    /// <summary>Nonnegative fraction of the prior body that the close must penetrate strictly. Values at least one cannot match.</summary>
    public double Penetration { get; }

    /// <inheritdoc/>
    public override int WarmupBars => Period + 1;

    /// <inheritdoc/>
    protected internal override object CreateState() =>
        new PenetrationCandleState(Period, false, Penetration);

    /// <inheritdoc/>
    public IEnumerable<IndicatorValidationRule> ValidationRules =>
        [
            IndicatorValidationRule.Bounds(0, -100, 100),
            IndicatorValidationRule.Reference(
                0,
                bars => PenetrationCandleReference.Evaluate(bars, Period, false, Penetration),
                0,
                0
            ),
        ];
}

internal sealed class PenetrationCandleState(int period, bool piercing, double penetration)
    : IIndicatorState
{
    private readonly Queue<Bar> _history = new();
    private ExactMeanAccumulator _sum,
        _lagSum;
    private Bar _previous;
    private int _seen;

    public void Reset()
    {
        _history.Clear();
        _sum = _lagSum = default;
        _previous = default;
        _seen = 0;
    }

    public double Update(in Bar bar)
    {
        var result = _seen >= period + 1 && Matches(bar) ? (piercing ? 100d : -100d) : 0;
        _lagSum = _sum;
        if (_history.Count == period)
            Accumulate(_history.Dequeue(), -1);
        _history.Enqueue(bar);
        Accumulate(bar, 1);
        _previous = bar;
        if (_seen < period + 1)
            _seen++;
        return result;
    }

    private void Accumulate(in Bar bar, int sign)
    {
        _sum.Add(Math.Max(bar.Open, bar.Close), sign);
        _sum.Add(Math.Min(bar.Open, bar.Close), -sign);
    }

    private bool Matches(in Bar bar)
    {
        var priorLong = _lagSum;
        priorLong.Add(Math.Max(_previous.Open, _previous.Close), -period);
        priorLong.Add(Math.Min(_previous.Open, _previous.Close), period);
        if (priorLong.Sign >= 0)
            return false;
        var margin = new ExactMeanAccumulator();
        if (piercing)
        {
            if (
                _previous.Close >= _previous.Open
                || bar.Close < bar.Open
                || bar.Open >= _previous.Low
                || bar.Close >= _previous.Open
            )
                return false;
            var currentLong = _sum;
            currentLong.Add(bar.Close, -period);
            currentLong.Add(bar.Open, period);
            if (currentLong.Sign >= 0)
                return false;
            margin.Add(bar.Close, 2);
            margin.Add(_previous.Open, -1);
            margin.Add(_previous.Close, -1);
        }
        else
        {
            if (
                _previous.Close < _previous.Open
                || bar.Close >= bar.Open
                || bar.Open <= _previous.High
                || bar.Close <= _previous.Open
            )
                return false;
            margin.Add(_previous.Close);
            margin.Add(bar.Close, -1);
            margin.AddProduct(_previous.Close, penetration, -1);
            margin.AddProduct(_previous.Open, penetration);
        }
        return margin.Sign > 0;
    }
}

internal static class PenetrationCandleReference
{
    internal static IReadOnlyList<double> Evaluate(
        IReadOnlyList<Bar> bars,
        int period,
        bool piercing,
        double penetration
    )
    {
        var result = new double[bars.Count];
        for (var i = period + 1; i < bars.Count; i++)
        {
            var previous = bars[i - 1];
            var current = bars[i];
            var priorSum = new ReferenceFraction(0);
            var currentSum = new ReferenceFraction(0);
            for (var j = i - period - 1; j < i - 1; j++)
                priorSum += Body(bars[j]);
            for (var j = i - period; j < i; j++)
                currentSum += Body(bars[j]);
            if (Body(previous).CompareTo(priorSum / new ReferenceFraction(period)) <= 0)
                continue;
            var matched = piercing
                ? previous.Close < previous.Open
                    && current.Close >= current.Open
                    && current.Open < previous.Low
                    && current.Close < previous.Open
                    && Body(current).CompareTo(currentSum / new ReferenceFraction(period)) > 0
                    && R(current.Close)
                        .CompareTo(
                            (R(previous.Open) + R(previous.Close)) / new ReferenceFraction(2)
                        ) > 0
                : previous.Close >= previous.Open
                    && current.Close < current.Open
                    && current.Open > previous.High
                    && current.Close > previous.Open
                    && R(current.Close)
                        .CompareTo(R(previous.Close) - Body(previous) * R(penetration)) < 0;
            if (matched)
                result[i] = piercing ? 100 : -100;
        }
        return result;
    }

    private static ReferenceFraction R(double value) => ReferenceFraction.FromDouble(value);

    private static ReferenceFraction Body(Bar bar) => (R(bar.Close) - R(bar.Open)).Abs();
}

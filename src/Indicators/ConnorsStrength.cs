using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Validation;
using ReturnKey = OoplesFinance.StockIndicators.Helpers.ReturnOrderStatistic.ReturnKey;

namespace OoplesFinance.StockIndicators.Indicators;

/// <summary>Connors strength with inclusive return ranks and independently present components.</summary>
/// <remarks>Matches ConnorsStrengthSnapshot, including its delayed streak RSI,
/// initial zero return, excluded nonpositive denominators and truncated rank.
/// All four value columns have separate presence columns. History grows lazily.</remarks>
public sealed class ConnorsStrength : MultiOutputIndicatorBase, IIndicatorValidationContract
{
    /// <summary>Creates the three component periods, each at least two.</summary>
    public ConnorsStrength(int rsiPeriod = 3, int streakPeriod = 2, int rankPeriod = 100) : base(8)
    {
        if (rsiPeriod < 2) throw new ArgumentOutOfRangeException(nameof(rsiPeriod));
        if (streakPeriod < 2) throw new ArgumentOutOfRangeException(nameof(streakPeriod));
        if (rankPeriod < 2) throw new ArgumentOutOfRangeException(nameof(rankPeriod));
        RsiPeriod = rsiPeriod; StreakPeriod = streakPeriod; RankPeriod = rankPeriod;
    }
    /// <summary>Close RSI period.</summary>
    public int RsiPeriod { get; }
    /// <summary>Streak RSI period.</summary>
    public int StreakPeriod { get; }
    /// <summary>Return rank period.</summary>
    public int RankPeriod { get; }
    /// <summary>Close RSI, or zero when absent.</summary>
    public IIndicatorOutput Rsi => Outputs[0];
    /// <summary>Streak RSI, or zero when absent.</summary>
    public IIndicatorOutput StreakRsi => Outputs[1];
    /// <summary>Truncated percent rank, or zero when absent.</summary>
    public IIndicatorOutput PercentRank => Outputs[2];
    /// <summary>Combined strength, or zero when absent.</summary>
    public IIndicatorOutput Value => Outputs[3];
    /// <summary>Close RSI presence.</summary>
    public IIndicatorOutput RsiIsDefined => Outputs[4];
    /// <summary>Streak RSI presence.</summary>
    public IIndicatorOutput StreakRsiIsDefined => Outputs[5];
    /// <summary>Rank presence.</summary>
    public IIndicatorOutput PercentRankIsDefined => Outputs[6];
    /// <summary>Combined strength presence.</summary>
    public IIndicatorOutput IsDefined => Outputs[7];
    /// <inheritdoc/>
    protected internal override object CreateState() => new ConnorsStrengthState(RsiPeriod, StreakPeriod, RankPeriod);
    /// <inheritdoc/>
    public IEnumerable<IndicatorValidationRule> ValidationRules => Enumerable.Range(0, 8).Select(slot =>
        IndicatorValidationRule.ReferenceWithOverflowRejection(slot, bars =>
            ConnorsStrengthReference.Calculate(bars, RsiPeriod, StreakPeriod, RankPeriod).Select(row =>
            {
                double? value = (slot % 4) switch { 0 => row.Rsi, 1 => row.RsiStreak, 2 => row.PercentRank, _ => row.ConnorsRsi };
                return slot < 4 ? value ?? 0 : value.HasValue ? 1d : 0;
            }).ToArray(), IndicatorErrorBudget.Exact));
}

internal sealed class ConnorsStrengthState : IMultiOutputState
{
    private readonly int _rsiPeriod, _streakPeriod, _rankPeriod;
    private readonly IMultiOutputState _rsi, _streakRsi;
    private readonly double[] _priceOutput = new double[2], _streakOutput = new double[2];
    private readonly Queue<ReturnKey?> _returns = new();
    private long _index = -1;
    private double _previous;
    private int _streak;

    internal ConnorsStrengthState(int rsiPeriod, int streakPeriod, int rankPeriod)
    {
        _rsiPeriod = rsiPeriod; _streakPeriod = streakPeriod; _rankPeriod = rankPeriod;
        _rsi = (IMultiOutputState)new WilderStrengthOscillator(rsiPeriod, WilderStrengthConvention.RsiHundredFlat).CreateState();
        _streakRsi = (IMultiOutputState)new WilderStrengthOscillator(streakPeriod, WilderStrengthConvention.RsiHundredFlat).CreateState();
    }

    public void Reset()
    {
        _rsi.Reset(); _streakRsi.Reset(); _returns.Clear();
        Array.Clear(_priceOutput, 0, 2); Array.Clear(_streakOutput, 0, 2);
        _index = -1; _previous = 0; _streak = 0;
    }

    public void Update(in Bar bar, Span<double> output)
    {
        if (!FrameworkCompatibility.IsFinite(bar.Close)) throw new ArgumentOutOfRangeException(nameof(bar));
        output.Clear();
        _index++;
        _rsi.Update(bar, _priceOutput);
        ReturnKey? change = new ReturnKey(1, 1);
        if (_index > 0)
        {
            int direction = bar.Close.CompareTo(_previous);
            _streak = direction == 0 ? 0 : direction > 0 ? Math.Max(0, _streak) + 1 : Math.Min(0, _streak) - 1;
            _streakRsi.Update(new Bar(bar.Time, _streak, _streak, _streak, _streak, 0), _streakOutput);
            change = _previous > 0 ? new ReturnKey(bar.Close, _previous) : null;
        }
        _previous = bar.Close;
        if (_returns.Count > _rankPeriod) _returns.Dequeue();
        _returns.Enqueue(change);
        if (_index >= _rankPeriod)
        {
            int count = 0;
            if (change.HasValue)
                foreach (var prior in _returns)
                    if (prior.HasValue && prior.Value.CompareTo(change.Value) < 0) count++;
            output[2] = (100L * count) / _rankPeriod; output[6] = 1; // NOSONAR: S2184 - Connors percent rank deliberately truncates integer division before publication.
        }
        if (_priceOutput[1] > 0) { output[0] = _priceOutput[0]; output[4] = 1; }
        if (_index >= (long)_streakPeriod + 2 && _streakOutput[1] > 0)
        { output[1] = _streakOutput[0]; output[5] = 1; }
        if (_index >= (long)Math.Max(_rsiPeriod, Math.Max(_streakPeriod, _rankPeriod)) + 1
            && output[4] > 0 && output[5] > 0 && output[6] > 0)
        {
            var mean = new ExactMeanAccumulator();
            mean.Add(output[0]); mean.Add(output[1]); mean.Add(output[2]);
            output[3] = mean.Mean(3); output[7] = 1;
        }
    }
}

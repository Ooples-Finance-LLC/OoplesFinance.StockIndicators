using System.Numerics;
using OoplesFinance.StockIndicators.Helpers;

namespace OoplesFinance.StockIndicators.Indicators;

/// <summary>The four independently nullable Connors strength components.</summary>
public sealed record ConnorsStrengthValue(
    double? Rsi,
    double? RsiStreak,
    double? PercentRank,
    double? ConnorsRsi
);

/// <summary>Connors RSI using close RSI, streak RSI, and truncated integer percent ranks.</summary>
/// <remarks>Retains the historical inclusive rank window, initial zero return and
/// delayed streak publication. Returns with nonpositive previous prices do not
/// participate in comparisons. Distinct returns are compared exactly, without
/// overflowing differences or tolerance ties. Wilder means use the existing
/// extended-exponent strength engine. The final three-component mean rounds once.
/// Input order is preserved; each call starts fresh and storage grows lazily.</remarks>
public static class ConnorsStrengthSnapshot
{
    /// <summary>Calculates Connors components with all periods at least two.</summary>
    public static IReadOnlyList<ConnorsStrengthValue> Calculate(
        IReadOnlyList<Bar> bars,
        int rsiPeriod = 3,
        int streakPeriod = 2,
        int rankPeriod = 100
    )
    {
        ArgumentNullException.ThrowIfNull(bars);
        if (rsiPeriod < 2)
            throw new ArgumentOutOfRangeException(nameof(rsiPeriod));
        if (streakPeriod < 2)
            throw new ArgumentOutOfRangeException(nameof(streakPeriod));
        if (rankPeriod < 2)
            throw new ArgumentOutOfRangeException(nameof(rankPeriod));
        foreach (var bar in bars)
            if (!double.IsFinite(bar.Close))
                throw new ArgumentOutOfRangeException(nameof(bars));
        var rsi = (IMultiOutputState)
            new WilderStrengthOscillator(
                rsiPeriod,
                WilderStrengthConvention.RsiHundredFlat
            ).CreateState();
        var streakRsi = (IMultiOutputState)
            new WilderStrengthOscillator(
                streakPeriod,
                WilderStrengthConvention.RsiHundredFlat
            ).CreateState();
        var priceOutput = new double[2];
        var streakOutput = new double[2];
        var result = new ConnorsStrengthValue[bars.Count];
        var returns = new Queue<(BigInteger N, BigInteger D)>();
        var streak = 0;
        for (var i = 0; i < bars.Count; i++)
        {
            var bar = bars[i];
            rsi.Update(bar, priceOutput);
            var change = (N: BigInteger.Zero, D: BigInteger.One);
            if (i > 0)
            {
                var previous = bars[i - 1].Close;
                var direction = bar.Close.CompareTo(previous);
                streak =
                    direction == 0 ? 0
                    : direction > 0 ? Math.Max(0, streak) + 1
                    : Math.Min(0, streak) - 1;
                streakRsi.Update(
                    new Bar(bar.Time, streak, streak, streak, streak, 0),
                    streakOutput
                );
                change =
                    previous > 0
                        ? (
                            ExactVarianceWindow.Units(bar.Close)
                                - ExactVarianceWindow.Units(previous),
                            ExactVarianceWindow.Units(previous)
                        )
                        : (BigInteger.Zero, BigInteger.Zero);
            }
            if (returns.Count > rankPeriod)
                returns.Dequeue();
            returns.Enqueue(change);
            double? rank = null;
            if (i >= rankPeriod)
            {
                var count = change.D.IsZero
                    ? 0
                    : returns.Count(r => !r.D.IsZero && r.N * change.D < change.N * r.D);
                rank = (100L * count) / rankPeriod;
            }
            double? priceValue = priceOutput[1] > 0 ? priceOutput[0] : null;
            double? streakValue =
                i >= (long)streakPeriod + 2 && streakOutput[1] > 0 ? streakOutput[0] : null;
            double? combined = null;
            if (
                i >= (long)Math.Max(rsiPeriod, Math.Max(streakPeriod, rankPeriod)) + 1
                && priceValue.HasValue
                && streakValue.HasValue
                && rank.HasValue
            )
            {
                var mean = new ExactMeanAccumulator();
                mean.Add(priceValue.Value);
                mean.Add(streakValue.Value);
                mean.Add(rank.Value);
                combined = mean.Mean(3);
            }
            result[i] = new(priceValue, streakValue, rank, combined);
        }
        return result;
    }
}

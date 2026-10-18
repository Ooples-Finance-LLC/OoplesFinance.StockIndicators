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
        if (bars is null) throw new ArgumentNullException(nameof(bars));
        if (rsiPeriod < 2)
            throw new ArgumentOutOfRangeException(nameof(rsiPeriod));
        if (streakPeriod < 2)
            throw new ArgumentOutOfRangeException(nameof(streakPeriod));
        if (rankPeriod < 2)
            throw new ArgumentOutOfRangeException(nameof(rankPeriod));
        foreach (var bar in bars)
            if (!FrameworkCompatibility.IsFinite(bar.Close))
                throw new ArgumentOutOfRangeException(nameof(bars));
        var state = new ConnorsStrengthState(rsiPeriod, streakPeriod, rankPeriod);
        var output = new double[8];
        var result = new ConnorsStrengthValue[bars.Count];
        for (int i = 0; i < bars.Count; i++)
        {
            state.Update(bars[i], output);
            result[i] = new(output[4] > 0 ? output[0] : null,
                output[5] > 0 ? output[1] : null, output[6] > 0 ? output[2] : null,
                output[7] > 0 ? output[3] : null);
        }
        return result;
    }
}

using System.Text;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

/// <summary>The libraries this benchmark measures against.</summary>
public enum CompetitorLibrary
{
    /// <summary>This library, through the v2 builder.</summary>
    OoplesV2,

    /// <summary>This library, through the v1 batch extension methods.</summary>
    OoplesV1,

    /// <summary>Skender.Stock.Indicators 2.7.3.</summary>
    Skender,

    /// <summary>TALib.NETCore 0.5.0.</summary>
    TaLib,

    /// <summary>Trady.Analysis 3.2.8 over Trady.Core 3.2.9.</summary>
    Trady,

    /// <summary>QuanTAlib 1.0.0.</summary>
    QuanTAlib
}

/// <summary>The indicators the head-to-head benchmark covers.</summary>
public enum BenchmarkedIndicator
{
    Sma,
    Ema,
    Rsi,
    Atr,
    BollingerBands,
    Macd,
    Stochastic
}

/// <summary>How a library offers an indicator, which decides what can fairly be measured.</summary>
public enum IndicatorSupport
{
    /// <summary>Not shipped by that library at all, so there is nothing to measure.</summary>
    Absent,

    /// <summary>Shipped as a whole-series computation only.</summary>
    Batch,

    /// <summary>Shipped with per-bar state, so a new bar costs one update rather than a recomputation.</summary>
    Incremental
}

/// <summary>
/// What each library actually ships, established by reflecting over the packages rather than from their
/// documentation.
///
/// <para>This exists because a timing table alone misleads. "QuanTAlib has no RSI row" and "QuanTAlib's RSI was
/// too slow to include" look identical in a benchmark result and mean opposite things, and a reader comparing
/// libraries needs the second axis - what is there at all - at least as much as the first. Printing it from the
/// same run that produces the timings keeps the two from drifting apart.</para>
/// </summary>
internal static class CompetitorCoverage
{
    private static readonly Dictionary<(BenchmarkedIndicator, CompetitorLibrary), IndicatorSupport> Support =
        new()
        {
            // This library: every indicator has a batch arm and a streaming state.
            { (BenchmarkedIndicator.Sma, CompetitorLibrary.OoplesV2), IndicatorSupport.Incremental },
            { (BenchmarkedIndicator.Ema, CompetitorLibrary.OoplesV2), IndicatorSupport.Incremental },
            { (BenchmarkedIndicator.Rsi, CompetitorLibrary.OoplesV2), IndicatorSupport.Incremental },
            { (BenchmarkedIndicator.Atr, CompetitorLibrary.OoplesV2), IndicatorSupport.Incremental },
            { (BenchmarkedIndicator.BollingerBands, CompetitorLibrary.OoplesV2), IndicatorSupport.Incremental },
            { (BenchmarkedIndicator.Macd, CompetitorLibrary.OoplesV2), IndicatorSupport.Incremental },
            { (BenchmarkedIndicator.Stochastic, CompetitorLibrary.OoplesV2), IndicatorSupport.Incremental },

            { (BenchmarkedIndicator.Sma, CompetitorLibrary.OoplesV1), IndicatorSupport.Batch },
            { (BenchmarkedIndicator.Ema, CompetitorLibrary.OoplesV1), IndicatorSupport.Batch },
            { (BenchmarkedIndicator.Rsi, CompetitorLibrary.OoplesV1), IndicatorSupport.Batch },
            { (BenchmarkedIndicator.Atr, CompetitorLibrary.OoplesV1), IndicatorSupport.Batch },
            { (BenchmarkedIndicator.BollingerBands, CompetitorLibrary.OoplesV1), IndicatorSupport.Batch },
            { (BenchmarkedIndicator.Macd, CompetitorLibrary.OoplesV1), IndicatorSupport.Batch },
            { (BenchmarkedIndicator.Stochastic, CompetitorLibrary.OoplesV1), IndicatorSupport.Batch },

            // Skender 2.7.3 is batch-only; the streaming API arrived in 3.x.
            { (BenchmarkedIndicator.Sma, CompetitorLibrary.Skender), IndicatorSupport.Batch },
            { (BenchmarkedIndicator.Ema, CompetitorLibrary.Skender), IndicatorSupport.Batch },
            { (BenchmarkedIndicator.Rsi, CompetitorLibrary.Skender), IndicatorSupport.Batch },
            { (BenchmarkedIndicator.Atr, CompetitorLibrary.Skender), IndicatorSupport.Batch },
            { (BenchmarkedIndicator.BollingerBands, CompetitorLibrary.Skender), IndicatorSupport.Batch },
            { (BenchmarkedIndicator.Macd, CompetitorLibrary.Skender), IndicatorSupport.Batch },
            { (BenchmarkedIndicator.Stochastic, CompetitorLibrary.Skender), IndicatorSupport.Batch },

            // TA-Lib is span-in, span-out with no state of its own.
            { (BenchmarkedIndicator.Sma, CompetitorLibrary.TaLib), IndicatorSupport.Batch },
            { (BenchmarkedIndicator.Ema, CompetitorLibrary.TaLib), IndicatorSupport.Batch },
            { (BenchmarkedIndicator.Rsi, CompetitorLibrary.TaLib), IndicatorSupport.Batch },
            { (BenchmarkedIndicator.Atr, CompetitorLibrary.TaLib), IndicatorSupport.Batch },
            { (BenchmarkedIndicator.BollingerBands, CompetitorLibrary.TaLib), IndicatorSupport.Batch },
            { (BenchmarkedIndicator.Macd, CompetitorLibrary.TaLib), IndicatorSupport.Batch },
            { (BenchmarkedIndicator.Stochastic, CompetitorLibrary.TaLib), IndicatorSupport.Batch },

            // Trady computes in decimal, whole-series. Its stochastic ships only as an indicator class with no
            // extension-method entry point, so it is not measured here.
            { (BenchmarkedIndicator.Sma, CompetitorLibrary.Trady), IndicatorSupport.Batch },
            { (BenchmarkedIndicator.Ema, CompetitorLibrary.Trady), IndicatorSupport.Batch },
            { (BenchmarkedIndicator.Rsi, CompetitorLibrary.Trady), IndicatorSupport.Batch },
            { (BenchmarkedIndicator.Atr, CompetitorLibrary.Trady), IndicatorSupport.Batch },
            { (BenchmarkedIndicator.BollingerBands, CompetitorLibrary.Trady), IndicatorSupport.Batch },
            { (BenchmarkedIndicator.Macd, CompetitorLibrary.Trady), IndicatorSupport.Batch },
            { (BenchmarkedIndicator.Stochastic, CompetitorLibrary.Trady), IndicatorSupport.Absent },

            // QuanTAlib 1.0.0 ships moving averages, statistics and ATR; the momentum family is not there.
            { (BenchmarkedIndicator.Sma, CompetitorLibrary.QuanTAlib), IndicatorSupport.Incremental },
            { (BenchmarkedIndicator.Ema, CompetitorLibrary.QuanTAlib), IndicatorSupport.Incremental },
            { (BenchmarkedIndicator.Rsi, CompetitorLibrary.QuanTAlib), IndicatorSupport.Absent },
            { (BenchmarkedIndicator.Atr, CompetitorLibrary.QuanTAlib), IndicatorSupport.Incremental },
            { (BenchmarkedIndicator.BollingerBands, CompetitorLibrary.QuanTAlib), IndicatorSupport.Absent },
            { (BenchmarkedIndicator.Macd, CompetitorLibrary.QuanTAlib), IndicatorSupport.Absent },
            { (BenchmarkedIndicator.Stochastic, CompetitorLibrary.QuanTAlib), IndicatorSupport.Absent }
        };

    public static IndicatorSupport SupportFor(BenchmarkedIndicator indicator, CompetitorLibrary library) =>
        Support.TryGetValue((indicator, library), out var support) ? support : IndicatorSupport.Absent;

    /// <summary>Renders the table as markdown, ready to paste into the benchmarks README.</summary>
    public static string ToMarkdown()
    {
        var libraries = Enum.GetValues<CompetitorLibrary>();
        var builder = new StringBuilder();

        builder.Append("| Indicator |");
        foreach (var library in libraries)
        {
            builder.Append(' ').Append(DisplayName(library)).Append(" |");
        }

        builder.AppendLine();
        builder.Append("| --- |");
        foreach (var _ in libraries)
        {
            builder.Append(" --- |");
        }

        builder.AppendLine();

        foreach (var indicator in Enum.GetValues<BenchmarkedIndicator>())
        {
            builder.Append("| ").Append(indicator).Append(" |");
            foreach (var library in libraries)
            {
                builder.Append(' ').Append(Describe(SupportFor(indicator, library))).Append(" |");
            }

            builder.AppendLine();
        }

        return builder.ToString();
    }

    private static string DisplayName(CompetitorLibrary library) => library switch
    {
        CompetitorLibrary.OoplesV2 => "This library (v2)",
        CompetitorLibrary.OoplesV1 => "This library (v1)",
        CompetitorLibrary.Skender => "Skender 2.7.3",
        CompetitorLibrary.TaLib => "TA-Lib 0.5.0",
        CompetitorLibrary.Trady => "Trady 3.2.8",
        CompetitorLibrary.QuanTAlib => "QuanTAlib 1.0.0",
        _ => library.ToString()
    };

    private static string Describe(IndicatorSupport support) => support switch
    {
        IndicatorSupport.Incremental => "batch + streaming",
        IndicatorSupport.Batch => "batch only",
        IndicatorSupport.Absent => "not shipped",
        _ => "unknown"
    };
}

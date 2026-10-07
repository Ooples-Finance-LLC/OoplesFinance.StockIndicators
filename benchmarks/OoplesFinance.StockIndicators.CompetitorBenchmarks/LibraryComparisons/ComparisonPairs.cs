using OoplesFinance.StockIndicators.Builder;
using Skender.Stock.Indicators;
using TALib;
using Trady.Analysis.Extension;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

// Present distinguishes a genuinely absent result from an invalid numeric value.
// Missing mature values use NaN plus an explicit false flag; a null mask means all present.
internal sealed record ComparisonOutput(int FirstValid, double[] Values, bool[]? Present = null);
internal sealed record ComparisonSeries(IReadOnlyDictionary<string, ComparisonOutput> Outputs)
{
    internal ComparisonSeries(int firstValid, double[] values)
        : this(new Dictionary<string, ComparisonOutput>(StringComparer.Ordinal) { ["Value"] = new(firstValid, values) }) { }
}
internal sealed record ComparisonPair(string Id, string Indicator, Func<CompetitorData, int, ComparisonSeries> Competitor,
    Func<CompetitorData, int, ComparisonSeries>? Library = null,
    Func<CompetitorData, int, ComparisonSeries>? Reference = null, string[]? OutputNames = null, int MinimumInputCount = 1,
    Func<CompetitorData, int, ComparisonSeries>? CompetitorReference = null,
    OoplesFinance.StockIndicators.Validation.IndicatorErrorBudget? ErrorBudget = null)
{
    internal bool IsCandle => Id.Contains(".Candles.", StringComparison.Ordinal) || Id.Contains(".Candlestick.", StringComparison.Ordinal) || Id is "Skender.GetDoji" or "Skender.GetMarubozu";
    public override string ToString() => Id;
    internal ComparisonSeries Ooples(CompetitorData data, int period)
    {
        if (Library is not null) return Library(data, period);
        OoplesFinance.StockIndicators.Indicators.IIndicator indicator = Indicator switch
        {
            "Sma" => new OoplesFinance.StockIndicators.Indicators.Sma(period),
            "Wma" => new OoplesFinance.StockIndicators.Indicators.Wma(period),
            "Ema" => new OoplesFinance.StockIndicators.Indicators.Ema(period),
            _ => throw new InvalidOperationException("No Ooples runner registered for " + Id)
        };
        using var run = new StockIndicatorBuilder().ConfigureSource(OoplesFinance.StockIndicators.Indicators.Bars.From(data.IndicatorBars))
            .ConfigureIndicators(indicator).BuildAsync().GetAwaiter().GetResult();
        return new(Math.Min(period - 1, data.Count), run[indicator.Outputs[0]].ToArray());
    }
}

internal static class ComparisonPairs
{
    internal static readonly ComparisonPair[] All = [
        new("Skender.GetSma", "Sma", (data, period) => Aligned(data.Quotes.GetSma(period).Select(row => row.Sma ?? double.NaN).ToArray(), period)),
        new("Skender.GetWma", "Wma", (data, period) => Aligned(data.Quotes.GetWma(period).Select(row => row.Wma ?? double.NaN).ToArray(), period)),
        new("TaLib.Functions.Sma", "Sma", (data, period) => TaLib(data, period, false)),
        new("TaLib.Functions.Wma", "Wma", (data, period) => TaLib(data, period, true)),
        new("Trady.Indicator.SimpleMovingAverage", "Sma", (data, period) => Aligned(data.Candles.Sma(period).Select(row => (double?)row.Tick ?? double.NaN).ToArray(), period)),
        new("Trady.Indicator.WeightedMovingAverage", "Wma", (data, period) => Aligned(data.Candles.Wma(period).Select(row => (double?)row.Tick ?? double.NaN).ToArray(), period)),
        new("QuanTAlib.Sma", "Sma", (data, period) => Quan(data, period, false)),
        new("TaLib.Candles.Engulfing", "Engulfing", CandleComparison.TaLib, CandleComparison.Ooples, (data, _) => CandleComparison.Reference(data)),
        .. TradyCandleComparison.Pairs,
        .. DirectionalComparison.Pairs,
        SumDirectionalComparison.Pair,
        .. TaDirectionalComparison.Pairs,
        .. TillsonComparison.Pairs,
        .. TripleRateComparison.Pairs,
        .. SeededMomentumComparison.Pairs,
        PriceRelativeComparison.Pair,
        .. McGinleyComparison.Pairs,
        .. LogVolatilityComparison.Pairs,
        .. EmaDifferenceSignalComparison.Pairs,
        .. AlignedMacdComparison.Pairs,
        .. SmoothedAccumulationComparison.Pairs,
        .. SeededAdaptiveComparison.Pairs,
        .. SeededPhaseComparison.Pairs,
        DelayedPhaseComparison.Pair(),
        DeviationRatioComparison.Pair(),
        .. DeviationBandsComparison.Pairs,
        ClassicAverageComparison.Pair(),
        VariablePeriodComparison.Pair(),
        ClassicStochasticRsiComparison.Pair(),
        WindowStochasticComparison.Pair(),
        ClassicMacdComparison.Pair(),
        ClassicStochasticComparison.Pair(),
        ClassicStochasticComparison.Pair(true),
        HilbertCycleComparison.Pair(true),
        HilbertCycleComparison.Pair(false),
        DelayedHilbertTrendComparison.Pair(),
        HilbertCycleSignalComparison.Pair(0),
        HilbertCycleSignalComparison.Pair(1),
        HilbertCycleSignalComparison.Pair(2),
        SeededHilbertTrendComparison.Pair(),
        RangeAdaptiveComparison.Pair(true),
        RangeAdaptiveComparison.Pair(false),
        AtrTrailingComparison.Pair(OoplesFinance.StockIndicators.Indicators.AtrTrailBasis.Close),
        AtrTrailingComparison.Pair(OoplesFinance.StockIndicators.Indicators.AtrTrailBasis.Midpoint),
        VolatilityStopComparison.Pair(),
        WindowFisherComparison.Pair(),
        RegressionChannelComparison.Pair(),
        .. Enumerable.Range(0, 4).Select(i => ParabolicComparison.Pair(i)),
        ZigZagComparison.Pair(),
        RenkoComparison.Pair(),
        RenkoComparison.Pair(true),
        JurikComparison.Pair(),
        HurstComparison.Pair(),
        DynamicMomentumComparison.Pair(),
        KlingerComparison.Pair(),
        PivotTrendComparison.Pair(),
        FixedKernelSnapshotComparison.Pair(true),
        FixedKernelSnapshotComparison.Pair(false),
        MedianAdaptiveComparison.Pair(),
        RelativeVolatilityComparison.Pair(),
        OldestHilbertComparison.Pair(),
        ConnorsComparison.Pair(),
        SchaffCycleComparison.Pair(),
        PivotLevelComparison.Pair(false),
        PivotLevelComparison.Pair(true),
        .. ClassicOscillatorComparison.Pairs,
        ClassicBandsComparison.Pair(),
        TradyAdaptiveComparison.Pair(),
        TaAdaptiveComparison.Pair(),
        RangeAccelerationComparison.Pair,
        IchimokuCloudComparison.Pair(),
        ExtendedCloudComparison.Pair(),
        .. AverageRangeDojiComparison.Pairs,
        .. BodyShadowComparison.Pairs,
        .. ContextReversalComparison.Pairs,
        .. TradyExtremaComparison.Pairs,
        .. PriceComparison.Pairs,
        .. PercentileCandleComparison.Pairs,
        .. ContainmentCandleComparison.Pairs,
        .. SequenceCandleComparison.Pairs,
        .. ExponentialAverageComparison.Pairs,
        .. ExtrapolatedAverageComparison.Pairs,
        .. TriangularSumComparison.Pairs,
        .. ObvComparison.Pairs,
        .. PenetrationCandleComparison.Pairs,
        .. StrictHaramiComparison.Pairs,
        DelayedDarkCloudComparison.Pair,
        .. NeckCandleComparison.Pairs,
        .. TripleBodyComparison.Pairs,
        .. KickingRickshawComparison.Pairs,
        .. MatchedLinesComparison.Pairs,
        .. CrowSoldierComparison.Pairs,
        .. StarReversalComparison.Pairs,
        .. GapContinuationComparison.Pairs,
        .. ExtendedReversalComparison.Pairs,
        .. HikkakeComparison.Pairs,
        .. FiveCandleContinuationComparison.Pairs,
        .. ExhaustionComparison.Pairs,
        .. TrendTasukiComparison.Pairs,
        .. TrendBabyComparison.Pairs,
        .. TrendMethodsComparison.Pairs,
        .. TrendStarComparison.Pairs,
        .. PriceCandleComparison.Pairs,
        .. PriceChangeComparison.Pairs,
        .. TradyMomentumComparison.Pairs,
        .. TradyReturnComparison.Pairs,
        SkenderRocComparison.Pair,
        SkenderRocBandsComparison.Pair,
        .. WindowExtremeComparison.Pairs,
        .. BalanceOfPowerComparison.Pairs,
        .. AccumulationDistributionComparison.Pairs,
        .. PercentileComparison.Pairs,
        .. WilderAverageComparison.Pairs,
        ReverseWilderComparison.Pair,
        ZeroSeedLaguerreComparison.Pair,
        ElderRayComparison.Pair,
        .. ExpandingRecurrenceComparison.Pairs,
        HoltWinterComparison.Pair,
        EntropyComparison.Pair,
        .. ElementaryMathComparison.Pairs,
        .. TranscendentalComparison.Pairs,
        .. CircularComparison.Pairs,
        AwesomeComparison.Pair,
        HeikinAshiComparison.Pair,
        .. PathRatioComparison.Pairs,
        .. WilderStrengthComparison.Pairs,
        .. NullableStrengthComparison.Pairs,
        .. AverageDifferenceComparison.Pairs,
        .. RetrospectivePriceComparison.Pairs,
        .. StochasticSmaComparison.Pairs,
        .. StochasticMomentumComparison.Pairs,
        .. StochasticRsiComparison.Pairs,
        .. DecayingExtremeComparison.Pairs,
        .. CompensatedAverageComparison.Pairs,
        .. MassNormalizedComparison.Pairs,
        .. SampleShapeComparison.Pairs,
        .. PairStatisticsComparison.Pairs,
        .. ReturnBetaComparison.Pairs,
        .. CommodityChannelComparison.Pairs,
        UlcerComparison.Pair,
        ChoppinessComparison.Pair,
        .. MoneyFlowIndexComparison.Pairs,
        .. ChandelierComparison.Pairs,
        .. AtrEnvelopeComparison.Pairs,
        .. TrueRangeRatioComparison.Pairs,
        .. AlligatorComparison.Pairs,
        .. FixedWeightedComparison.Pairs,
        .. AnalyticKernelComparison.Pairs,
        .. AlmaComparison.Pairs,
        .. HullComparison.Pairs,
        EnvelopeComparison.Pair,
        WindowModeComparison.Pair,
        .. SeededAtrComparison.Pairs,
        QuanAtrComparison.Pair,
        NormalizedAtrComparison.Pair,
        .. WindowRegressionComparison.Pairs,
        .. ConvolutionComparison.Pairs,
        MeanStartupEndpointComparison.Pair,
        RegressionStatisticsComparison.Pair,
        CurvatureComparison.Pair,
        RegressionStatisticsComparison.SnapshotPair,
        .. DispersionComparison.Pairs,
        SkenderDeviationComparison.Pair,
        .. MeanErrorComparison.Pairs,
        .. PriceWindowChannelComparison.Pairs,
        .. AroonComparison.Pairs,
        .. VolumePriceComparison.Pairs,
        MoneyFlowDetailComparison.Pair,
        .. VolumeRecurrenceComparison.Pairs,
        .. ExtremaComparison.Pairs
    ];

    internal static ComparisonPair Get(string id) => All.Single(pair => pair.Id == id);
    private static ComparisonSeries Aligned(double[] values, int period) => new(Math.Min(period - 1, values.Length), values);

    private static ComparisonSeries Quan(CompetitorData data, int period, bool weighted)
    {
        QuanTAlib.AbstractBase average = weighted ? new QuanTAlib.Wma(period) : new QuanTAlib.Sma(period);
        var values = data.Closes.Select(close => average.Calc(new QuanTAlib.TValue(close, true, false)).Value).ToArray();
        return Aligned(values, period);
    }

    private static ComparisonSeries TaLib(CompetitorData data, int period, bool weighted)
    {
        var output = new double[data.Count];
        Range range;
        var code = weighted ? Functions.Wma<double>(data.Closes, Range.All, output, out range, period)
            : Functions.Sma<double>(data.Closes, Range.All, output, out range, period);
        // Version 0.5.0 rejects a one-bar input before checking lookback. There are
        // no mature values to compare; require that documented adapter status explicitly.
        if (data.Count == 1 && code == TALib.Core.RetCode.OutOfRangeParam)
            return new(1, [double.NaN]);
        if (code != TALib.Core.RetCode.Success) throw new InvalidOperationException("TA-Lib returned " + code);
        var values = Enumerable.Repeat(double.NaN, data.Count).ToArray();
        var (start, count) = range.GetOffsetAndLength(data.Count);
        Array.Copy(output, 0, values, start, count);
        var first = Math.Min(period - 1, data.Count);
        if (count != data.Count - first || (count > 0 && start != first))
            throw new InvalidOperationException("Unexpected TA-Lib output alignment.");
        return new(first, values);
    }
}

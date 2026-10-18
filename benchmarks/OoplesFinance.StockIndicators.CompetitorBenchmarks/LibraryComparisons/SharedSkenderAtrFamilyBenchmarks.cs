using BenchmarkDotNet.Attributes;
using OoplesFinance.StockIndicators.Indicators;
using Skender.Stock.Indicators;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

[MemoryDiagnoser, Config(typeof(TensorsGpuTimingConfig))]
public class SharedSkenderAtrFamilyBenchmarks : SharedSkenderBenchmarkBase
{
    private const int Period = 20;
    [Params("Skender.GetAtrStop", "Skender.GetSuperTrend", "Skender.GetChandelier", "Skender.GetKeltner", "Skender.GetStarcBands")]
    public string Operation { get; set; } = "";
    private IEnumerable<Quote>[] _quotePrefixes = null!;

    [GlobalSetup]
    public void Setup()
    {
        if (Operation is not ("Skender.GetAtrStop" or "Skender.GetSuperTrend" or "Skender.GetChandelier" or "Skender.GetKeltner" or "Skender.GetStarcBands"))
            throw new ArgumentException("Unknown Skender ATR-family operation.");
        var (pair, data) = PrepareNativeFixture(Operation);
        // Cache only input views. Each measured call still runs every full prefix
        // and allocates its native results and the joined output.
        _quotePrefixes = _ends.Select(end => _quotes.Take(end)).ToArray();
        if (Operation == "Skender.GetChandelier")
        {
            var expected = ChandelierComparison.Reference(_bars, Period, 3, true, 0);
            Array.Clear(expected[1]);
            VerifyOwnedColumns(expected);
        }
        else if (Operation == "Skender.GetStarcBands")
        {
            var expected = AtrEnvelopeComparison.Reference(_bars, Period, Period, 2, 1);
            expected[3] = new double?[Count];
            VerifyOwnedColumns(expected);
        }
        else SharedBenchmarkSupport.VerifyMappedOutputs(_bars, Create, pair.Reference!(data, Period), pair.OutputNames!);
        var nativeExpected = pair.CompetitorReference!(data, Period);
        VerifyNative(nativeExpected, NativeSingle(), NativeParallel(), Normalize, Operation, pair.ErrorBudget);
    }

    private void VerifyOwnedColumns(double?[][] expected)
    {
        foreach (var history in Enum.GetValues<OoplesFinance.StockIndicators.Builder.IndicatorHistoryMode>())
        {
            var owner = Create();
            using var run = SharedBenchmarkSupport.BuildCpu(_bars, owner, history);
            for (int slot = 0; slot < expected.Length; slot++)
            {
                AsinFeasibilityBenchmarks.RequireSame(expected[slot].Select(v => v ?? 0).ToArray(), run[owner.Outputs[slot]].ToArray());
                AsinFeasibilityBenchmarks.RequireSame(expected[slot].Select(v => v.HasValue ? 1d : 0d).ToArray(), run[owner.Outputs[slot + expected.Length]].ToArray());
            }
        }
    }

    protected override IIndicator Create() => Operation switch
    {
        "Skender.GetAtrStop" => new SeededAtrTrailingStop(Period),
        "Skender.GetSuperTrend" => new SeededAtrTrailingStop(Period, basis: AtrTrailBasis.Midpoint),
        "Skender.GetChandelier" => new WindowChandelierExit(Period, selection: ChandelierExitSelection.Long, zeroFloorHigh: true),
        "Skender.GetKeltner" => new WindowAtrBands(Period, Period),
        "Skender.GetStarcBands" => new WindowAtrBands(Period, Period, 2, AtrBandCenterMode.Simple, false),
        _ => throw new InvalidOperationException("Unknown operation.")
    };

    [Benchmark(Baseline = true)]
    public object NativeSingle() => Operation switch
    {
        "Skender.GetAtrStop" => _quotes.GetAtrStop(Period, 3, EndType.Close),
        "Skender.GetSuperTrend" => _quotes.GetSuperTrend(Period, 3),
        "Skender.GetChandelier" => _quotes.GetChandelier(Period, 3, ChandelierType.Long),
        "Skender.GetKeltner" => _quotes.GetKeltner(Period, 2, Period),
        "Skender.GetStarcBands" => _quotes.GetStarcBands(Period, 2, Period),
        _ => throw new InvalidOperationException("Unknown operation.")
    };

    [Benchmark]
    public object NativeParallel() => Operation switch
    {
        "Skender.GetAtrStop" => Parallel(_quotePrefixes, static quotes => quotes.GetAtrStop(Period, 3, EndType.Close)),
        "Skender.GetSuperTrend" => Parallel(_quotePrefixes, static quotes => quotes.GetSuperTrend(Period, 3)),
        "Skender.GetChandelier" => Parallel(_quotePrefixes, static quotes => quotes.GetChandelier(Period, 3, ChandelierType.Long)),
        "Skender.GetKeltner" => Parallel(_quotePrefixes, static quotes => quotes.GetKeltner(Period, 2, Period)),
        "Skender.GetStarcBands" => Parallel(_quotePrefixes, static quotes => quotes.GetStarcBands(Period, 2, Period)),
        _ => throw new InvalidOperationException("Unknown operation.")
    };

    internal static ComparisonSeries Normalize(object result) => result switch
    {
        IReadOnlyList<AtrStopResult> rows => AtrTrailingComparison.Series(
            [rows.Select(v => (double?)v.AtrStop).ToArray(), rows.Select(v => (double?)v.BuyStop).ToArray(), rows.Select(v => (double?)v.SellStop).ToArray()]),
        IReadOnlyList<SuperTrendResult> rows => AtrTrailingComparison.Series(
            [rows.Select(v => (double?)v.SuperTrend).ToArray(), rows.Select(v => (double?)v.UpperBand).ToArray(), rows.Select(v => (double?)v.LowerBand).ToArray()]),
        IReadOnlyList<ChandelierResult> rows => VolumePriceComparison.Mask(rows.Select(v => v.ChandelierExit).ToArray()),
        IReadOnlyList<KeltnerResult> rows => AtrEnvelopeComparison.Series(
            [rows.Select(v => v.Centerline).ToArray(), rows.Select(v => v.UpperBand).ToArray(), rows.Select(v => v.LowerBand).ToArray(), rows.Select(v => v.Width).ToArray()], 0),
        IReadOnlyList<StarcBandsResult> rows => AtrEnvelopeComparison.Series(
            [rows.Select(v => v.Centerline).ToArray(), rows.Select(v => v.UpperBand).ToArray(), rows.Select(v => v.LowerBand).ToArray()], 1),
        _ => throw new ArgumentException("Unexpected native result type.", nameof(result))
    };

}

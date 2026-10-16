using BenchmarkDotNet.Attributes;
using OoplesFinance.StockIndicators.Indicators;
using Skender.Stock.Indicators;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

[MemoryDiagnoser, Config(typeof(TensorsGpuTimingConfig))]
public class SharedSkenderSmoothingBenchmarks : SharedSkenderBenchmarkBase
{
    private const int Period = 20;
    [Params("Skender.GetAlligator", "Skender.GetGator", "Skender.GetElderRay")]
    public string Operation { get; set; } = "";
    private IEnumerable<Quote>[] _quotePrefixes = null!;
    private (DateTime Date, double Value)[] _medians = null!;
    private IEnumerable<(DateTime Date, double Value)>[] _prefixes = null!;

    [GlobalSetup]
    public void Setup()
    {
        if (Operation is not ("Skender.GetAlligator" or "Skender.GetGator" or "Skender.GetElderRay"))
            throw new ArgumentException("Unknown Skender smoothing operation.");
        var (pair, data) = PrepareNativeFixture(Operation);
        // Cache only input views. Each measured call still runs every full prefix
        // and allocates its native results and the joined output.
        _quotePrefixes = _ends.Select(end => _quotes.Take(end)).ToArray();
        // The tuple overload is public and avoids repeated quote conversion.
        // Preserve the decimal-to-double values used by the quote-based oracle.
        _medians = _quotes.Select(quote => (quote.Date, (double)(quote.High + quote.Low) / 2)).ToArray();
        _prefixes = _ends.Select(end => _medians.Take(end)).ToArray();
        var expected = pair.Reference!(data, Period);
        if (Operation == "Skender.GetElderRay")
        {
            foreach (var history in Enum.GetValues<OoplesFinance.StockIndicators.Builder.IndicatorHistoryMode>())
            {
                var owner = new ElderRayWithDetails(Period);
                using var run = SharedBenchmarkSupport.BuildCpu(_bars, owner, history);
                for (int slot = 0; slot < ElderRayComparison.Names.Length; slot++)
                {
                    var column = expected.Outputs[ElderRayComparison.Names[slot]];
                    var present = column.Present!;
                    AsinFeasibilityBenchmarks.RequireSame(column.Values.Select((v, i) => present[i] ? v : 0).ToArray(), run[owner.Outputs[slot]].ToArray());
                    AsinFeasibilityBenchmarks.RequireSame(present.Select(v => v ? 1d : 0d).ToArray(), run[owner.IsDefined].ToArray());
                }
            }
        }
        else SharedBenchmarkSupport.VerifyMappedOutputs(_bars, Create, expected, pair.OutputNames!);
        var nativeExpected = pair.CompetitorReference!(data, Period);
        VerifyNative(nativeExpected, NativeSingle(), NativeParallel(), Normalize, Operation, pair.ErrorBudget);
    }

    protected override IIndicator Create() => Operation switch
    {
        "Skender.GetAlligator" => new AlligatorWithDetails(),
        "Skender.GetGator" => new GatorWithDetails(),
        "Skender.GetElderRay" => new ElderRayWithDetails(Period),
        _ => throw new InvalidOperationException("Unknown operation.")
    };

    // Composing the public Alligator and Gator APIs avoids the extra tuple sort
    // inside GetGator(tuple), while retaining its native arithmetic and outputs.
    [Benchmark(Baseline = true)]
    public object NativeSingle() => Operation switch
    {
        "Skender.GetAlligator" => _medians.GetAlligator(),
        "Skender.GetGator" => _medians.GetAlligator().GetGator(),
        "Skender.GetElderRay" => _quotes.GetElderRay(Period),
        _ => throw new InvalidOperationException("Unknown operation.")
    };

    [Benchmark]
    public object NativeParallel() => Operation switch
    {
        "Skender.GetAlligator" => Parallel(_prefixes, static prices => prices.GetAlligator()),
        "Skender.GetGator" => Parallel(_prefixes, static prices => prices.GetAlligator().GetGator()),
        "Skender.GetElderRay" => Parallel(_quotePrefixes, static quotes => quotes.GetElderRay(Period)),
        _ => throw new InvalidOperationException("Unknown operation.")
    };

    internal static ComparisonSeries Normalize(object result) => result switch
    {
        IReadOnlyList<AlligatorResult> rows => RetrospectivePriceComparison.Series(AlligatorComparison.AlligatorNames,
            [rows.Select(v => v.Jaw).ToArray(), rows.Select(v => v.Teeth).ToArray(), rows.Select(v => v.Lips).ToArray()]),
        IReadOnlyList<GatorResult> rows => RetrospectivePriceComparison.Series(AlligatorComparison.GatorNames,
            [rows.Select(v => v.Upper).ToArray(), rows.Select(v => v.Lower).ToArray(), rows.Select(v => AlligatorComparison.Number(v.UpperIsExpanding)).ToArray(), rows.Select(v => AlligatorComparison.Number(v.LowerIsExpanding)).ToArray()]),
        IReadOnlyList<ElderRayResult> rows => RetrospectivePriceComparison.Series(ElderRayComparison.Names,
            [rows.Select(v => v.Ema).ToArray(), rows.Select(v => v.BullPower).ToArray(), rows.Select(v => v.BearPower).ToArray()]),
        _ => throw new ArgumentException("Unexpected native result type.", nameof(result))
    };

}

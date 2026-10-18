using BenchmarkDotNet.Attributes;
using OoplesFinance.StockIndicators.Indicators;
using Skender.Stock.Indicators;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

[MemoryDiagnoser, Config(typeof(TensorsGpuTimingConfig))]
public class SharedSkenderStrengthBenchmarks : SharedSkenderBenchmarkBase
{
    private const int Period = 20;
    [Params("Skender.GetRsi", "Skender.GetAdx")]
    public string Operation { get; set; } = "";
    private bool _adx;
    private IEnumerable<Quote>[] _quotePrefixes = null!;
    private (DateTime Date, double Value)[] _rsiPrices = null!;
    private IEnumerable<(DateTime Date, double Value)>[] _rsiPrefixes = null!;

    [GlobalSetup]
    public void Setup()
    {
        _adx = Operation switch
        {
            "Skender.GetRsi" => false,
            "Skender.GetAdx" => true,
            _ => throw new ArgumentException("Unknown Skender strength operation.")
        };
        var (pair, data) = PrepareNativeFixture(Operation);
        // Cache only input views. Each measured call still runs every full prefix
        // and allocates its native results and the joined output.
        _quotePrefixes = _ends.Select(end => _quotes.Take(end)).ToArray();
        // The tuple overload is public and avoids repeated quote conversion.
        // Preserve the decimal-to-double values used by the quote-based oracle.
        _rsiPrices = _quotes.Select(quote => (quote.Date, (double)quote.Close)).ToArray();
        _rsiPrefixes = _ends.Select(end => _rsiPrices.Take(end)).ToArray();
        var expected = pair.Reference!(data, Period);
        SharedBenchmarkSupport.VerifyMappedOutputs(_bars, Create, expected, pair.OutputNames ?? ["Value"]);
        var nativeExpected = pair.CompetitorReference!(data, Period);
        VerifyNative(nativeExpected, NativeSingle(), NativeParallel(), Normalize, Operation, pair.ErrorBudget);
    }

    protected override IIndicator Create() => _adx
        ? new SumSeededDirectionalIndex(Period)
        : new WilderStrengthOscillator(Period, WilderStrengthConvention.RsiHundredFlat);

    [Benchmark(Baseline = true)]
    public object NativeSingle() => _adx ? _quotes.GetAdx(Period) : _rsiPrices.GetRsi(Period);

    [Benchmark]
    public object NativeParallel() => _adx
        ? Parallel(_quotePrefixes, static quotes => quotes.GetAdx(Period))
        : Parallel(_rsiPrefixes, static prices => prices.GetRsi(Period));

    internal static ComparisonSeries Normalize(object result) => result switch
    {
        IReadOnlyList<RsiResult> rsi => VolumePriceComparison.Mask(rsi.Select(row => row.Rsi).ToArray()),
        IReadOnlyList<AdxResult> adx => SumDirectionalComparison.Native(adx),
        _ => throw new ArgumentException("Unexpected native result type.", nameof(result))
    };

}

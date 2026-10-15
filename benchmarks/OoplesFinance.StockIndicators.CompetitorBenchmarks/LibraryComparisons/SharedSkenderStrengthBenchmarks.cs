using AiDotNet.Tensors.Helpers;
using BenchmarkDotNet.Attributes;
using OoplesFinance.StockIndicators.Indicators;
using Skender.Stock.Indicators;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

[MemoryDiagnoser, Config(typeof(TensorsGpuTimingConfig))]
public class SharedSkenderStrengthBenchmarks : SharedMappedBenchmarkBase
{
    private const int Period = 20;
    [Params("Skender.GetRsi", "Skender.GetAdx")]
    public string Operation { get; set; } = "";
    private bool _adx;
    private List<Quote> _quotes = null!;
    private IEnumerable<Quote>[] _quotePrefixes = null!;
    private (DateTime Date, double Value)[] _rsiPrices = null!;
    private IEnumerable<(DateTime Date, double Value)>[] _rsiPrefixes = null!;
    private int[] _ends = null!;

    [GlobalSetup]
    public void Setup()
    {
        _adx = Operation switch
        {
            "Skender.GetRsi" => false,
            "Skender.GetAdx" => true,
            _ => throw new ArgumentException("Unknown Skender strength operation.")
        };
        if (Count < 1) throw new ArgumentOutOfRangeException(nameof(Count));
        var (pair, data) = PrepareFixture(Operation);
        _quotes = data.Quotes;
        int chunks = Math.Min(Count, Math.Min(8, Environment.ProcessorCount));
        _ends = Enumerable.Range(1, chunks).Select(i => (int)((long)Count * i / chunks)).ToArray();
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
        var serial = NativeSingle();
        var parallel = NativeParallel();
        ComparisonVerifier.Compare(nativeExpected, Normalize(serial), Operation + " direct native serial", pair.ErrorBudget);
        ComparisonVerifier.Compare(nativeExpected, Normalize(parallel), Operation + " direct native parallel", pair.ErrorBudget);
        RequireDates(serial);
        RequireDates(parallel);
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

    private T[] Parallel<TInput, T>(IEnumerable<TInput>[] prefixes, Func<IEnumerable<TInput>, IEnumerable<T>> calculate)
    {
        var result = new T[Count];
        CpuParallelSettings.LightweightParallel(_ends.Length, _ends.Length, chunk =>
        {
            int start = chunk == 0 ? 0 : _ends[chunk - 1], end = _ends[chunk];
            // Skender 2.7.3 eagerly returns List<T>; do not charge an avoidable
            // intermediate array copy to its serial or parallel native path.
            var prefix = (List<T>)calculate(prefixes[chunk]);
            if (prefix.Count != end) throw new InvalidOperationException("Native prefix length differs.");
            prefix.CopyTo(start, result, start, end - start);
        });
        return result;
    }

    internal static ComparisonSeries Normalize(object result) => result switch
    {
        IReadOnlyList<RsiResult> rsi => VolumePriceComparison.Mask(rsi.Select(row => row.Rsi).ToArray()),
        IReadOnlyList<AdxResult> adx => SumDirectionalComparison.Native(adx),
        _ => throw new ArgumentException("Unexpected native result type.", nameof(result))
    };

    private void RequireDates(object result)
    {
        var dates = result switch
        {
            IReadOnlyList<RsiResult> rsi => rsi.Select(row => row.Date),
            IReadOnlyList<AdxResult> adx => adx.Select(row => row.Date),
            _ => throw new ArgumentException("Unexpected native result type.", nameof(result))
        };
        if (!dates.SequenceEqual(_quotes.Select(quote => quote.Date)))
            throw new InvalidOperationException("Native result dates differ from the fixture.");
    }
}

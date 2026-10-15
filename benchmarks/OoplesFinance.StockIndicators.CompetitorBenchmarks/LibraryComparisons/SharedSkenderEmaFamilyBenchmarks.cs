using AiDotNet.Tensors.Helpers;
using BenchmarkDotNet.Attributes;
using OoplesFinance.StockIndicators.Indicators;
using Skender.Stock.Indicators;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

[MemoryDiagnoser, Config(typeof(TensorsGpuTimingConfig))]
public class SharedSkenderEmaFamilyBenchmarks : SharedMappedBenchmarkBase
{
    private const int Period = 20;
    [Params("Skender.GetMacd", "Skender.GetPvo", "Skender.GetPmo", "Skender.GetTsi", "Skender.GetTrix")]
    public string Operation { get; set; } = "";
    private List<Quote> _quotes = null!;
    private IEnumerable<Quote>[] _quotePrefixes = null!;
    private (DateTime Date, double Value)[] _prices = null!;
    private IEnumerable<(DateTime Date, double Value)>[] _prefixes = null!;
    private int[] _ends = null!;

    [GlobalSetup]
    public void Setup()
    {
        if (Operation is not ("Skender.GetMacd" or "Skender.GetPvo" or "Skender.GetPmo" or "Skender.GetTsi" or "Skender.GetTrix"))
            throw new ArgumentException("Unknown Skender EMA-family operation.");
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
        _prices = _quotes.Select(quote => (quote.Date, (double)quote.Close)).ToArray();
        _prefixes = _ends.Select(end => _prices.Take(end)).ToArray();
        if (Operation is "Skender.GetMacd" or "Skender.GetPvo")
        {
            bool volume = Operation == "Skender.GetPvo";
            var expected = EmaDifferenceSignalComparison.GridReference(volume ? data.Volumes : data.Closes, 12, 26, 9, false, volume);
            // PVO selects three values, but the owned indicator still exposes
            // five value/presence pairs. Verify unselected columns as absent too.
            if (volume) { expected[3] = new double?[Count]; expected[4] = new double?[Count]; }
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
        else SharedBenchmarkSupport.VerifyMappedOutputs(_bars, Create, pair.Reference!(data, Period), pair.OutputNames!);
        var nativeExpected = pair.CompetitorReference!(data, Period);
        var serial = NativeSingle();
        var parallel = NativeParallel();
        ComparisonVerifier.Compare(nativeExpected, Normalize(serial), Operation + " direct native serial", pair.ErrorBudget);
        ComparisonVerifier.Compare(nativeExpected, Normalize(parallel), Operation + " direct native parallel", pair.ErrorBudget);
        RequireDates(serial);
        RequireDates(parallel);
    }

    protected override IIndicator Create() => Operation switch
    {
        "Skender.GetMacd" => new EmaDifferenceSignal(12, 26, 9, false, false, false, EmaDifferenceSignalComparison.Selection(0)),
        "Skender.GetPvo" => new EmaDifferenceSignal(12, 26, 9, false, true, true, EmaDifferenceSignalComparison.Selection(1)),
        "Skender.GetPmo" => new SeededPriceMomentum(20, 20, 10),
        "Skender.GetTsi" => new SeededTrueStrength(20, 13, 7),
        "Skender.GetTrix" => new TripleExponentialRate(20),
        _ => throw new InvalidOperationException("Unknown operation.")
    };

    [Benchmark(Baseline = true)]
    public object NativeSingle() => Operation switch
    {
        "Skender.GetMacd" => _prices.GetMacd(12, 26, 9),
        "Skender.GetPvo" => _quotes.GetPvo(12, 26, 9),
        "Skender.GetPmo" => _prices.GetPmo(20, 20, 10),
        "Skender.GetTsi" => _prices.GetTsi(20, 13, 7),
        "Skender.GetTrix" => _prices.GetTrix(20),
        _ => throw new InvalidOperationException("Unknown operation.")
    };

    [Benchmark]
    public object NativeParallel() => Operation switch
    {
        "Skender.GetMacd" => Parallel(_prefixes, static prices => prices.GetMacd(12, 26, 9)),
        "Skender.GetPvo" => Parallel(_quotePrefixes, static quotes => quotes.GetPvo(12, 26, 9)),
        "Skender.GetPmo" => Parallel(_prefixes, static prices => prices.GetPmo(20, 20, 10)),
        "Skender.GetTsi" => Parallel(_prefixes, static prices => prices.GetTsi(20, 13, 7)),
        "Skender.GetTrix" => Parallel(_prefixes, static prices => prices.GetTrix(20)),
        _ => throw new InvalidOperationException("Unknown operation.")
    };

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
        IReadOnlyList<MacdResult> rows => EmaDifferenceSignalComparison.Series(
            [rows.Select(v => v.Macd).ToArray(), rows.Select(v => v.Signal).ToArray(), rows.Select(v => v.Histogram).ToArray(), rows.Select(v => v.FastEma).ToArray(), rows.Select(v => v.SlowEma).ToArray()], 0),
        IReadOnlyList<PvoResult> rows => EmaDifferenceSignalComparison.Series(
            [rows.Select(v => v.Pvo).ToArray(), rows.Select(v => v.Signal).ToArray(), rows.Select(v => v.Histogram).ToArray()], 1),
        IReadOnlyList<PmoResult> rows => SeededMomentumComparison.Series(
            [rows.Select(v => v.Pmo).ToArray(), rows.Select(v => v.Signal).ToArray()], true),
        IReadOnlyList<TsiResult> rows => SeededMomentumComparison.Series(
            [rows.Select(v => v.Tsi).ToArray(), rows.Select(v => v.Signal).ToArray()], false),
        IReadOnlyList<TrixResult> rows => TripleRateComparison.Series(
            [rows.Select(v => v.Trix).ToArray(), rows.Select(v => v.Ema3).ToArray(), rows.Select(v => v.Signal).ToArray()], false),
        _ => throw new ArgumentException("Unexpected native result type.", nameof(result))
    };

    private void RequireDates(object result)
    {
        var dates = result switch
        {
            IReadOnlyList<MacdResult> rows => rows.Select(row => row.Date),
            IReadOnlyList<PvoResult> rows => rows.Select(row => row.Date),
            IReadOnlyList<PmoResult> rows => rows.Select(row => row.Date),
            IReadOnlyList<TsiResult> rows => rows.Select(row => row.Date),
            IReadOnlyList<TrixResult> rows => rows.Select(row => row.Date),
            _ => throw new ArgumentException("Unexpected native result type.", nameof(result))
        };
        if (!dates.SequenceEqual(_quotes.Select(quote => quote.Date)))
            throw new InvalidOperationException("Native result dates differ from the fixture.");
    }
}

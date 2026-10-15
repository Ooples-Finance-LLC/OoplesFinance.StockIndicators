using System.Numerics;
using static OoplesFinance.StockIndicators.CompetitorBenchmarks.MoneyFlowReferenceArithmetic;
using AiDotNet.Tensors.Helpers;
using BenchmarkDotNet.Attributes;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Validation;
using Skender.Stock.Indicators;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

[MemoryDiagnoser, Config(typeof(TensorsGpuTimingConfig))]
public class SharedSkenderFiniteWindowBenchmarks : SharedMappedBenchmarkBase
{
    private const int Period = 20;
    [Params("Skender.GetAwesome", "Skender.GetBop", "Skender.GetCci")]
    public string Operation { get; set; } = "";
    private List<Quote> _quotes = null!;
    private IEnumerable<Quote>[] _quoteWindows = null!;
    private (DateTime Date, double Value)[] _medians = null!;
    private IEnumerable<(DateTime Date, double Value)>[] _windows = null!;
    private int[] _ends = null!;
    private int[] _seeds = null!;

    [GlobalSetup]
    public void Setup()
    {
        if (Operation is not ("Skender.GetAwesome" or "Skender.GetBop" or "Skender.GetCci"))
            throw new ArgumentException("Unknown Skender finite-window operation.");
        if (Count < 1) throw new ArgumentOutOfRangeException(nameof(Count));
        var (pair, data) = PrepareFixture(Operation);
        _quotes = data.Quotes;
        int chunks = Math.Min(Count, Math.Min(8, Environment.ProcessorCount));
        _ends = Enumerable.Range(1, chunks).Select(i => (int)((long)Count * i / chunks)).ToArray();
        int lookback = Operation == "Skender.GetAwesome" ? 34 : Period;
        _seeds = Enumerable.Range(0, chunks).Select(i => Math.Max(0, (i == 0 ? 0 : _ends[i - 1]) - lookback + 1)).ToArray();
        // Cache input views only. Each worker includes exactly the preceding
        // lookback needed for its first row; native window sums keep their order.
        _quoteWindows = _ends.Select((end, i) => _quotes.Skip(_seeds[i]).Take(end - _seeds[i])).ToArray();
        _medians = _quotes.Select(quote => (quote.Date, (double)(quote.High + quote.Low) / 2)).ToArray();
        _windows = _ends.Select((end, i) => _medians.Skip(_seeds[i]).Take(end - _seeds[i])).ToArray();
        var expected = Operation == "Skender.GetBop" ? BopReference(data, false) : pair.Reference!(data, Period);
        SharedBenchmarkSupport.VerifyMappedOutputs(_bars, Create, expected, pair.OutputNames ?? ["Value"]);
        var nativeExpected = Operation == "Skender.GetBop" ? BopReference(data, true) : pair.CompetitorReference!(data, Period);
        var serial = NativeSingle();
        var parallel = NativeParallel();
        ComparisonVerifier.Compare(nativeExpected, Normalize(serial), Operation + " direct native serial", IndicatorErrorBudget.Exact);
        ComparisonVerifier.Compare(nativeExpected, Normalize(parallel), Operation + " direct native parallel", IndicatorErrorBudget.Exact);
        RequireDates(serial);
        RequireDates(parallel);
    }

    protected override IIndicator Create() => Operation switch
    {
        "Skender.GetAwesome" => new AwesomeWithDetails(),
        "Skender.GetBop" => new BalanceOfPowerWithValidity(Period),
        "Skender.GetCci" => new WindowCommodityChannelIndex(Period, false, false),
        _ => throw new InvalidOperationException("Unknown operation.")
    };

    [Benchmark(Baseline = true)]
    public object NativeSingle() => Operation switch
    {
        "Skender.GetAwesome" => _medians.GetAwesome(5, 34),
        "Skender.GetBop" => _quotes.GetBop(Period),
        "Skender.GetCci" => _quotes.GetCci(Period),
        _ => throw new InvalidOperationException("Unknown operation.")
    };

    [Benchmark]
    public object NativeParallel() => Operation switch
    {
        "Skender.GetAwesome" => Parallel(_windows, static prices => prices.GetAwesome(5, 34)),
        "Skender.GetBop" => Parallel(_quoteWindows, static quotes => quotes.GetBop(Period)),
        "Skender.GetCci" => Parallel(_quoteWindows, static quotes => quotes.GetCci(Period)),
        _ => throw new InvalidOperationException("Unknown operation.")
    };

    private T[] Parallel<TInput, T>(IEnumerable<TInput>[] windows, Func<IEnumerable<TInput>, IEnumerable<T>> calculate)
    {
        var result = new T[Count];
        CpuParallelSettings.LightweightParallel(_ends.Length, _ends.Length, chunk =>
        {
            int start = chunk == 0 ? 0 : _ends[chunk - 1], end = _ends[chunk];
            // Skender 2.7.3 eagerly returns List<T>; do not charge an avoidable
            // intermediate array copy to its serial or parallel native path.
            var window = (List<T>)calculate(windows[chunk]);
            if (window.Count != end - _seeds[chunk]) throw new InvalidOperationException("Native window length differs.");
            window.CopyTo(start - _seeds[chunk], result, start, end - start);
        });
        return result;
    }

    internal static ComparisonSeries Normalize(object result) => result switch
    {
        IReadOnlyList<AwesomeResult> rows => RetrospectivePriceComparison.Series(AwesomeComparison.Names,
            [rows.Select(v => v.Oscillator).ToArray(), rows.Select(v => v.Normalized).ToArray()]),
        IReadOnlyList<BopResult> rows => VolumePriceComparison.Mask(rows.Select(v => v.Bop).ToArray()),
        IReadOnlyList<CciResult> rows => VolumePriceComparison.Mask(rows.Select(v => v.Cci).ToArray()),
        _ => throw new ArgumentException("Unexpected native result type.", nameof(result))
    };

    private static ComparisonSeries BopReference(CompetitorData data, bool native)
    {
        var raw = new double?[data.Count];
        for (int i = 0; i < raw.Length; i++)
        {
            if (native)
            {
                var quote = data.Quotes[i];
                double high = (double)quote.High, low = (double)quote.Low;
                if (high != low) raw[i] = Divide(Subtract((double)quote.Close, (double)quote.Open), Subtract(high, low)); // NOSONAR: S1244 - Exactly equal converted prices define native BOP absence.
            }
            else
            {
                var bar = data.IndicatorBars[i];
                if (bar.High != bar.Low) raw[i] = Round(Units(bar.Close) - Units(bar.Open), Units(bar.High) - Units(bar.Low)); // NOSONAR: S1244 - Exactly flat candles define owned BOP absence.
            }
        }
        var values = new double?[data.Count];
        for (int i = Period - 1; i < values.Length; i++)
        {
            var window = raw.Skip(i - Period + 1).Take(Period).ToArray();
            if (window.Any(v => !v.HasValue)) continue;
            values[i] = native
                ? Divide(window.Aggregate(0d, (sum, v) => Add(sum, v!.Value)), Period)
                : Round(window.Aggregate(BigInteger.Zero, (sum, v) => sum + Units(v!.Value)), Grid * Period);
        }
        return VolumePriceComparison.Mask(values);
    }

    private void RequireDates(object result)
    {
        var dates = result switch
        {
            IReadOnlyList<AwesomeResult> rows => rows.Select(row => row.Date),
            IReadOnlyList<BopResult> rows => rows.Select(row => row.Date),
            IReadOnlyList<CciResult> rows => rows.Select(row => row.Date),
            _ => throw new ArgumentException("Unexpected native result type.", nameof(result))
        };
        if (!dates.SequenceEqual(_quotes.Select(quote => quote.Date)))
            throw new InvalidOperationException("Native result dates differ from the fixture.");
    }
}

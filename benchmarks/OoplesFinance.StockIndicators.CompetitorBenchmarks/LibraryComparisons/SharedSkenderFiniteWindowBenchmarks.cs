using System.Numerics;
using static OoplesFinance.StockIndicators.CompetitorBenchmarks.MoneyFlowReferenceArithmetic;
using BenchmarkDotNet.Attributes;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Validation;
using Skender.Stock.Indicators;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

[MemoryDiagnoser, Config(typeof(TensorsGpuTimingConfig))]
public class SharedSkenderFiniteWindowBenchmarks : SharedSkenderBenchmarkBase
{
    private const int Period = 20;
    [Params("Skender.GetAwesome", "Skender.GetBop", "Skender.GetCci")]
    public string Operation { get; set; } = "";
    private IEnumerable<Quote>[] _quoteWindows = null!;
    private (DateTime Date, double Value)[] _medians = null!;
    private IEnumerable<(DateTime Date, double Value)>[] _windows = null!;

    [GlobalSetup]
    public void Setup()
    {
        if (Operation is not ("Skender.GetAwesome" or "Skender.GetBop" or "Skender.GetCci"))
            throw new ArgumentException("Unknown Skender finite-window operation.");
        var (pair, data) = PrepareNativeFixture(Operation, Operation == "Skender.GetAwesome" ? 33 : Period - 1);
        // Cache input views only. Each worker includes exactly the preceding
        // lookback needed for its first row; native window sums keep their order.
        _quoteWindows = _ends.Select((end, i) => _quotes.Skip(_seeds[i]).Take(end - _seeds[i])).ToArray();
        _medians = _quotes.Select(quote => (quote.Date, (double)(quote.High + quote.Low) / 2)).ToArray();
        _windows = _ends.Select((end, i) => _medians.Skip(_seeds[i]).Take(end - _seeds[i])).ToArray();
        var expected = Operation == "Skender.GetBop" ? BopReference(data, false) : pair.Reference!(data, Period);
        SharedBenchmarkSupport.VerifyMappedOutputs(_bars, Create, expected, pair.OutputNames ?? ["Value"]);
        var nativeExpected = Operation == "Skender.GetBop" ? BopReference(data, true) : pair.CompetitorReference!(data, Period);
        VerifyNative(nativeExpected, NativeSingle(), NativeParallel(), Normalize, Operation, IndicatorErrorBudget.Exact);
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

}

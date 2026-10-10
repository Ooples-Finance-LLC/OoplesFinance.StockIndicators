using BenchmarkDotNet.Attributes;
using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Indicators;
using TALib;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

[MemoryDiagnoser, Config(typeof(TensorsGpuTimingConfig))]
public class PilotRepresentativeBenchmarks
{
    [Params(10_000, 100_000)] public int Count { get; set; }
    [Params("SmaGrid", "SmaDecimal", "ComposedGrid", "ComposedDecimal", "LateReject", "MultiPeriod", "Asin")]
    public string Case { get; set; } = "";
    private Bar[] _bars = null!;
    private double[] _close = null!;

    [GlobalSetup]
    public void Setup()
    {
        _close = Enumerable.Range(0, Count).Select(i => Case switch
        {
            "SmaDecimal" => 100 + (i % 19) / 100d,
            "ComposedDecimal" => .1 + (i % 19) / 100d,
            _ => (i % 127 - 63) / 64d
        }).ToArray();
        if (Case == "LateReject") _close[^1] = double.Epsilon;
        _bars = _close.Select(x => new Bar(default, x, x, x, x, 1)).ToArray();
        var indicators = Indicators();
        foreach (var history in new[] { IndicatorHistoryMode.Full, IndicatorHistoryMode.LatestOnly })
        {
            using var run = Build(indicators, history);
            foreach (var indicator in indicators)
            {
                var expected = new double[Count];
                if (indicator is Sma sma)
                    CpuFeasibilityPrototypes.CurrentSma(_close, expected, sma.Length);
                else
                {
                    if (Case.StartsWith("Composed", StringComparison.Ordinal))
                        CpuFeasibilityPrototypes.CurrentSma(_close, expected, 20);
                    else _close.CopyTo(expected, 0);
                    for (var i = 0; i < expected.Length; i++) expected[i] = Math.Asin(expected[i]);
                }
                AsinFeasibilityBenchmarks.RequireSame(expected, run[indicator].ToArray());
            }
        }
    }

    private IIndicator[] Indicators()
    {
        if (Case == "MultiPeriod") return [new Sma(20), new Sma(50)];
        if (Case.StartsWith("Composed", StringComparison.Ordinal))
        {
            var asin = new PriceCircularTransform(PriceCircularOperation.ArcSine);
            asin.Of(new Sma(20));
            return [asin];
        }
        return Case == "Asin" ? [new PriceCircularTransform(PriceCircularOperation.ArcSine)] : [new Sma(20)];
    }

    private IIndicatorRun Build(IIndicator[] indicators, IndicatorHistoryMode history) => new StockIndicatorBuilder()
        .ConfigureSource(Bars.From(_bars)).ConfigureIndicators(indicators)
        .ConfigureExecution(IndicatorExecutionBackend.Cpu).ConfigureHistory(history).BuildAsync().GetAwaiter().GetResult();

    [Benchmark] public IIndicatorRun CpuBuilder() => Build(Indicators(), IndicatorHistoryMode.Full);
    [Benchmark] public IIndicatorRun CpuLatestOnlyBuilder() => Build(Indicators(), IndicatorHistoryMode.LatestOnly);

    // Direct public competitor calls, including output allocation. These do not
    // provide the builder's history/validation/presence contract. Keep that visible.
    [Benchmark(Baseline = true)] public object Competitor()
    {
        var output = new double[Count];
        if (Case == "Asin") Functions.Asin<double>(_close, System.Range.All, output, out _);
        else
        {
            Functions.Sma<double>(_close, System.Range.All, output, out _, 20);
            if (Case == "MultiPeriod")
            {
                var second = new double[Count];
                Functions.Sma<double>(_close, System.Range.All, second, out _, 50);
                return new[] { output, second };
            }
            if (Case.StartsWith("Composed", StringComparison.Ordinal))
            {
                var result = new double[Count - 19];
                Functions.Asin<double>(output.AsSpan(0, Count - 19), System.Range.All, result, out _);
                return result;
            }
        }
        return output;
    }
}

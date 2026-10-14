using AiDotNet.Tensors.Helpers;
using BenchmarkDotNet.Attributes;
using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Validation;
using Trady.Analysis.Indicator;
using NativeBands = (decimal? LowerBand, decimal? MiddleBand, decimal? UpperBand);

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

[MemoryDiagnoser, Config(typeof(TensorsGpuTimingConfig))]
public class SharedTradyBandsBenchmarks
{
    private const int Period = 20;
    [Params("Trady.Indicator.BollingerBands", "Trady.Indicator.BollingerBandWidth")]
    public string Operation { get; set; } = "";
    [Params(1_000, 10_000)] public int Count { get; set; }
    private Bar[] _bars = null!;
    private decimal[] _prices = null!;
    private decimal[][] _chunks = null!;
    private int[] _skip = null!;
    private int _priorDop;
    private bool WidthOnly => Operation == "Trady.Indicator.BollingerBandWidth";

    [GlobalSetup]
    public void Setup()
    {
        _priorDop = CpuParallelSettings.MaxDegreeOfParallelism;
        CpuParallelSettings.MaxDegreeOfParallelism = 8;
        var values = Enumerable.Range(0, Count).Select(i => 10 + i % 101 / 8d).ToArray();
        _prices = values.Select(v => (decimal)v).ToArray();
        _bars = values.Select((v, i) => new Bar(DateTime.UnixEpoch.AddMinutes(i), v, v + 1, v - 1, v, 1)).ToArray();
        int workers = Math.Min(8, Environment.ProcessorCount);
        _chunks = new decimal[workers][]; _skip = new int[workers];
        for (int worker = 0; worker < workers; worker++)
        {
            int start = Count * worker / workers, end = Count * (worker + 1) / workers;
            int seed = Math.Max(0, start - Period + 1);
            _skip[worker] = start - seed; _chunks[worker] = _prices[seed..end];
        }
        var data = CompetitorData.FromCloses(values);
        var native = DeviationBandsComparison.Pair(Operation).CompetitorReference!(data, Period);
        CheckNative(NativeSingle(), native); CheckNative(NativeParallel(), native);
        // Builder retains all six values and masks even for the width-only mapping.
        var exact = DeviationBandsComparison.GridReference(values, Period, 2, true);
        foreach (var history in Enum.GetValues<IndicatorHistoryMode>())
        {
            var indicator = new WindowDeviationBands(Period, widthAsPercent: true);
            using var run = Build(indicator, history);
            for (int slot = 0; slot < 6; slot++)
            {
                AsinFeasibilityBenchmarks.RequireSame(exact[slot].Select(v => v ?? 0).ToArray(), run[indicator.Outputs[slot]].ToArray());
                AsinFeasibilityBenchmarks.RequireSame(exact[slot].Select(v => v.HasValue ? 1d : 0).ToArray(), run[indicator.Outputs[slot + 6]].ToArray());
            }
        }
    }
    private void CheckNative(object result, ComparisonSeries expected)
    {
        var actual = WidthOnly
            ? DeviationBandsComparison.WidthSeries(((IReadOnlyList<decimal?>)result).ToArray())
            : DeviationBandsComparison.TradySeries(((IReadOnlyList<NativeBands>)result).ToArray());
        ComparisonVerifier.Compare(expected, actual, Operation, IndicatorErrorBudget.Exact);
    }
    [GlobalCleanup] public void Cleanup() => CpuParallelSettings.MaxDegreeOfParallelism = _priorDop;
    [Benchmark] public IIndicatorRun BuilderFull() => Build(new WindowDeviationBands(Period, widthAsPercent: true), IndicatorHistoryMode.Full);
    [Benchmark] public IIndicatorRun BuilderLatestOnly() => Build(new WindowDeviationBands(Period, widthAsPercent: true), IndicatorHistoryMode.LatestOnly);
    // Retain native eager output directly. Decimal conversion/chunk preparation
    // are setup-only; indicator construction, P-1 seeds and output assembly are timed.
    [Benchmark(Baseline = true)] public object NativeSingle() => WidthOnly ? Width(_prices) : Bands(_prices);
    [Benchmark] public object NativeParallel() => WidthOnly ? Parallel(Width) : Parallel(Bands);
    private static IReadOnlyList<decimal?> Width(decimal[] prices) => new BollingerBandWidthByTuple(prices, Period, 2).Compute();
    private static IReadOnlyList<NativeBands> Bands(decimal[] prices) => new BollingerBandsByTuple(prices, Period, 2).Compute();
    private T[] Parallel<T>(Func<decimal[], IReadOnlyList<T>> compute)
    {
        var output = new T[Count];
        CpuParallelSettings.LightweightParallel(_chunks.Length, _chunks.Length, worker =>
        {
            var local = compute(_chunks[worker]);
            int start = Count * worker / _chunks.Length;
            for (int i = _skip[worker]; i < local.Count; i++) output[start + i - _skip[worker]] = local[i];
        });
        return output;
    }
    private IIndicatorRun Build(IIndicator indicator, IndicatorHistoryMode history) => SharedBenchmarkSupport.BuildCpu(_bars, indicator, history);
}

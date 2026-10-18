using AiDotNet.Tensors.Helpers;
using BenchmarkDotNet.Attributes;
using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Indicators;
using TALib;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

[MemoryDiagnoser, Config(typeof(TensorsGpuTimingConfig))]
public class SharedClassicBandsBenchmarks
{
    private const int Period = 20;
    [Params("TaLib.Functions.Bbands")] public string Operation { get; set; } = "";
    [Params(1_000, 10_000)] public int Count { get; set; }
    private Bar[] _bars = null!;
    private double[] _prices = null!;
    private int _priorDop;
    [GlobalSetup]
    public void Setup()
    {
        _priorDop = CpuParallelSettings.MaxDegreeOfParallelism;
        CpuParallelSettings.MaxDegreeOfParallelism = 8;
        _prices = Enumerable.Range(0, Count).Select(i => 10 + i % 101 / 8d).ToArray();
        _bars = _prices.Select(v => new Bar(default, v, v + 1, v - 1, v, 1)).ToArray();
        var data = CompetitorData.FromCloses(_prices); var pair = ClassicBandsComparison.Pair();
        var exact = pair.Reference!(data, Period); var native = pair.CompetitorReference!(data, Period);
        foreach (var actual in new[] { NativeSingle(), NativeParallel() })
            for (int slot = 0; slot < 3; slot++)
            {
                var column = native.Outputs[ClassicBandsComparison.Names[slot]];
                AsinFeasibilityBenchmarks.RequireSame(column.Values.Skip(Period - 1).ToArray(), actual[slot]);
                if (column.Present!.Where((present, i) => present != (i >= Period - 1)).Any())
                    throw new InvalidOperationException("Unexpected native bands presence.");
            }
        foreach (var history in Enum.GetValues<IndicatorHistoryMode>())
        {
            var indicator = new ClassicDeviationBands(Period); using var run = Build(indicator, history);
            for (int slot = 0; slot < 3; slot++)
            {
                var column = exact.Outputs[ClassicBandsComparison.Names[slot]];
                AsinFeasibilityBenchmarks.RequireSame(column.Values.Select((v, i) => column.Present![i] ? v : 0).ToArray(), run[indicator.Outputs[slot]].ToArray());
                AsinFeasibilityBenchmarks.RequireSame(column.Present!.Select(v => v ? 1d : 0).ToArray(), run[indicator.Outputs[slot + 3]].ToArray());
            }
        }
    }
    [GlobalCleanup] public void Cleanup() => CpuParallelSettings.MaxDegreeOfParallelism = _priorDop;
    [Benchmark] public IIndicatorRun BuilderFull() => Build(new ClassicDeviationBands(Period), IndicatorHistoryMode.Full);
    [Benchmark] public IIndicatorRun BuilderLatestOnly() => Build(new ClassicDeviationBands(Period), IndicatorHistoryMode.LatestOnly);
    [Benchmark(Baseline = true)] public double[][] NativeSingle()
    {
        var result = Allocate(); Native(_prices, result[0], result[1], result[2]); return result;
    }
    [Benchmark] public double[][] NativeParallel()
    {
        var result = Allocate(); int workers = Math.Min(8, Environment.ProcessorCount);
        CpuParallelSettings.LightweightParallel(workers, workers, worker =>
        {
            int start = result[0].Length * worker / workers, end = result[0].Length * (worker + 1) / workers;
            Native(_prices.AsSpan(start, end - start + Period - 1), result[0].AsSpan(start, end - start),
                result[1].AsSpan(start, end - start), result[2].AsSpan(start, end - start));
        });
        return result;
    }
    private double[][] Allocate() =>
    [
        GC.AllocateUninitializedArray<double>(Count - Period + 1),
        GC.AllocateUninitializedArray<double>(Count - Period + 1),
        GC.AllocateUninitializedArray<double>(Count - Period + 1)
    ];
    private static void Native(ReadOnlySpan<double> input, Span<double> upper, Span<double> middle, Span<double> lower)
    {
        var status = Functions.Bbands<double>(input, System.Range.All, upper, middle, lower, out var range, Period, 2, 2, TALib.Core.MAType.Sma);
        if (status != TALib.Core.RetCode.Success || range.Start.Value != Period - 1 || range.End.Value - range.Start.Value != upper.Length)
            throw new InvalidOperationException("Native bands failed.");
    }
    private IIndicatorRun Build(IIndicator indicator, IndicatorHistoryMode history) => SharedBenchmarkSupport.BuildCpu(_bars, indicator, history);
}

using AiDotNet.Tensors.Helpers;
using BenchmarkDotNet.Attributes;
using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Indicators;
using Skender.Stock.Indicators;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

[MemoryDiagnoser, Config(typeof(TensorsGpuTimingConfig))]
public class SharedPivotBenchmarks
{
    private const int Period = 20;
    [Params(1_000, 10_000)] public int Count { get; set; }
    private Bar[] _bars = null!;
    private Quote[] _quotes = null!;
    private int _priorDop;
    [GlobalSetup]
    public void Setup()
    {
        _priorDop = CpuParallelSettings.MaxDegreeOfParallelism;
        if (Count < 1) throw new ArgumentOutOfRangeException(nameof(Count));
        CpuParallelSettings.MaxDegreeOfParallelism = 8;
        var close = Enumerable.Range(0, Count).Select(i => 100 + (i * 13 % 101) / 8d).ToArray();
        var data = CompetitorData.FromOhlcv(close.Select(p => p + .5).ToArray(), close.Select(p => p + 2).ToArray(),
            close.Select(p => p - 1).ToArray(), close, Enumerable.Repeat(1d, Count).ToArray());
        _bars = data.IndicatorBars; _quotes = data.Quotes.ToArray();
        var expected = PivotLevelComparison.Reference(data, Period, 0, PivotLevelStyle.Standard, false, PivotCalendarWindow.Day, false);
        var nativeExpected = PivotLevelComparison.Reference(data, Period, 0, PivotLevelStyle.Standard, false, PivotCalendarWindow.Day, true);
        var serial = NativeSingle(); var parallel = NativeParallel();
        for (int i = 0; i < Count; i++)
        {
            if (serial[i].Date != _bars[i].Time || parallel[i].Date != _bars[i].Time)
                throw new InvalidOperationException("Pivot dates changed.");
            var a = Read(serial[i]); var b = Read(parallel[i]);
            for (int slot = 0; slot < 9; slot++)
            {
                Require(nativeExpected[slot][i], a[slot]); Require(a[slot], b[slot]);
                if (expected[slot][i].HasValue != a[slot].HasValue || expected[slot][i] is double value
                    && Math.Abs(value - a[slot]!.Value) > 1e-9)
                    throw new InvalidOperationException($"Native pivot tolerance failed at {slot}/{i}.");
            }
        }
        foreach (var history in Enum.GetValues<IndicatorHistoryMode>())
        {
            var indicator = new RollingPivotLevels(Period);
            using var run = Build(indicator, history);
            for (int slot = 0; slot < 9; slot++)
            {
                AsinFeasibilityBenchmarks.RequireSame(expected[slot].Select(v => v ?? 0).ToArray(), run[indicator.Outputs[slot]].ToArray());
                AsinFeasibilityBenchmarks.RequireSame(expected[slot].Select(v => v.HasValue ? 1d : 0).ToArray(), run[indicator.Outputs[slot + 9]].ToArray());
            }
        }
    }
    private static double?[] Read(RollingPivotsResult r) => [(double?)r.PP, (double?)r.S1, (double?)r.S2,
        (double?)r.S3, (double?)r.S4, (double?)r.R1, (double?)r.R2, (double?)r.R3, (double?)r.R4];
    private static void Require(double? a, double? b)
    {
        if (a.HasValue != b.HasValue || a is double value && BitConverter.DoubleToInt64Bits(value) != BitConverter.DoubleToInt64Bits(b!.Value))
            throw new InvalidOperationException("Pivot reference or native partition differs.");
    }
    [GlobalCleanup] public void Cleanup() => CpuParallelSettings.MaxDegreeOfParallelism = _priorDop;
    [Benchmark] public IIndicatorRun BuilderFull() => Build(new(Period), IndicatorHistoryMode.Full);
    [Benchmark] public IIndicatorRun BuilderLatestOnly() => Build(new(Period), IndicatorHistoryMode.LatestOnly);
    [Benchmark(Baseline = true)] public List<RollingPivotsResult> NativeSingle() =>
        (List<RollingPivotsResult>)_quotes.GetRollingPivots(Period, 0, PivotPointType.Standard);
    [Benchmark] public RollingPivotsResult[] NativeParallel()
    {
        int workers = Math.Min(Count, Math.Min(8, Environment.ProcessorCount));
        var result = new RollingPivotsResult[Count];
        CpuParallelSettings.LightweightParallel(workers, workers, worker =>
        {
            int start = Count * worker / workers, end = Count * (worker + 1) / workers;
            int from = Math.Max(0, start - Period), index = from;
            // Exactly Period preceding bars are needed; native input preparation,
            // allocation and discarded overlap rows remain inside timing.
            foreach (var row in _quotes.Skip(from).Take(end - from).GetRollingPivots(Period, 0, PivotPointType.Standard))
            {
                if (index >= start) result[index] = row;
                index++;
            }
        });
        return result;
    }
    private IIndicatorRun Build(RollingPivotLevels indicator, IndicatorHistoryMode history) => SharedBenchmarkSupport.BuildCpu(_bars, indicator, history);
}

using AiDotNet.Tensors.Helpers;
using BenchmarkDotNet.Attributes;
using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Indicators;
using Skender.Stock.Indicators;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

[MemoryDiagnoser, Config(typeof(TensorsGpuTimingConfig))]
public class SharedHilbertTrendlineBenchmarks
{
    [Params("QuanTAlib.Htit", "Skender.GetHtTrendline")] public string Operation { get; set; } = "";
    [Params(1_000, 10_000)] public int Count { get; set; }
    private Bar[] _bars = null!;
    private QuanTAlib.TValue[] _ticks = null!;
    private (DateTime, double)[] _tuples = null!;
    private bool _quan;
    private int _priorDop;
    private int Columns => _quan ? 1 : 3;

    [GlobalSetup]
    public void Setup()
    {
        _priorDop = CpuParallelSettings.MaxDegreeOfParallelism;
        _quan = Operation switch
        {
            "QuanTAlib.Htit" => true,
            "Skender.GetHtTrendline" => false,
            _ => throw new ArgumentException("Unknown Hilbert trendline operation.")
        };
        if (Count < 1) throw new ArgumentOutOfRangeException(nameof(Count));
        CpuParallelSettings.MaxDegreeOfParallelism = 8;
        var closes = Enumerable.Range(0, Count).Select(i => 100 + (i * 13 % 101) / 8d).ToArray();
        _bars = closes.Select((p, i) => new Bar(DateTime.UnixEpoch.AddMinutes(i), p, p + 2, p - 1, p, 1)).ToArray();
        _ticks = _bars.Select(b => new QuanTAlib.TValue(b.Time, b.Close, true, false)).ToArray();
        // Midpoint differs from close; dyadic fixture prices make this exact.
        _tuples = _bars.Select(b => (b.Time, (b.High + b.Low) / 2)).ToArray();
        var input = _quan ? closes : _tuples.Select(t => t.Item2).ToArray();
        double?[][] expected = _quan ? [OldestHilbertComparison.Reference(input).Select(v => (double?)v).ToArray()]
            : SeededHilbertTrendComparison.Reference(input, false);
        double?[][] nativeExpected = _quan ? [OldestHilbertComparison.NativeReference(input).Select(v => (double?)v).ToArray()]
            : SeededHilbertTrendComparison.Reference(input, true);
        var native = Read(NativeSingle());
        var parallel = Read(NativeParallel());
        for (int slot = 0; slot < Columns; slot++)
        {
            Require(nativeExpected[slot], native[slot]); Require(native[slot], parallel[slot]);
            for (int i = 0; i < Count; i++)
                if (native[slot][i].HasValue != expected[slot][i].HasValue || expected[slot][i] is double value
                    && (!double.IsFinite(native[slot][i]!.Value) || Math.Abs(value - native[slot][i]!.Value) > 1e-9))
                    throw new InvalidOperationException($"Native and owned trendline differ at {slot}/{i}.");
        }
        if (!_quan)
        {
            var quotes = _bars.Select(b => new Quote { Date = b.Time, Open = (decimal)b.Open,
                High = (decimal)b.High, Low = (decimal)b.Low, Close = (decimal)b.Close, Volume = 1 }).ToArray();
            var rows = quotes.GetHtTrendline().ToArray();
            Require(native[0], rows.Select(r => r.Trendline).ToArray());
            Require(native[1], rows.Select(r => r.SmoothPrice).ToArray());
            Require(native[2], rows.Select(r => (double?)r.DcPeriods).ToArray());
        }
        foreach (var history in Enum.GetValues<IndicatorHistoryMode>())
        {
            var indicator = Create();
            using var run = Build(indicator, history);
            for (int slot = 0; slot < Columns; slot++)
            {
                AsinFeasibilityBenchmarks.RequireSame(expected[slot].Select(v => v ?? 0).ToArray(), run[indicator.Outputs[slot]].ToArray());
                if (!_quan) AsinFeasibilityBenchmarks.RequireSame(expected[slot].Select(v => v.HasValue ? 1d : 0).ToArray(), run[indicator.Outputs[slot + 3]].ToArray());
            }
        }
    }
    private double?[][] Read(object result) => result switch
    {
        double[] values => [values.Select(v => (double?)v).ToArray()],
        List<HtlResult> rows => [rows.Select(v => v.Trendline).ToArray(), rows.Select(v => v.SmoothPrice).ToArray(), rows.Select(v => (double?)v.DcPeriods).ToArray()],
        HtlResult[] rows => [rows.Select(v => v.Trendline).ToArray(), rows.Select(v => v.SmoothPrice).ToArray(), rows.Select(v => (double?)v.DcPeriods).ToArray()],
        _ => throw new InvalidOperationException("Unexpected native trendline result type.")
    };
    private static void Require(double?[] expected, double?[] actual)
    {
        if (expected.Length != actual.Length) throw new InvalidOperationException("Trendline output length differs.");
        for (int i = 0; i < expected.Length; i++)
            if (expected[i].HasValue != actual[i].HasValue || expected[i] is double value
                && BitConverter.DoubleToInt64Bits(value) != BitConverter.DoubleToInt64Bits(actual[i]!.Value))
                throw new InvalidOperationException($"Trendline value/presence differs at {i}.");
    }
    private IIndicator Create() => _quan ? new OldestFirstHilbertTrendline() : new SeededHilbertTrendline();
    [GlobalCleanup] public void Cleanup() => CpuParallelSettings.MaxDegreeOfParallelism = _priorDop;
    [Benchmark] public IIndicatorRun BuilderFull() => Build(Create(), IndicatorHistoryMode.Full);
    [Benchmark] public IIndicatorRun BuilderLatestOnly() => Build(Create(), IndicatorHistoryMode.LatestOnly);
    [Benchmark(Baseline = true)] public object NativeSingle()
    {
        if (!_quan) return (List<HtlResult>)_tuples.GetHtTrendline();
        var result = new double[Count]; Calculate(0, Count, result); return result;
    }
    [Benchmark] public object NativeParallel()
    {
        int workers = Math.Min(Count, Math.Min(8, Environment.ProcessorCount));
        if (!_quan)
        {
            var result = new HtlResult[Count];
            CpuParallelSettings.LightweightParallel(workers, workers, worker =>
            {
                int start = Count * worker / workers, end = Count * (worker + 1) / workers, index = 0;
                foreach (var value in _tuples.Take(end).GetHtTrendline())
                {
                    if (index >= start) result[index] = value;
                    index++;
                }
            });
            return result;
        }
        else
        {
            var result = new double[Count];
            CpuParallelSettings.LightweightParallel(workers, workers, worker =>
                Calculate(Count * worker / workers, Count * (worker + 1) / workers, result));
            return result;
        }
    }
    private void Calculate(int start, int end, double[] result)
    {
        // Replay complete prefixes to preserve native phase, period and mean
        // history. State allocation and discarded prefix work remain timed.
        var state = new QuanTAlib.Htit();
        for (int i = 0; i < start; i++) state.Calc(_ticks[i]);
        for (int i = start; i < end; i++)
        {
            result[i] = state.Calc(_ticks[i]).Value;
        }
    }
    private IIndicatorRun Build(IIndicator indicator, IndicatorHistoryMode history) => new StockIndicatorBuilder()
        .ConfigureSource(Bars.From(_bars)).ConfigureIndicators(indicator).ConfigureHistory(history)
        .ConfigureExecution(IndicatorExecutionBackend.Cpu).BuildAsync().GetAwaiter().GetResult();
}

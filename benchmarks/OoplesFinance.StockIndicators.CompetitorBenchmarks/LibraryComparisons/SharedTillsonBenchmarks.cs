using AiDotNet.Tensors.Helpers;
using BenchmarkDotNet.Attributes;
using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Indicators;
using Skender.Stock.Indicators;
using TALib;
using TaCore = TALib.Core;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

[MemoryDiagnoser, Config(typeof(TensorsGpuTimingConfig))]
public class SharedTillsonBenchmarks
{
    private const int Period = 20, Lookback = 6 * (Period - 1);
    private const double Factor = .7;
    [Params("Skender.GetT3", "TaLib.Functions.T3", "QuanTAlib.T3")] public string Operation { get; set; } = "";
    [Params(1_000, 10_000)] public int Count { get; set; }
    private Bar[] _bars = null!;
    private double[] _prices = null!;
    private QuanTAlib.TValue[] _ticks = null!;
    private (DateTime, double)[] _tuples = null!;
    private int _variant, _priorDop;

    [GlobalSetup]
    public void Setup()
    {
        _priorDop = CpuParallelSettings.MaxDegreeOfParallelism;
        _variant = Array.IndexOf(TillsonComparison.Ids, Operation);
        if (_variant < 0) throw new ArgumentException("Unknown Tillson operation.");
        if (Count < 1 || _variant == 1 && Count <= Lookback) throw new ArgumentOutOfRangeException(nameof(Count));
        if (_variant == 1 && (TaCore.CompatibilitySettings.Get() != TaCore.CompatibilityMode.Default
            || TaCore.UnstablePeriodSettings.Get(TaCore.UnstableFunc.T3) != 0))
            throw new InvalidOperationException("Tillson benchmarks require default native settings.");
        CpuParallelSettings.MaxDegreeOfParallelism = 8;
        _prices = Enumerable.Range(0, Count).Select(i => 100 + (i * 13 % 101) / 8d).ToArray();
        _tuples = _prices.Select((p, i) => (DateTime.UnixEpoch.AddMinutes(i), p)).ToArray();
        _ticks = _tuples.Select(p => new QuanTAlib.TValue(p.Item1, p.Item2, true, false)).ToArray();
        _bars = _tuples.Select(p => new Bar(p.Item1, p.Item2, p.Item2 + 1, p.Item2 - 1, p.Item2, 1)).ToArray();
        var expected = TillsonComparison.Reference(_prices, Period, Factor, TillsonComparison.Seed(_variant, true));
        var nativeExpected = TillsonComparison.NativeReference(_prices, Period, Factor, _variant);
        var native = Read(NativeSingle());
        Require(nativeExpected, native);
        Require(native, Read(NativeParallel()));
        if (_variant == 0)
        {
            var quotes = _bars.Select(b => new Quote { Date = b.Time, Open = (decimal)b.Open,
                High = (decimal)b.High, Low = (decimal)b.Low, Close = (decimal)b.Close, Volume = 1 }).ToArray();
            Require(native, quotes.GetT3(Period, Factor).Select(r => r.T3).ToArray());
        }
        for (int i = 0; i < Count; i++)
            if (native[i].HasValue != expected[i].HasValue || expected[i] is double value
                && (!double.IsFinite(native[i]!.Value) || Math.Abs(value - native[i]!.Value) > 1e-9))
                throw new InvalidOperationException("Native and owned Tillson outputs disagree on benchmark inputs.");
        foreach (var history in Enum.GetValues<IndicatorHistoryMode>())
        {
            var indicator = Create();
            using var run = Build(indicator, history);
            AsinFeasibilityBenchmarks.RequireSame(expected.Select(v => v ?? 0).ToArray(), run[indicator.Value].ToArray());
            AsinFeasibilityBenchmarks.RequireSame(expected.Select(v => v.HasValue ? 1d : 0).ToArray(), run[indicator.IsDefined].ToArray());
        }
    }

    private double?[] Read(object result)
    {
        if (result is double[] values)
        {
            var output = new double?[Count];
            int first = _variant == 1 ? Lookback : 0;
            if (values.Length != Count - first) throw new InvalidOperationException("Unexpected native Tillson array length.");
            for (int i = 0; i < values.Length; i++) output[first + i] = values[i];
            return output;
        }
        return result switch
        {
            List<T3Result> rows => rows.Select(v => v.T3).ToArray(),
            T3Result[] rows => rows.Select(v => v.T3).ToArray(),
            _ => throw new InvalidOperationException("Unexpected native Tillson result type.")
        };
    }
    private static void Require(double?[] expected, double?[] actual)
    {
        if (expected.Length != actual.Length) throw new InvalidOperationException("Tillson output length differs.");
        for (int i = 0; i < expected.Length; i++)
            if (expected[i].HasValue != actual[i].HasValue || expected[i] is double value
                && BitConverter.DoubleToInt64Bits(value) != BitConverter.DoubleToInt64Bits(actual[i]!.Value))
                throw new InvalidOperationException($"Tillson value/presence differs at {i}.");
    }
    private TillsonAverage Create() => new(Period, Factor, TillsonComparison.Seed(_variant, true));
    [GlobalCleanup] public void Cleanup() => CpuParallelSettings.MaxDegreeOfParallelism = _priorDop;
    [Benchmark] public IIndicatorRun BuilderFull() => Build(Create(), IndicatorHistoryMode.Full);
    [Benchmark] public IIndicatorRun BuilderLatestOnly() => Build(Create(), IndicatorHistoryMode.LatestOnly);
    [Benchmark(Baseline = true)] public object NativeSingle()
    {
        if (_variant == 0) return (List<T3Result>)_tuples.GetT3(Period, Factor);
        var result = new double[_variant == 1 ? Count - Lookback : Count];
        if (_variant == 1) NativeTa(_prices, result);
        else Calculate(0, Count, result);
        return result;
    }
    [Benchmark] public object NativeParallel()
    {
        int length = _variant == 1 ? Count - Lookback : Count;
        int workers = Math.Min(length, Math.Min(8, Environment.ProcessorCount));
        if (_variant == 0)
        {
            var result = new T3Result[Count];
            CpuParallelSettings.LightweightParallel(workers, workers, worker =>
            {
                int start = Count * worker / workers, end = Count * (worker + 1) / workers;
                int index = 0;
                foreach (var value in _tuples.Take(end).GetT3(Period, Factor))
                {
                    if (index >= start) result[index] = value;
                    index++;
                }
            });
            return result;
        }
        else
        {
            var result = new double[length];
            CpuParallelSettings.LightweightParallel(workers, workers, worker =>
            {
                int start = length * worker / workers, end = length * (worker + 1) / workers;
                if (_variant == 1)
                {
                    var prefix = new double[end];
                    NativeTa(_prices.AsSpan(0, end + Lookback), prefix);
                    prefix.AsSpan(start, end - start).CopyTo(result.AsSpan(start, end - start));
                }
                else Calculate(start, end, result);
            });
            return result;
        }
    }
    private static void NativeTa(ReadOnlySpan<double> prices, Span<double> output)
    {
        var status = Functions.T3<double>(prices, System.Range.All, output, out var range, Period, Factor);
        if (status != TaCore.RetCode.Success || range.Start.Value != Lookback || range.End.Value != prices.Length)
            throw new InvalidOperationException("Unexpected native Tillson range.");
    }
    private void Calculate(int start, int end, double[] result)
    {
        // All three libraries replay complete prefixes; never reseed recursive
        // stages at worker boundaries. Native state/scratch/discards stay timed.
        var state = new QuanTAlib.T3(Period, Factor, true);
        for (int i = 0; i < start; i++) state.Calc(_ticks[i]);
        for (int i = start; i < end; i++) result[i] = state.Calc(_ticks[i]).Value;
    }
    private IIndicatorRun Build(TillsonAverage indicator, IndicatorHistoryMode history) => new StockIndicatorBuilder()
        .ConfigureSource(Bars.From(_bars)).ConfigureIndicators(indicator).ConfigureHistory(history)
        .ConfigureExecution(IndicatorExecutionBackend.Cpu).BuildAsync().GetAwaiter().GetResult();
}

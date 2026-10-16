using System.Numerics;
using AiDotNet.Tensors.Helpers;
using BenchmarkDotNet.Attributes;
using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Indicators;
using Skender.Stock.Indicators;
using TALib;
using Trady.Analysis.Indicator;
using static OoplesFinance.StockIndicators.CompetitorBenchmarks.MoneyFlowReferenceArithmetic;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

[MemoryDiagnoser, Config(typeof(TensorsGpuTimingConfig))]
public class SharedSmaBenchmarks
{
    private const int Period = 20;
    [Params("QuanTAlib.Sma", "Skender.GetSma", "TaLib.Functions.Sma", "Trady.Indicator.SimpleMovingAverage")]
    public string Operation { get; set; } = "";
    [Params(1_000, 10_000)] public int Count { get; set; }
    private SkenderWindowBenchmarkInput _input = null!;
    private QuanTAlib.TValue[] _quan = null!;
    private decimal?[] _decimals = null!;
    private decimal?[][] _decimalChunks = null!;
    private int _priorDop;

    [GlobalSetup]
    public void Setup()
    {
        _priorDop = CpuParallelSettings.MaxDegreeOfParallelism;
        CpuParallelSettings.MaxDegreeOfParallelism = 8;
        _input = SkenderWindowBenchmarkInput.Create(Count, Period - 1);
        _quan = _input.Prices.Select(v => new QuanTAlib.TValue(v, true, false)).ToArray();
        _decimals = _input.Prices.Select(v => (decimal?)v).ToArray();
        _decimalChunks = _input.Chunks.Select(c => c.Select(v => (decimal?)v.Item2).ToArray()).ToArray();
        var expected = new double[Count];
        var available = new double[Count];
        var units = _input.Prices.Select(Units).ToArray();
        BigInteger sum = 0;
        for (int i = 0; i < Count; i++)
        {
            sum += units[i];
            if (i >= Period) sum -= units[i - Period];
            available[i] = Round(sum, Grid * Math.Min(i + 1, Period));
            if (i >= Period - 1) expected[i] = available[i];
        }
        if (Operation == "TaLib.Functions.Sma")
        {
            var output = new double[Count - Period + 1];
            var code = Functions.Sma<double>(_input.Prices, System.Range.All, output, out var range, Period);
            if (code != TALib.Core.RetCode.Success || range.GetOffsetAndLength(Count) != (Period - 1, output.Length))
                throw new InvalidOperationException("Unexpected native SMA status or packed range.");
        }
        CheckNative(NativeSingle(), available); CheckNative(NativeParallel(), available);
        foreach (var history in Enum.GetValues<IndicatorHistoryMode>())
        {
            var indicator = new Sma(Period); using var run = Build(indicator, history);
            AsinFeasibilityBenchmarks.RequireSame(expected, run[indicator].ToArray());
        }
    }

    private void CheckNative(object result, double[] expected)
    {
        if (result is double[] values)
        {
            var wanted = Operation == "TaLib.Functions.Sma" ? expected[(Period - 1)..] : expected;
            AsinFeasibilityBenchmarks.RequireSame(wanted, values);
            return;
        }
        if (result is IReadOnlyList<SmaResult> skender)
        {
            if (skender.Count != Count || skender.Take(Period - 1).Any(r => r.Sma.HasValue)
                || skender.Skip(Period - 1).Any(r => !r.Sma.HasValue))
                throw new InvalidOperationException("Unexpected Skender SMA startup presence.");
            AsinFeasibilityBenchmarks.RequireSame(expected[(Period - 1)..], skender.Skip(Period - 1).Select(r => r.Sma!.Value).ToArray());
            return;
        }
        var trady = (IReadOnlyList<decimal?>)result;
        if (trady.Count != Count || trady.Take(Period - 1).Any(v => v.HasValue)
            || trady.Skip(Period - 1).Any(v => !v.HasValue))
            throw new InvalidOperationException("Unexpected Trady SMA startup presence.");
        AsinFeasibilityBenchmarks.RequireSame(expected[(Period - 1)..], trady.Skip(Period - 1).Select(v => (double)v!.Value).ToArray());
    }

    [GlobalCleanup] public void Cleanup() => CpuParallelSettings.MaxDegreeOfParallelism = _priorDop;
    [Benchmark] public IIndicatorRun BuilderFull() => Build(new Sma(Period), IndicatorHistoryMode.Full);
    [Benchmark] public IIndicatorRun BuilderLatestOnly() => Build(new Sma(Period), IndicatorHistoryMode.LatestOnly);
    private IIndicatorRun Build(IIndicator indicator, IndicatorHistoryMode history) => new StockIndicatorBuilder()
        .ConfigureSource(Bars.From(_input.Bars)).ConfigureIndicators(indicator).ConfigureHistory(history)
        .ConfigureExecution(IndicatorExecutionBackend.Cpu).BuildAsync().GetAwaiter().GetResult();

    // Native tuples and packed arrays are retained directly. Input conversion is
    // outside timing for every library; native sorting and output allocation stay timed.
    [Benchmark(Baseline = true)] public object NativeSingle()
    {
        switch (Operation)
        {
            case "Skender.GetSma": return (IReadOnlyList<SmaResult>)_input.Tuples.GetSma(Period);
            case "Trady.Indicator.SimpleMovingAverage": return new SimpleMovingAverageByTuple(_decimals, Period).Compute();
            case "TaLib.Functions.Sma":
                var packed = new double[Count - Period + 1];
                Functions.Sma<double>(_input.Prices, System.Range.All, packed, out _, Period);
                return packed;
            default:
                var output = new double[Count]; Quan(0, Count, output); return output;
        }
    }

    [Benchmark] public object NativeParallel()
    {
        int chunks = _input.Chunks.Length;
        if (Operation == "TaLib.Functions.Sma")
        {
            int defined = Count - Period + 1;
            chunks = Math.Min(chunks, defined);
            var packed = new double[defined];
            CpuParallelSettings.LightweightParallel(chunks, chunks, worker =>
            {
                int start = defined * worker / chunks, end = defined * (worker + 1) / chunks;
                Functions.Sma<double>(_input.Prices.AsSpan(start, end - start + Period - 1), System.Range.All,
                    packed.AsSpan(start, end - start), out _, Period);
            });
            return packed;
        }
        if (Operation == "Skender.GetSma")
        {
            var output = new SmaResult[Count];
            CpuParallelSettings.LightweightParallel(chunks, chunks, worker =>
            {
                var rows = (IReadOnlyList<SmaResult>)_input.Chunks[worker].GetSma(Period);
                int start = Count * worker / chunks, skip = _input.Skip[worker];
                for (int i = skip; i < rows.Count; i++) output[start + i - skip] = rows[i];
            });
            return output;
        }
        if (Operation == "Trady.Indicator.SimpleMovingAverage")
        {
            var output = new decimal?[Count];
            CpuParallelSettings.LightweightParallel(chunks, chunks, worker =>
            {
                var rows = new SimpleMovingAverageByTuple(_decimalChunks[worker], Period).Compute(startIndex: _input.Skip[worker]);
                int start = Count * worker / chunks;
                for (int i = 0; i < rows.Count; i++) output[start + i] = rows[i];
            });
            return output;
        }
        var values = new double[Count];
        CpuParallelSettings.LightweightParallel(chunks, chunks, worker => Quan(Count * worker / chunks, Count * (worker + 1) / chunks, values));
        return values;
    }

    private void Quan(int start, int end, double[] output)
    {
        var state = new QuanTAlib.Sma(Period);
        for (int i = Math.Max(0, start - Period + 1); i < start; i++) state.Calc(_quan[i]);
        for (int i = start; i < end; i++) output[i] = state.Calc(_quan[i]).Value;
    }
}

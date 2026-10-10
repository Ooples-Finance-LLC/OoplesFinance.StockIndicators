using AiDotNet.Tensors.Helpers;
using BenchmarkDotNet.Attributes;
using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Indicators;
using TALib;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

// Identical prepared closes/opens. No normalization adapters are timed. Native
// statistics return packed mature outputs; builder also owns startup and presence.
[MemoryDiagnoser, Config(typeof(TensorsGpuTimingConfig))]
public class SharedPairInputBenchmarks
{
    private const int Period = 20;
    [Params("Add", "Sub", "Mult", "Div", "Correl", "Beta")] public string Operation { get; set; } = "";
    [Params(1_000, 10_000)] public int Count { get; set; }
    private Bar[] _bars = null!;
    private double[] _closes = null!, _opens = null!;
    private int _previousDop;
    private int Lookback => Operation switch { "Correl" => Period - 1, "Beta" => Period, _ => 0 };

    [GlobalSetup]
    public void Setup()
    {
        _previousDop = CpuParallelSettings.MaxDegreeOfParallelism;
        CpuParallelSettings.MaxDegreeOfParallelism = 8;
        _closes = Enumerable.Range(0, Count).Select(i => 10 + (i * 13 % 101) / 8d).ToArray();
        _opens = Enumerable.Range(0, Count).Select(i => 8 + (i * 7 % 97) / 16d).ToArray();
        _bars = _closes.Select((v, i) => new Bar(default, _opens[i], Math.Max(v, _opens[i]) + 1,
            Math.Min(v, _opens[i]) - 1, v, 1)).ToArray();
        var expected = NativeSingle();
        Check(expected, NativeParallel());
        foreach (var mode in new[] { IndicatorHistoryMode.Full, IndicatorHistoryMode.LatestOnly })
        {
            var indicator = Create();
            using var result = Build(indicator, mode);
            Check(expected, result[indicator].Slice(Lookback).ToArray());
            foreach (var flag in result[indicator.Outputs[1]].Slice(Lookback))
                if (BitConverter.DoubleToInt64Bits(flag) != BitConverter.DoubleToInt64Bits(1d))
                    throw new InvalidOperationException("Missing mature pair-input result.");
        }
    }

    [GlobalCleanup] public void Cleanup() => CpuParallelSettings.MaxDegreeOfParallelism = _previousDop;
    [Benchmark] public IIndicatorRun BuilderFull() => Build(Create(), IndicatorHistoryMode.Full);
    [Benchmark] public IIndicatorRun BuilderLatestOnly() => Build(Create(), IndicatorHistoryMode.LatestOnly);
    [Benchmark(Baseline = true)] public double[] NativeSingle()
    {
        var output = GC.AllocateUninitializedArray<double>(Count - Lookback);
        Native(_closes, _opens, output);
        return output;
    }

    [Benchmark] public double[] NativeParallel()
    {
        var lookback = Lookback;
        var output = GC.AllocateUninitializedArray<double>(Count - lookback);
        int chunks = Math.Min(8, Environment.ProcessorCount);
        CpuParallelSettings.LightweightParallel(chunks, chunks, chunk =>
        {
            int start = output.Length * chunk / chunks;
            int end = output.Length * (chunk + 1) / chunks;
            Native(_closes.AsSpan(start, end - start + lookback), _opens.AsSpan(start, end - start + lookback),
                output.AsSpan(start, end - start));
        });
        return output;
    }

    private void Native(ReadOnlySpan<double> left, ReadOnlySpan<double> right, Span<double> output)
    {
        System.Range range;
        var status = Operation switch
        {
            "Add" => Functions.Add<double>(left, right, System.Range.All, output, out range),
            "Sub" => Functions.Sub<double>(left, right, System.Range.All, output, out range),
            "Mult" => Functions.Mult<double>(left, right, System.Range.All, output, out range),
            "Div" => Functions.Div<double>(left, right, System.Range.All, output, out range),
            "Correl" => Functions.Correl<double>(left, right, System.Range.All, output, out range, Period),
            "Beta" => Functions.Beta<double>(left, right, System.Range.All, output, out range, Period),
            _ => throw new InvalidOperationException("Unknown pair-input operation.")
        };
        if (status != TALib.Core.RetCode.Success || range.Start.Value != Lookback
            || range.End.Value - range.Start.Value != output.Length)
            throw new InvalidOperationException("Unexpected native pair-input output range.");
    }

    private IIndicator Create() => Operation switch
    {
        "Add" => new CandleArithmetic(CandleArithmeticOperation.Add),
        "Sub" => new CandleArithmetic(CandleArithmeticOperation.Subtract),
        "Mult" => new CandleArithmetic(CandleArithmeticOperation.Multiply),
        "Div" => new CandleArithmetic(CandleArithmeticOperation.Divide),
        "Correl" => new WindowCorrelation(Period, flatZero: true),
        "Beta" => new WindowReturnBeta(Period),
        _ => throw new InvalidOperationException("Unknown pair-input operation.")
    };

    private IIndicatorRun Build(IIndicator indicator, IndicatorHistoryMode history) => new StockIndicatorBuilder()
        .ConfigureSource(Bars.From(_bars)).ConfigureIndicators(indicator).ConfigureHistory(history)
        .ConfigureExecution(IndicatorExecutionBackend.Cpu).BuildAsync().GetAwaiter().GetResult();

    private void Check(double[] expected, double[] actual)
    {
        if (Lookback == 0) { AsinFeasibilityBenchmarks.RequireSame(expected, actual); return; }
        if (expected.Length != actual.Length) throw new InvalidOperationException("Pair-input output length mismatch.");
        for (int i = 0; i < expected.Length; i++)
            if (!double.IsFinite(expected[i]) || !double.IsFinite(actual[i])
                || Math.Abs(expected[i] - actual[i]) > 1e-10 * Math.Max(1, Math.Abs(expected[i])))
                throw new InvalidOperationException($"Pair-input mismatch at {i}.");
    }
}

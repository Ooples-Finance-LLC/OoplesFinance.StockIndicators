using AiDotNet.Tensors.Helpers;
using BenchmarkDotNet.Attributes;
using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Indicators;
using TALib;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

// Prepared identical field values. GPU timings include staging, transfers,
// dispatch, readback and output validation; compilation/workspace are warmed.
// NativeParallel is our bounded scheduler wrapper around the native primitive.
[MemoryDiagnoser, Config(typeof(TensorsGpuTimingConfig))]
public class SharedGpuPointwiseBenchmarks
{
    [Params("Add", "Sub", "Mult", "Div", "Ceil", "Floor", "Sqrt")]
    public string Operation { get; set; } = "";
    [Params(10_000, 100_000)] public int Count { get; set; }
    private Bar[] _bars = null!;
    private double[] _left = null!, _right = null!;
    private int _previousDop;

    [GlobalSetup]
    public void Setup()
    {
        _previousDop = CpuParallelSettings.MaxDegreeOfParallelism;
        CpuParallelSettings.MaxDegreeOfParallelism = 8;
        _left = Enumerable.Range(0, Count).Select(i => 10 + (i * 13 % 101) / 8d).ToArray();
        _right = Enumerable.Range(0, Count).Select(i => 8 + (i * 7 % 97) / 16d).ToArray();
        _bars = _left.Select((v, i) => new Bar(default, _right[i], Math.Max(v, _right[i]) + 1,
            Math.Min(v, _right[i]) - 1, v, 1)).ToArray();
        var expected = NativeSingle();
        AsinFeasibilityBenchmarks.RequireSame(expected, NativeParallel());
        foreach (var backend in new[] { IndicatorExecutionBackend.Cpu, IndicatorExecutionBackend.Gpu })
        foreach (var history in Enum.GetValues<IndicatorHistoryMode>())
        {
            var indicator = Create();
            using var result = Build(indicator, backend, history);
            AsinFeasibilityBenchmarks.RequireSame(expected, result[indicator].ToArray());
            foreach (var flag in result[indicator.Outputs[1]])
                if (BitConverter.DoubleToInt64Bits(flag) != BitConverter.DoubleToInt64Bits(1d))
                    throw new InvalidOperationException("Missing pointwise output.");
        }
    }

    [GlobalCleanup] public void Cleanup() => CpuParallelSettings.MaxDegreeOfParallelism = _previousDop;
    [Benchmark] public IIndicatorRun CpuFull() => Build(Create(), IndicatorExecutionBackend.Cpu, IndicatorHistoryMode.Full);
    [Benchmark] public IIndicatorRun CpuLatestOnly() => Build(Create(), IndicatorExecutionBackend.Cpu, IndicatorHistoryMode.LatestOnly);
    [Benchmark] public IIndicatorRun GpuFull() => Build(Create(), IndicatorExecutionBackend.Gpu, IndicatorHistoryMode.Full);
    [Benchmark] public IIndicatorRun GpuLatestOnly() => Build(Create(), IndicatorExecutionBackend.Gpu, IndicatorHistoryMode.LatestOnly);
    [Benchmark(Baseline = true)] public double[] NativeSingle()
    {
        var output = GC.AllocateUninitializedArray<double>(Count);
        Native(_left, _right, output);
        return output;
    }
    [Benchmark] public double[] NativeParallel()
    {
        var output = GC.AllocateUninitializedArray<double>(Count);
        int chunks = Math.Min(8, Environment.ProcessorCount);
        CpuParallelSettings.LightweightParallel(chunks, chunks, chunk =>
        {
            int start = Count * chunk / chunks, end = Count * (chunk + 1) / chunks;
            Native(_left.AsSpan(start, end - start), _right.AsSpan(start, end - start), output.AsSpan(start, end - start));
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
            "Ceil" => Functions.Ceil<double>(left, System.Range.All, output, out range),
            "Floor" => Functions.Floor<double>(left, System.Range.All, output, out range),
            "Sqrt" => Functions.Sqrt<double>(left, System.Range.All, output, out range),
            _ => throw new InvalidOperationException("Unknown pointwise operation.")
        };
        if (status != TALib.Core.RetCode.Success || range.Start.Value != 0 || range.End.Value != output.Length)
            throw new InvalidOperationException("Unexpected native pointwise range.");
    }

    private IIndicator Create() => Operation switch
    {
        "Add" => new CandleArithmetic(CandleArithmeticOperation.Add),
        "Sub" => new CandleArithmetic(CandleArithmeticOperation.Subtract),
        "Mult" => new CandleArithmetic(CandleArithmeticOperation.Multiply),
        "Div" => new CandleArithmetic(CandleArithmeticOperation.Divide),
        "Ceil" => new PriceRoundingTransform(PriceRoundingOperation.Ceiling),
        "Floor" => new PriceRoundingTransform(PriceRoundingOperation.Floor),
        "Sqrt" => new PriceRoundingTransform(PriceRoundingOperation.SquareRoot),
        _ => throw new InvalidOperationException("Unknown pointwise operation.")
    };

    private IIndicatorRun Build(IIndicator indicator, IndicatorExecutionBackend backend, IndicatorHistoryMode history)
    {
        var builder = new StockIndicatorBuilder().ConfigureSource(Bars.From(_bars)).ConfigureIndicators(indicator)
            .ConfigureHistory(history).ConfigureExecution(backend);
        var result = builder.BuildAsync().GetAwaiter().GetResult();
        if (builder.LastExecution?.Backend != backend) throw new InvalidOperationException("Requested backend did not execute.");
        return result;
    }
}

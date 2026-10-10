using AiDotNet.Tensors.Helpers;
using BenchmarkDotNet.Attributes;
using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Indicators;
using TALib;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

// Prepared identical close/bar inputs. Native methods return only their owned
// values; builders also return presence and, by default, owned history.
[MemoryDiagnoser, Config(typeof(TensorsGpuTimingConfig))]
public class SharedPointwiseBenchmarks
{
    public static IEnumerable<string> Cases => new[]
    {
        "Sin", "Cos", "Tan", "Acos", "Atan", "Ln", "Log10", "Exp", "Sinh", "Cosh", "Tanh", "Ceil", "Floor", "Sqrt"
    }.Where(name => Environment.GetEnvironmentVariable("SHARED_POINTWISE") is not { } selected || selected == name);
    [ParamsSource(nameof(Cases))] public string Operation { get; set; } = "";
    [Params(10_000, 100_000)] public int Count { get; set; }
    private Bar[] _bars = null!;
    private double[] _closes = null!;
    private int _previousDop;

    [GlobalSetup]
    public void Setup()
    {
        _previousDop = CpuParallelSettings.MaxDegreeOfParallelism;
        CpuParallelSettings.MaxDegreeOfParallelism = 8;
#pragma warning disable S2245 // Deterministic benchmark fixture.
        var random = new Random(981);
#pragma warning restore S2245
        _closes = Enumerable.Range(0, Count).Select(_ => .125 + random.NextDouble() * .75).ToArray();
        _bars = _closes.Select(v => new Bar(default, v, v, v, v, 1)).ToArray();
        var expected = NativeSingle();
        AsinFeasibilityBenchmarks.RequireSame(expected, NativeParallel());
        foreach (var history in new[] { IndicatorHistoryMode.Full, IndicatorHistoryMode.LatestOnly })
        {
            var indicator = Create();
            using var result = Build(indicator, history);
            AsinFeasibilityBenchmarks.RequireSame(expected, result[indicator.Outputs[0]].ToArray());
            if (result[indicator.Outputs[1]].ToArray().Any(v => v != 1))
                throw new InvalidOperationException("Lost pointwise presence.");
        }
    }
    [GlobalCleanup] public void Cleanup() => CpuParallelSettings.MaxDegreeOfParallelism = _previousDop;
    [Benchmark] public IIndicatorRun BuilderFull() => Build(Create(), IndicatorHistoryMode.Full);
    [Benchmark] public IIndicatorRun BuilderLatestOnly() => Build(Create(), IndicatorHistoryMode.LatestOnly);
    [Benchmark(Baseline = true)] public double[] NativeSingle()
    {
        // Every slot is written by these zero-lookback functions. Give the native
        // arm the same uninitialized-output allocation available to our kernels.
        var output = GC.AllocateUninitializedArray<double>(Count);
        Native(_closes, output);
        return output;
    }
    [Benchmark] public double[] NativeParallel()
    {
        var output = GC.AllocateUninitializedArray<double>(Count);
        int chunks = Math.Min(8, Environment.ProcessorCount);
        CpuParallelSettings.LightweightParallel(chunks, chunks, chunk =>
        {
            int start = (int)((long)Count * chunk / chunks);
            int end = (int)((long)Count * (chunk + 1) / chunks);
            Native(_closes.AsSpan(start, end - start), output.AsSpan(start, end - start));
        });
        return output;
    }
    private IIndicatorRun Build(IIndicator indicator, IndicatorHistoryMode history) => new StockIndicatorBuilder()
        .ConfigureSource(Bars.From(_bars)).ConfigureIndicators(indicator).ConfigureHistory(history)
        .ConfigureExecution(IndicatorExecutionBackend.Cpu).BuildAsync().GetAwaiter().GetResult();

    private IIndicator Create() => Operation switch
    {
        "Sin" => new PriceCircularTransform(PriceCircularOperation.Sine),
        "Cos" => new PriceCircularTransform(PriceCircularOperation.Cosine),
        "Tan" => new PriceCircularTransform(PriceCircularOperation.Tangent),
        "Acos" => new PriceCircularTransform(PriceCircularOperation.ArcCosine),
        "Atan" => new PriceCircularTransform(PriceCircularOperation.ArcTangent),
        "Ln" => new PriceTranscendentalTransform(PriceTranscendentalOperation.NaturalLogarithm),
        "Log10" => new PriceTranscendentalTransform(PriceTranscendentalOperation.CommonLogarithm),
        "Exp" => new PriceTranscendentalTransform(PriceTranscendentalOperation.Exponential),
        "Sinh" => new PriceTranscendentalTransform(PriceTranscendentalOperation.HyperbolicSine),
        "Cosh" => new PriceTranscendentalTransform(PriceTranscendentalOperation.HyperbolicCosine),
        "Tanh" => new PriceTranscendentalTransform(PriceTranscendentalOperation.HyperbolicTangent),
        "Ceil" => new PriceRoundingTransform(PriceRoundingOperation.Ceiling),
        "Floor" => new PriceRoundingTransform(PriceRoundingOperation.Floor),
        "Sqrt" => new PriceRoundingTransform(PriceRoundingOperation.SquareRoot),
        _ => throw new ArgumentOutOfRangeException(nameof(Operation))
    };

    private void Native(ReadOnlySpan<double> input, Span<double> output)
    {
        System.Range range;
        var status = Operation switch
        {
            "Sin" => Functions.Sin<double>(input, System.Range.All, output, out range),
            "Cos" => Functions.Cos<double>(input, System.Range.All, output, out range),
            "Tan" => Functions.Tan<double>(input, System.Range.All, output, out range),
            "Acos" => Functions.Acos<double>(input, System.Range.All, output, out range),
            "Atan" => Functions.Atan<double>(input, System.Range.All, output, out range),
            "Ln" => Functions.Ln<double>(input, System.Range.All, output, out range),
            "Log10" => Functions.Log10<double>(input, System.Range.All, output, out range),
            "Exp" => Functions.Exp<double>(input, System.Range.All, output, out range),
            "Sinh" => Functions.Sinh<double>(input, System.Range.All, output, out range),
            "Cosh" => Functions.Cosh<double>(input, System.Range.All, output, out range),
            "Tanh" => Functions.Tanh<double>(input, System.Range.All, output, out range),
            "Ceil" => Functions.Ceil<double>(input, System.Range.All, output, out range),
            "Floor" => Functions.Floor<double>(input, System.Range.All, output, out range),
            "Sqrt" => Functions.Sqrt<double>(input, System.Range.All, output, out range),
            _ => throw new ArgumentOutOfRangeException(nameof(Operation))
        };
        if (status != TALib.Core.RetCode.Success || range.GetOffsetAndLength(input.Length) != (0, input.Length))
            throw new InvalidOperationException("Native pointwise call failed.");
    }
}

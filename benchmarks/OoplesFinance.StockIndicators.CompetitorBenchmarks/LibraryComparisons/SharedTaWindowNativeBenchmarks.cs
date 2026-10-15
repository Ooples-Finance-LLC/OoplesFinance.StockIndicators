using AiDotNet.Tensors.Helpers;
using BenchmarkDotNet.Attributes;
using OoplesFinance.StockIndicators.Indicators;
using TALib;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

[MemoryDiagnoser, Config(typeof(TensorsGpuTimingConfig))]
public class SharedTaWindowNativeBenchmarks : SharedMappedBenchmarkBase
{
    private const int Period = 20;
    [Params("TaLib.Functions.AvgDev", "TaLib.Functions.Accbands")]
    public string Operation { get; set; } = "";
    private CompetitorData _data = null!;
    private int Columns => Operation == "TaLib.Functions.Accbands" ? 3 : 1;

    [GlobalSetup]
    public void Setup()
    {
        var (pair, data) = PrepareFixture(Operation);
        _data = data;
        var names = pair.OutputNames ?? ["Value"];
        var native = (pair.CompetitorReference ?? pair.Reference)!(data, Period);
        var serial = NativeSingle();
        var parallel = NativeParallel();
        for (int slot = 0; slot < names.Length; slot++)
        {
            var expected = native.Outputs[names[slot]].Values.Skip(Period - 1).ToArray();
            AsinFeasibilityBenchmarks.RequireSame(expected, serial[slot]);
            AsinFeasibilityBenchmarks.RequireSame(expected, parallel[slot]);
        }
        SharedBenchmarkSupport.VerifyMappedOutputs(_bars, Create, pair.Reference!(data, Period), names);
    }

    protected override IIndicator Create() => Operation switch
    {
        "TaLib.Functions.AvgDev" => new WindowMeanAbsoluteDeviation(Period),
        "TaLib.Functions.Accbands" => new RangeAccelerationBands(Period),
        _ => throw new ArgumentException("Unknown TA window operation.")
    };

    [Benchmark(Baseline = true)]
    public double[][] NativeSingle() => NativeRange(0, Math.Max(0, Count - Period + 1));

    private double[][] NativeRange(int start, int end)
    {
        int defined = end - start;
        var output = Allocate(defined);
        if (defined == 0) return output;
        System.Range range;
        // Endpoints are inclusive. Preserve the original start so Accbands'
        // rolling sums retain the exact native addition/subtraction sequence.
        var inputRange = new System.Range(start + Period - 1, end + Period - 2);
        var code = Operation == "TaLib.Functions.Accbands"
            ? Functions.Accbands<double>(_data.Highs, _data.Lows, _data.Closes, inputRange,
                output[0], output[1], output[2], out range, Period)
            : Functions.AvgDev<double>(_data.Closes, inputRange, output[0], out range, Period);
        if (code != TALib.Core.RetCode.Success || !range.Equals(new System.Range(start + Period - 1, end + Period - 1)))
            throw new InvalidOperationException("Unexpected native window result alignment.");
        return output;
    }

    private double[][] Allocate(int length) => Enumerable.Range(0, Columns).Select(_ => new double[length]).ToArray();

    [Benchmark]
    public double[][] NativeParallel()
    {
        int defined = Math.Max(0, Count - Period + 1);
        var output = Allocate(defined);
        int workers = Math.Min(defined, Math.Min(8, Environment.ProcessorCount));
        if (workers == 0) return output;
        CpuParallelSettings.LightweightParallel(workers, workers, worker =>
        {
            int start = defined * worker / workers, end = defined * (worker + 1) / workers;
            // AvgDev recomputes each window independently. Accbands retains
            // its original prefix because its rolling sum carries rounding.
            int first = Operation == "TaLib.Functions.Accbands" ? 0 : start;
            var prefix = NativeRange(first, end);
            for (int slot = 0; slot < output.Length; slot++)
                prefix[slot].AsSpan(start - first, end - start).CopyTo(output[slot].AsSpan(start));
        });
        return output;
    }
}

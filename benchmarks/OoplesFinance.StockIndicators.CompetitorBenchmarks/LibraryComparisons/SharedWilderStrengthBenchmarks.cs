using AiDotNet.Tensors.Helpers;
using BenchmarkDotNet.Attributes;
using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Indicators;
using TALib;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

[MemoryDiagnoser, Config(typeof(TensorsGpuTimingConfig))]
public class SharedWilderStrengthBenchmarks
{
    private const int Period = 20;
    [Params("TaLib.Functions.Rsi", "TaLib.Functions.Cmo")] public string Operation { get; set; } = "";
    [Params(1_000, 10_000)] public int Count { get; set; }
    private double[] _prices = null!;
    private Bar[] _bars = null!;
    private int _priorDop;
    private bool _signed;

    [GlobalSetup]
    public void Setup()
    {
        _priorDop = CpuParallelSettings.MaxDegreeOfParallelism;
        CpuParallelSettings.MaxDegreeOfParallelism = 8;
        _signed = Operation switch
        {
            "TaLib.Functions.Rsi" => false,
            "TaLib.Functions.Cmo" => true,
            _ => throw new ArgumentException("Unknown strength operation.")
        };
        if (Count <= Period) throw new ArgumentOutOfRangeException(nameof(Count));
        if (TALib.Core.CompatibilitySettings.Get() != TALib.Core.CompatibilityMode.Default
            || TALib.Core.UnstablePeriodSettings.Get(_signed ? TALib.Core.UnstableFunc.Cmo : TALib.Core.UnstableFunc.Rsi) != 0)
            throw new InvalidOperationException("Strength benchmarks require default native settings.");
        _prices = Enumerable.Range(0, Count).Select(i => 10 + (i * 13 % 101) / 8d).ToArray();
        _bars = _prices.Select(v => new Bar(default, v, v + 1, v - 1, v, 1)).ToArray();
        var expected = WilderStrengthComparison.OwnedReference(_prices, Period, Convention).Outputs["Value"];
        var native = NativeSingle();
        var nativeExpected = WilderStrengthComparison.NativeReference(_prices, Period, Convention);
        if (nativeExpected.Range.Start.Value != Period || nativeExpected.Range.End.Value != Count)
            throw new InvalidOperationException("Unexpected independent strength reference range.");
        AsinFeasibilityBenchmarks.RequireSame(nativeExpected.Values, native);
        AsinFeasibilityBenchmarks.RequireSame(native, NativeParallel());
        for (int i = Period; i < Count; i++)
            if (!double.IsFinite(native[i - Period]) || Math.Abs(native[i - Period] - expected.Values[i]) > 1e-10)
                throw new InvalidOperationException("Native strength differs from independent reference.");
        foreach (var history in Enum.GetValues<IndicatorHistoryMode>())
        {
            var indicator = Create();
            using var run = Build(indicator, history);
            AsinFeasibilityBenchmarks.RequireSame(expected.Values.Select((v, i) => expected.Present![i] ? v : 0).ToArray(),
                run[indicator.Value].ToArray());
            AsinFeasibilityBenchmarks.RequireSame(expected.Present!.Select(v => v ? 1d : 0d).ToArray(),
                run[indicator.IsDefined].ToArray());
        }
    }

    private WilderStrengthConvention Convention => _signed ? WilderStrengthConvention.ChandeZeroFlat : WilderStrengthConvention.RsiZeroFlat;
    private WilderStrengthOscillator Create() => new(Period, Convention);
    [GlobalCleanup] public void Cleanup() => CpuParallelSettings.MaxDegreeOfParallelism = _priorDop;
    [Benchmark] public IIndicatorRun BuilderFull() => Build(Create(), IndicatorHistoryMode.Full);
    [Benchmark] public IIndicatorRun BuilderLatestOnly() => Build(Create(), IndicatorHistoryMode.LatestOnly);
    [Benchmark(Baseline = true)] public double[] NativeSingle()
    {
        var result = new double[Count - Period];
        Native(_prices, result);
        return result;
    }
    [Benchmark] public double[] NativeParallel()
    {
        var result = new double[Count - Period];
        int chunks = Math.Min(result.Length, Math.Min(8, Environment.ProcessorCount));
        CpuParallelSettings.LightweightParallel(chunks, chunks, chunk =>
        {
            int start = result.Length * chunk / chunks, end = result.Length * (chunk + 1) / chunks;
            // Wilder recurrences depend on the full prefix. Include replay and
            // worker scratch allocation rather than reseeding at chunk boundaries.
            var prefix = new double[end];
            Native(_prices.AsSpan(0, end + Period), prefix);
            prefix.AsSpan(start, end - start).CopyTo(result.AsSpan(start, end - start));
        });
        return result;
    }
    private void Native(ReadOnlySpan<double> input, Span<double> output)
    {
        System.Range range;
        var code = _signed ? Functions.Cmo<double>(input, System.Range.All, output, out range, Period)
            : Functions.Rsi<double>(input, System.Range.All, output, out range, Period);
        if (code != TALib.Core.RetCode.Success || range.Start.Value != Period || range.End.Value != input.Length)
            throw new InvalidOperationException("Unexpected native strength output range.");
    }
    private IIndicatorRun Build(IIndicator indicator, IndicatorHistoryMode history) => SharedBenchmarkSupport.BuildCpu(_bars, indicator, history);
}

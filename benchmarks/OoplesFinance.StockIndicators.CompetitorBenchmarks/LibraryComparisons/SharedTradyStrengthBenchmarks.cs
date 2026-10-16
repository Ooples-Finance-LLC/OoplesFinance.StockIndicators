using AiDotNet.Tensors.Helpers;
using BenchmarkDotNet.Attributes;
using OoplesFinance.StockIndicators.Indicators;
using Trady.Analysis.Indicator;
using Trady.Analysis.Infrastructure;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

[MemoryDiagnoser, Config(typeof(TensorsGpuTimingConfig))]
public class SharedTradyStrengthBenchmarks : SharedMappedBenchmarkBase
{
    private const int Period = 20;
    [Params("RelativeStrength", "RelativeStrengthIndex", "NetMomentumOscillator", "RelativeMomentum", "RelativeMomentumIndex")]
    public string Operation { get; set; } = "";
    private NullableStrengthConvention _convention;
    private int _lag;
    private decimal?[] _prices = null!;
    private IEnumerable<decimal?>[] _prefixes = null!;
    private int[] _ends = null!;

    [GlobalSetup]
    public void Setup()
    {
        _convention = Operation switch
        {
            "RelativeStrength" => NullableStrengthConvention.RelativeStrength,
            "RelativeStrengthIndex" => NullableStrengthConvention.RelativeStrengthIndex,
            "NetMomentumOscillator" => NullableStrengthConvention.NetMomentum,
            "RelativeMomentum" => NullableStrengthConvention.RelativeMomentum,
            "RelativeMomentumIndex" => NullableStrengthConvention.RelativeMomentumIndex,
            _ => throw new ArgumentException("Unknown Trady strength operation.")
        };
        if (Count < 1) throw new ArgumentOutOfRangeException(nameof(Count));
        _lag = NullableStrengthComparison.Momentum(_convention) ? 5 : 1;
        var (pair, data) = PrepareFixture("Trady.Indicator." + Operation);
        _prices = data.Candles.Select(candle => (decimal?)candle.Close).ToArray();
        int workers = Math.Min(Count, Math.Min(8, Environment.ProcessorCount));
        _ends = Enumerable.Range(1, workers).Select(i => (int)((long)Count * i / workers)).ToArray();
        _prefixes = _ends.Select(end => _prices.Take(end)).ToArray();
        SharedBenchmarkSupport.VerifyMappedOutputs(_bars, Create, pair.Reference!(data, Period), pair.OutputNames ?? ["Value"]);
        var expected = NullableStrengthComparison.NativeReference(_prices, Period, _convention, _lag);
        if (!expected.SequenceEqual(NativeSingle()) || !expected.SequenceEqual(NativeParallel()))
            throw new InvalidOperationException("Native strength values or presence differ from the independent reference.");
    }

    protected override IIndicator Create() => new NullableStrengthOscillator(Period, _convention, _lag);

    // The tuple overload avoids candle wrappers and returns its eager native List.
    [Benchmark(Baseline = true)]
    public List<decimal?> NativeSingle() => (List<decimal?>)Native(_prices).Compute();

    [Benchmark]
    public decimal?[] NativeParallel()
    {
        var result = new decimal?[Count];
        CpuParallelSettings.LightweightParallel(_ends.Length, _ends.Length, worker =>
        {
            int start = worker == 0 ? 0 : _ends[worker - 1], end = _ends[worker];
            // Retain the entire prefix for recursive averages, while requesting
            // only this worker's output range through the public native API.
            var local = (List<decimal?>)Native(_prefixes[worker]).Compute(startIndex: start, endIndex: end - 1);
            if (local.Count != end - start) throw new InvalidOperationException("Native range length differs.");
            local.CopyTo(0, result, start, local.Count);
        });
        return result;
    }

    private AnalyzableBase<decimal?, decimal?, decimal?, decimal?> Native(IEnumerable<decimal?> prices) => _convention switch
    {
        NullableStrengthConvention.RelativeStrength => new RelativeStrengthByTuple(prices, Period),
        NullableStrengthConvention.RelativeStrengthIndex => new RelativeStrengthIndexByTuple(prices, Period),
        NullableStrengthConvention.NetMomentum => new NetMomentumOscillatorByTuple(prices, Period),
        NullableStrengthConvention.RelativeMomentum => new RelativeMomentumByTuple(prices, Period, _lag),
        NullableStrengthConvention.RelativeMomentumIndex => new RelativeMomentumIndexByTuple(prices, Period, _lag),
        _ => throw new InvalidOperationException("Unknown strength convention.")
    };
}

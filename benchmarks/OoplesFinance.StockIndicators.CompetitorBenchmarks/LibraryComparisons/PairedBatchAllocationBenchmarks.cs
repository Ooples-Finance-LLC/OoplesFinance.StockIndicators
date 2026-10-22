using System.Reflection;
using BenchmarkDotNet.Attributes;
using OoplesFinance.StockIndicators.Core;
using OoplesFinance.StockIndicators.Enums;
using OoplesFinance.StockIndicators.Streaming;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

[MemoryDiagnoser, Config(typeof(TensorsGpuTimingConfig))]
public class PairedBatchAllocationBenchmarks
{
    [Params(1_000, 10_000)] public int Count { get; set; }
    [Params("ComparePriceMomentumOscillator", "RelativeNormalizedVolatility", "RelativeStrength3DIndicator", "SectorRotationModel")]
    public string Operation { get; set; } = "ComparePriceMomentumOscillator";
    private delegate Dictionary<string, List<double>> Runner(IReadOnlyList<double> primary, IReadOnlyList<double> benchmark,
        Func<SeriesKey, SeriesKey, IMultiSeriesIndicatorState> factory);
    private Runner _legacy = null!, _candidate = null!;
    private Func<SeriesKey, SeriesKey, IMultiSeriesIndicatorState> _factory = null!;
    private double[] _primary = null!, _benchmark = null!;

    [GlobalSetup]
    public void Setup()
    {
        _primary = Enumerable.Range(0, Count).Select(i => 10 + (i * 13 % 101) / 8d).ToArray();
        _benchmark = Enumerable.Range(0, Count).Select(i => 9 + (i * 7 % 97) / 16d).ToArray();
        _factory = Create; _legacy = LegacyRun;
        var type = typeof(IMultiSeriesIndicatorState).Assembly.GetType("OoplesFinance.StockIndicators.Helpers.PairedBatch", throwOnError: true)!;
        _candidate = (Runner)type.GetMethod("Run", BindingFlags.NonPublic | BindingFlags.Static)!.CreateDelegate(typeof(Runner));
        // Bind reflection once, outside timing. Both timed arms invoke the same delegate signature.
        var expected = _legacy(_primary, _benchmark, _factory);
        var actual = _candidate(_primary, _benchmark, _factory);
        if (!expected.Keys.SequenceEqual(actual.Keys)) throw new InvalidOperationException("Paired output keys changed.");
        foreach (var key in expected.Keys)
        {
            if (expected[key].Count != Count || actual[key].Count != Count) throw new InvalidOperationException("Paired output count changed.");
            for (var i = 0; i < Count; i++)
                if (BitConverter.DoubleToInt64Bits(expected[key][i]) != BitConverter.DoubleToInt64Bits(actual[key][i]))
                    throw new InvalidOperationException($"Paired output mismatch: {key}[{i}].");
        }
    }

    [Benchmark(Baseline = true)] public Dictionary<string, List<double>> Legacy() => _legacy(_primary, _benchmark, _factory);
    [Benchmark] public Dictionary<string, List<double>> Shared() => _candidate(_primary, _benchmark, _factory);

    private IMultiSeriesIndicatorState Create(SeriesKey p, SeriesKey b) => Operation switch
    {
        "ComparePriceMomentumOscillator" => new ComparePriceMomentumOscillatorState(p, b),
        "RelativeNormalizedVolatility" => new RelativeNormalizedVolatilityState(p, b),
        "RelativeStrength3DIndicator" => new RelativeStrength3DIndicatorState(p, b),
        "SectorRotationModel" => new SectorRotationModelState(p, b),
        _ => throw new InvalidOperationException("Unknown paired operation.")
    };

    // Exact pre-optimization PairedBatch.Run from base 71c31b97, with method access and context construction adapted for this assembly.
    private static Dictionary<string, List<double>> LegacyRun(IReadOnlyList<double> primary, IReadOnlyList<double> benchmark,
        Func<SeriesKey, SeriesKey, IMultiSeriesIndicatorState> factory)
    {
        var p = new SeriesKey("PRIMARY", BarTimeframe.Minutes(1));
        var b = new SeriesKey("BENCHMARK", BarTimeframe.Minutes(1));
        var state = factory(p, b);
        using var lifetime = state as IDisposable;
        var context = PreservedSectorRotationBatch.CreateContext();
        var result = new Dictionary<string, List<double>>();
        for (var i = 0; i < primary.Count; i++)
        {
            var time = new DateTime(2000, 1, 1, 0, 0, 0, DateTimeKind.Utc).AddMinutes(i);
            OhlcvBar Bar(SeriesKey key, double value) => new(key.Symbol, key.Timeframe, time, time, value, value, value, value, 0, true);
            state.Update(context, b, Bar(b, benchmark[i]), true, false);
            var point = state.Update(context, p, Bar(p, primary[i]), true, true);
            if (!point.HasValue || point.Outputs is null) throw new InvalidOperationException("An aligned pair did not publish outputs.");
            foreach (var output in point.Outputs)
            {
                if (!result.TryGetValue(output.Key, out var values)) result[output.Key] = values = new List<double>(primary.Count);
                values.Add(output.Value);
            }
        }
        return result;
    }
}

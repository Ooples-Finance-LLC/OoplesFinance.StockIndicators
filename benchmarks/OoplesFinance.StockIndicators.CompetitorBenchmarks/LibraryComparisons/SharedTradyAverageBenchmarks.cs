using AiDotNet.Tensors.Helpers;
using BenchmarkDotNet.Attributes;
using OoplesFinance.StockIndicators.Indicators;
using Trady.Analysis.Indicator;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

[MemoryDiagnoser, Config(typeof(TensorsGpuTimingConfig))]
public class SharedTradyAverageBenchmarks : SharedMappedBenchmarkBase
{
    private const int Period = 20;
    [Params("ExponentialMovingAverage", "WeightedMovingAverage", "SimpleMovingAverageOscillator", "ExponentialMovingAverageOscillator")]
    public string Operation { get; set; } = "";
    private decimal[] _prices = null!;
    private decimal?[] _nullablePrices = null!;
    private IEnumerable<decimal>[] _windows = null!;
    private IEnumerable<decimal?>[] _nullableWindows = null!;
    private int[] _ends = null!;
    private int[] _seeds = null!;

    [GlobalSetup]
    public void Setup()
    {
        int? preceding = Operation switch
        {
            "WeightedMovingAverage" => Period - 1,
            "SimpleMovingAverageOscillator" => 25,
            "ExponentialMovingAverage" or "ExponentialMovingAverageOscillator" => null,
            _ => throw new ArgumentException("Unknown Trady average operation.")
        };
        ArgumentOutOfRangeException.ThrowIfLessThan(Count, 1);
        var (pair, data) = PrepareFixture("Trady.Indicator." + Operation);
        _prices = data.Candles.Select(candle => candle.Close).ToArray();
        _nullablePrices = _prices.Select(value => (decimal?)value).ToArray();
        int workers = Math.Min(Count, Math.Min(8, Environment.ProcessorCount));
        _ends = Enumerable.Range(1, workers).Select(i => (int)((long)Count * i / workers)).ToArray();
        _seeds = Enumerable.Range(0, workers).Select(i => preceding.HasValue
            ? Math.Max(0, (i == 0 ? 0 : _ends[i - 1]) - preceding.Value) : 0).ToArray();
        _windows = _ends.Select((end, i) => _prices.Skip(_seeds[i]).Take(end - _seeds[i])).ToArray();
        _nullableWindows = _ends.Select((end, i) => _nullablePrices.Skip(_seeds[i]).Take(end - _seeds[i])).ToArray();
        bool scalar = Operation is "ExponentialMovingAverage" or "WeightedMovingAverage";
        var owned = scalar ? SharedAverageOscillatorBenchmarks.ScalarReference(_bars, Operation == "ExponentialMovingAverage")
            : pair.Reference!(data, Period);
        SharedBenchmarkSupport.VerifyMappedOutputs(_bars, Create, owned, pair.OutputNames ?? ["Value"], includeScalarStartup: scalar);
        var native = scalar ? ScalarNativeReference(_prices, Operation == "ExponentialMovingAverage")
            : AverageDifferenceComparison.NativeReference(_prices, 12, 26, Operation == "ExponentialMovingAverageOscillator");
        if (!native.SequenceEqual(NativeSingle()) || !native.SequenceEqual(NativeParallel()))
            throw new InvalidOperationException("Native decimal averages or presence differ from their independent reference.");
    }

    protected override IIndicator Create() => Operation switch
    {
        "ExponentialMovingAverage" => new FirstValueEma(Period),
        "WeightedMovingAverage" => new Wma(Period),
        "SimpleMovingAverageOscillator" => new MovingAverageDifference(),
        "ExponentialMovingAverageOscillator" => new MovingAverageDifference(exponential: true),
        _ => throw new InvalidOperationException("Unknown operation.")
    };

    [Benchmark(Baseline = true)]
    public List<decimal?> NativeSingle() => Compute(_prices, _nullablePrices);

    [Benchmark]
    public decimal?[] NativeParallel()
    {
        var result = new decimal?[Count];
        CpuParallelSettings.LightweightParallel(_ends.Length, _ends.Length, worker =>
        {
            int first = worker == 0 ? 0 : _ends[worker - 1];
            // Computation and native output allocation stay inside timing.
            // Public range computation avoids returning unowned prefix rows.
            var rows = Compute(_windows[worker], _nullableWindows[worker], first - _seeds[worker], _ends[worker] - _seeds[worker] - 1);
            if (rows.Count != _ends[worker] - first) throw new InvalidOperationException("Native average range length differs.");
            rows.CopyTo(0, result, first, rows.Count);
        });
        return result;
    }

    private List<decimal?> Compute(IEnumerable<decimal> prices, IEnumerable<decimal?> nullable, int? first = null, int? last = null) =>
        (List<decimal?>)(Operation switch
        {
            "ExponentialMovingAverage" => new ExponentialMovingAverageByTuple(nullable, Period).Compute(startIndex: first, endIndex: last),
            "WeightedMovingAverage" => new WeightedMovingAverageByTuple(nullable, Period).Compute(startIndex: first, endIndex: last),
            "SimpleMovingAverageOscillator" => new SimpleMovingAverageOscillatorByTuple(prices, 12, 26).Compute(startIndex: first, endIndex: last),
            "ExponentialMovingAverageOscillator" => new ExponentialMovingAverageOscillatorByTuple(prices, 12, 26).Compute(startIndex: first, endIndex: last),
            _ => throw new InvalidOperationException("Unknown operation.")
        });

    private static decimal?[] ScalarNativeReference(decimal[] prices, bool exponential)
    {
        var values = new decimal?[prices.Length];
        decimal alpha = 2m / (Period + 1);
        for (int i = 0; i < prices.Length; i++)
        {
            if (exponential)
                values[i] = i == 0 ? prices[i] : values[i - 1] + alpha * (prices[i] - values[i - 1]);
            else if (i >= Period - 1)
            {
                decimal sum = 0;
                // The native decimal contract rounds each weighted ratio before summing.
                for (int weight = 1; weight <= Period; weight++)
                    sum += weight * prices[i - Period + weight] / (Period * (Period + 1) / 2m);
                values[i] = sum;
            }
        }
        return values;
    }
}

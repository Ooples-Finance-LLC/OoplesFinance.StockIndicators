using AiDotNet.Tensors.Helpers;
using BenchmarkDotNet.Attributes;
using OoplesFinance.StockIndicators.Indicators;
using Trady.Analysis.Indicator;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

[MemoryDiagnoser, Config(typeof(TensorsGpuTimingConfig))]
public class SharedTradyStochasticMomentumBenchmarks : SharedMappedBenchmarkBase
{
    [Params("StochasticsMomentum", "StochasticsMomentumIndex", "StochasticsRsiOscillator")]
    public string Operation { get; set; } = "";
    private (decimal High, decimal Low, decimal Close)[] _hlc = null!;
    private decimal?[] _closes = null!;
    private IEnumerable<(decimal High, decimal Low, decimal Close)>[] _windows = null!;
    private IEnumerable<decimal?>[] _closeWindows = null!;
    private int[] _ends = null!, _seeds = null!;

    [GlobalSetup]
    public void Setup()
    {
        if (Operation is not ("StochasticsMomentum" or "StochasticsMomentumIndex" or "StochasticsRsiOscillator"))
            throw new ArgumentException("Unknown Trady stochastic momentum operation.");
        ArgumentOutOfRangeException.ThrowIfLessThan(Count, 1);
        var (pair, data) = PrepareFixture("Trady.Indicator." + Operation);
        _hlc = data.Candles.Select(c => (c.High, c.Low, c.Close)).ToArray();
        _closes = data.Candles.Select(c => (decimal?)c.Close).ToArray();
        int workers = Math.Min(Count, Math.Min(8, Environment.ProcessorCount));
        _ends = Enumerable.Range(1, workers).Select(i => (int)((long)Count * i / workers)).ToArray();
        // Raw momentum needs only its 20-bar extrema window. Smoothed momentum
        // and RSI retain full prefixes for their recursive states.
        _seeds = Enumerable.Range(0, workers).Select(i => Operation == "StochasticsMomentum"
            ? Math.Max(0, (i == 0 ? 0 : _ends[i - 1]) - 19) : 0).ToArray();
        _windows = _ends.Select((end, i) => _hlc.Skip(_seeds[i]).Take(end - _seeds[i])).ToArray();
        _closeWindows = _ends.Select((end, i) => _closes.Skip(_seeds[i]).Take(end - _seeds[i])).ToArray();
        // The owned smoothed momentum exposes its signal as well as its index.
        var referencePair = Operation == "StochasticsMomentumIndex" ? StochasticMomentumComparison.Create("Skender") : pair;
        SharedBenchmarkSupport.VerifyMappedOutputs(_bars, Create, referencePair.Reference!(data, 20), referencePair.OutputNames ?? ["Value"]);
        var expected = Operation == "StochasticsRsiOscillator"
            ? StochasticRsiComparison.TradyReference(_closes, 20)
            : StochasticMomentumComparison.TradyReference(_hlc, 20, Operation == "StochasticsMomentumIndex", 25, 2);
        if (!expected.SequenceEqual(NativeSingle()) || !expected.SequenceEqual(NativeParallel()))
            throw new InvalidOperationException("Native stochastic momentum decimal values or presence differ from the independent reference.");
    }

    protected override IIndicator Create() => Operation switch
    {
        "StochasticsMomentum" => new WindowStochasticMomentum(20),
        "StochasticsMomentumIndex" => new DoubleSmoothedStochasticMomentum(20, 25, 2, 3),
        _ => new NullableStochasticRsi(20)
    };

    [Benchmark(Baseline = true)]
    public List<decimal?> NativeSingle() => Compute(_hlc, _closes);

    [Benchmark]
    public decimal?[] NativeParallel()
    {
        var joined = new decimal?[Count];
        CpuParallelSettings.LightweightParallel(_ends.Length, _ends.Length, worker =>
        {
            int first = worker == 0 ? 0 : _ends[worker - 1];
            var rows = Compute(_windows[worker], _closeWindows[worker], first - _seeds[worker], _ends[worker] - _seeds[worker] - 1);
            if (rows.Count != _ends[worker] - first) throw new InvalidOperationException("Native stochastic momentum range length differs.");
            rows.CopyTo(0, joined, first, rows.Count);
        });
        return joined;
    }

    private List<decimal?> Compute(IEnumerable<(decimal High, decimal Low, decimal Close)> hlc,
        IEnumerable<decimal?> closes, int? first = null, int? last = null) =>
        (List<decimal?>)(Operation switch
        {
            "StochasticsMomentum" => new StochasticsMomentumByTuple(hlc, 20).Compute(startIndex: first, endIndex: last),
            "StochasticsMomentumIndex" => new StochasticsMomentumIndexByTuple(hlc, 20, 25, 2).Compute(startIndex: first, endIndex: last),
            _ => new StochasticsRsiOscillatorByTuple(closes, 20).Compute(startIndex: first, endIndex: last)
        });
}

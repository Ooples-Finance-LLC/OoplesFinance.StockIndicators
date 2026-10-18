using AiDotNet.Tensors.Helpers;
using BenchmarkDotNet.Attributes;
using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Indicators;
using Trady.Analysis.Indicator;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

[MemoryDiagnoser, Config(typeof(TensorsGpuTimingConfig))]
public class SharedTradyCciAdaptiveBenchmarks : SharedMappedBenchmarkBase
{
    [Params("CommodityChannelIndex", "KaufmanAdaptiveMovingAverage")]
    public string Operation { get; set; } = "";
    private bool _adaptive;
    private (decimal High, decimal Low, decimal Close)[] _hlc = null!;
    private decimal[] _closes = null!;
    private IEnumerable<(decimal High, decimal Low, decimal Close)>[] _windows = null!;
    private IEnumerable<decimal>[] _closeWindows = null!;
    private int[] _ends = null!, _seeds = null!;

    [GlobalSetup]
    public void Setup()
    {
        _adaptive = Operation switch
        {
            "CommodityChannelIndex" => false,
            "KaufmanAdaptiveMovingAverage" => true,
            _ => throw new ArgumentException("Unknown Trady CCI/adaptive operation.")
        };
        ArgumentOutOfRangeException.ThrowIfLessThan(Count, 1);
        var (pair, data) = PrepareFixture("Trady.Indicator." + Operation);
        _hlc = data.Candles.Select(c => (c.High, c.Low, c.Close)).ToArray();
        _closes = data.Candles.Select(c => c.Close).ToArray();
        int workers = Math.Min(Count, Math.Min(8, Environment.ProcessorCount));
        _ends = Enumerable.Range(1, workers).Select(i => (int)((long)Count * i / workers)).ToArray();
        // Native CCI averages 20 deviations from their respective 20-bar means.
        // KAMA retains the entire prefix for its recursive adaptive state.
        _seeds = Enumerable.Range(0, workers).Select(i => _adaptive ? 0
            : Math.Max(0, (i == 0 ? 0 : _ends[i - 1]) - 38)).ToArray();
        _windows = _ends.Select((end, i) => _hlc.Skip(_seeds[i]).Take(end - _seeds[i])).ToArray();
        _closeWindows = _ends.Select((end, i) => _closes.Skip(_seeds[i]).Take(end - _seeds[i])).ToArray();
        if (_adaptive)
        {
            // The owned contract retains efficiency as well as the mapped average.
            var expected = SeededAdaptiveComparison.ReferenceValues(data.Closes, 20, 2, 30, false, false, false, false, 0);
            foreach (var mode in Enum.GetValues<IndicatorHistoryMode>())
            {
                var owner = Create();
                using var run = SharedBenchmarkSupport.BuildCpu(_bars, owner, mode);
                for (int slot = 0; slot < expected.Length; slot++)
                {
                    AsinFeasibilityBenchmarks.RequireSame(expected[slot].Select(v => v ?? 0).ToArray(), run[owner.Outputs[slot]].ToArray());
                    AsinFeasibilityBenchmarks.RequireSame(expected[slot].Select(v => v.HasValue ? 1d : 0d).ToArray(), run[owner.Outputs[slot + expected.Length]].ToArray());
                }
            }
        }
        else SharedBenchmarkSupport.VerifyMappedOutputs(_bars, Create, pair.Reference!(data, 20), ["Value"]);
        var native = _adaptive
            ? TradyAdaptiveComparison.DecimalReference(_closes.Select(v => (decimal?)v).ToArray(), 20, 2, 30)
            : CommodityChannelComparison.TradyReference(_hlc, 20);
        if (!native.SequenceEqual(NativeSingle()) || !native.SequenceEqual(NativeParallel()))
            throw new InvalidOperationException("Native CCI/adaptive decimal values or presence differ from the independent reference.");
    }

    protected override IIndicator Create() => _adaptive
        ? new SeededAdaptiveAverage(20, resetFlat: false)
        : new WindowCommodityChannelIndex(20, true, true);

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
            if (rows.Count != _ends[worker] - first) throw new InvalidOperationException("Native CCI/adaptive range length differs.");
            rows.CopyTo(0, joined, first, rows.Count);
        });
        return joined;
    }

    private List<decimal?> Compute(IEnumerable<(decimal High, decimal Low, decimal Close)> hlc,
        IEnumerable<decimal> closes, int? first = null, int? last = null) =>
        (List<decimal?>)(_adaptive
            ? new KaufmanAdaptiveMovingAverageByTuple(closes, 20, 2, 30).Compute(startIndex: first, endIndex: last)
            : new CommodityChannelIndexByTuple(hlc, 20).Compute(startIndex: first, endIndex: last));
}

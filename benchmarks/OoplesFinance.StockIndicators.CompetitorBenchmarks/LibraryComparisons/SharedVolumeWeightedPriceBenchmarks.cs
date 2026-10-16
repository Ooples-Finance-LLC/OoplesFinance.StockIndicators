using AiDotNet.Tensors.Helpers;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Filters;
using BenchmarkDotNet.Jobs;
using BenchmarkDotNet.Running;
using BenchmarkDotNet.Toolchains.InProcess.Emit;
using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Indicators;
using Skender.Stock.Indicators;
using Trady.Analysis.Indicator;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

// Trady's 10k native call takes seconds. One invocation already exceeds the
// iteration time target; adaptive batching can exhaust the in-process timeout.
public sealed class SharedVolumeWeightedPriceTimingConfig : ManualConfig
{
    public SharedVolumeWeightedPriceTimingConfig()
    {
        var job = Job.ShortRun
            .WithToolchain(new InProcessEmitToolchain(TimeSpan.FromMinutes(30), true))
            .WithUnrollFactor(1).WithWarmupCount(10).WithIterationCount(10)
            .WithMinIterationTime(Perfolizer.Horology.TimeInterval.FromMilliseconds(100));
        AddJob(job.WithId("Adaptive"));
        AddJob(job.WithInvocationCount(1).WithId("SlowNative"));
        AddFilter(new InvocationFilter());
    }

    private sealed class InvocationFilter : IFilter
    {
        public bool Predicate(BenchmarkCase benchmarkCase)
        {
            bool slow = (string)benchmarkCase.Parameters.Items.Single(p => p.Name == "Operation").Value
                    == "Trady.Indicator.VolumeWeightedAveragePrice"
                && (int)benchmarkCase.Parameters.Items.Single(p => p.Name == "Count").Value == 10_000
                && benchmarkCase.Descriptor.WorkloadMethod.Name.StartsWith("Native", StringComparison.Ordinal);
            return benchmarkCase.Job.Id == (slow ? "SlowNative" : "Adaptive");
        }
    }
}

[MemoryDiagnoser, Config(typeof(SharedVolumeWeightedPriceTimingConfig))]
public class SharedVolumeWeightedPriceBenchmarks
{
    private const int Period = 20;
    [Params("Skender.GetVwap", "Skender.GetVwma", "Trady.Indicator.VolumeWeightedAveragePrice")]
    public string Operation { get; set; } = "Skender.GetVwap";
    [Params(1_000, 10_000)] public int Count { get; set; }
    private Quote[] _quotes = null!;
    private (decimal High, decimal Low, decimal Close, decimal Volume)[] _tuples = null!;
    private Bar[] _bars = null!;
    private int _chunks, _priorDop;
    private bool _settingsSaved;
    private bool Rolling => Operation == "Skender.GetVwma";
    private bool Trady => Operation == "Trady.Indicator.VolumeWeightedAveragePrice";

    [GlobalSetup]
    public void Setup()
    {
        if (!VolumePriceComparison.Pairs.Any(p => p.Id == Operation)) throw new ArgumentException("Unsupported weighted price operation.");
        if (Count < 1) throw new ArgumentOutOfRangeException(nameof(Count));
        _priorDop = CpuParallelSettings.MaxDegreeOfParallelism;
        _settingsSaved = true;
        try
        {
            CpuParallelSettings.MaxDegreeOfParallelism = 8;
            var closes = Enumerable.Range(0, Count).Select(i => 10 + (i * 13 % 101) / 8d).ToArray();
            var highs = closes.Select((v, i) => v + (i % 3 + 1) / 4d).ToArray();
            var lows = closes.Select(v => v - .5).ToArray();
            var volumes = Enumerable.Range(0, Count).Select(i => (double)(i % 17 + 1)).ToArray();
            var data = CompetitorData.FromOhlcv(closes, highs, lows, closes, volumes);
            _quotes = data.Quotes.ToArray(); _bars = data.IndicatorBars;
            _tuples = _quotes.Select(q => (q.High, q.Low, q.Close, q.Volume)).ToArray();
            _chunks = Math.Min(Count, Math.Min(8, Environment.ProcessorCount));
            var pair = VolumePriceComparison.Pairs.Single(p => p.Id == Operation);
            var expected = pair.Reference!(data, Period).Outputs["Value"];
            var native = Trady ? TradyReference() : pair.CompetitorReference!(data, Period).Outputs["Value"];
            CheckNative(NativeSingle(), native); CheckNative(NativeParallel(), native);
            foreach (var history in Enum.GetValues<IndicatorHistoryMode>())
            {
                var indicator = Create();
                using var run = Build(indicator, history);
                if (run[indicator.Value].Length != Count || run[indicator.IsDefined].Length != Count)
                    throw new InvalidOperationException("Weighted price column length differs.");
                for (int i = 0; i < Count; i++)
                {
                    bool present = expected.Present![i];
                    double value = present ? expected.Values[i] : 0;
                    if (BitConverter.DoubleToInt64Bits(value) != BitConverter.DoubleToInt64Bits(run[indicator.Value][i])
                        || run[indicator.IsDefined][i] != (present ? 1d : 0d)) // NOSONAR: S1244 - Presence columns are discrete 0/1 flags, not approximate measurements.
                        throw new InvalidOperationException($"Exact weighted price differs at {i}.");
                    if (present != native.Present![i] || present && Math.Abs(value - native.Values[i]) > 1e-10)
                        throw new InvalidOperationException($"Exact and native weighted price disagree at {i}.");
                }
            }
        }
        catch { Cleanup(); throw; }
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        if (!_settingsSaved) return;
        CpuParallelSettings.MaxDegreeOfParallelism = _priorDop;
        _settingsSaved = false;
    }

    [Benchmark] public IIndicatorRun BuilderFull() => Build(Create(), IndicatorHistoryMode.Full);
    [Benchmark] public IIndicatorRun BuilderLatestOnly() => Build(Create(), IndicatorHistoryMode.LatestOnly);
    [Benchmark(Baseline = true)]
    public object NativeSingle() => Trady ? new VolumeWeightedAveragePriceByTuple(_tuples, null).Compute()
        : Rolling ? _quotes.GetVwma(Period) : _quotes.GetVwap();

    [Benchmark]
    public object NativeParallel()
    {
        if (Trady)
        {
            var result = new decimal?[Count];
            CpuParallelSettings.LightweightParallel(_chunks, _chunks, chunk =>
            {
                int start = Count * chunk / _chunks, end = Count * (chunk + 1) / _chunks;
                // Each requested index retains its original cumulative prefix.
                // Tuple preparation is setup-only; native execution and copying
                // its eager result into the owned output remain measured.
                var rows = new VolumeWeightedAveragePriceByTuple(_tuples, null).Compute(startIndex: start, endIndex: end - 1);
                for (int i = 0; i < rows.Count; i++) result[start + i] = rows[i];
            });
            return result;
        }
        return Rolling ? NativeRollingParallel() : NativeCumulativeParallel();
    }

    private VwmaResult[] NativeRollingParallel()
    {
        var result = new VwmaResult[Count];
        CpuParallelSettings.LightweightParallel(_chunks, _chunks, chunk =>
        {
            int start = Count * chunk / _chunks, end = Count * (chunk + 1) / _chunks;
            int first = Math.Max(0, start - Period + 1), index = first;
            foreach (var row in _quotes.Skip(first).Take(end - first).GetVwma(Period))
            {
                if (index >= start) result[index] = row;
                index++;
            }
        });
        return result;
    }

    private VwapResult[] NativeCumulativeParallel()
    {
        var result = new VwapResult[Count];
        CpuParallelSettings.LightweightParallel(_chunks, _chunks, chunk =>
        {
            int start = Count * chunk / _chunks, end = Count * (chunk + 1) / _chunks, index = 0;
            // Skender maintains rounded cumulative totals: prefix replay keeps
            // its exact summation order. Replay and quote conversion are timed.
            foreach (var row in _quotes.Take(end).GetVwap())
            {
                if (index >= start) result[index] = row;
                index++;
            }
        });
        return result;
    }

    internal VolumeWeightedPrice Create() => new(Rolling ? Period : null, typicalPrice: !Rolling);
    internal IIndicatorRun Build(VolumeWeightedPrice indicator, IndicatorHistoryMode history, bool gpu = false)
    {
        var builder = new StockIndicatorBuilder().ConfigureSource(Bars.From(_bars)).ConfigureIndicators(indicator)
            .ConfigureHistory(history).ConfigureExecution(gpu ? IndicatorExecutionBackend.Gpu : IndicatorExecutionBackend.Cpu);
        var run = builder.BuildAsync().GetAwaiter().GetResult();
        if (gpu && builder.LastExecution?.Backend != IndicatorExecutionBackend.Gpu)
        { run.Dispose(); throw new InvalidOperationException("Weighted-price fixture did not execute on GPU."); }
        return run;
    }

    private ComparisonOutput TradyReference()
    {
        // Each cumulative decimal prefix has the same left-to-right order.
        // Retaining the previous prefix avoids quadratic reference setup work.
        var values = new double[Count];
        decimal weighted = 0, volume = 0;
        for (int i = 0; i < Count; i++)
        {
            var q = _quotes[i];
            decimal typical = (q.High + q.Low + q.Close) / 3;
            weighted += typical * q.Volume;
            volume += q.Volume;
            values[i] = (double)(weighted / volume);
        }
        return new ComparisonOutput(0, values, Enumerable.Repeat(true, Count).ToArray());
    }

    private void CheckNative(object result, ComparisonOutput expected)
    {
        double?[] values;
        if (Trady && result is IReadOnlyList<decimal?> decimals) values = decimals.Select(v => (double?)v).ToArray();
        else if (Rolling && result is IReadOnlyList<VwmaResult> rolling)
        {
            if (!rolling.Select(r => r.Date).SequenceEqual(_quotes.Select(q => q.Date)))
                throw new InvalidOperationException("Native VWMA dates differ.");
            values = rolling.Select(r => r.Vwma).ToArray();
        }
        else if (!Rolling && result is IReadOnlyList<VwapResult> cumulative)
        {
            if (!cumulative.Select(r => r.Date).SequenceEqual(_quotes.Select(q => q.Date)))
                throw new InvalidOperationException("Native VWAP dates differ.");
            values = cumulative.Select(r => r.Vwap).ToArray();
        }
        else throw new InvalidOperationException("Native weighted price must retain its eager result.");
        if (values.Length != Count) throw new InvalidOperationException("Native weighted price count differs.");
        for (int i = 0; i < Count; i++)
            if (values[i].HasValue != expected.Present![i] || values[i].HasValue
                && BitConverter.DoubleToInt64Bits(values[i]!.Value) != BitConverter.DoubleToInt64Bits(expected.Values[i]))
                throw new InvalidOperationException($"Native weighted price differs at {i}.");
    }
}

using AiDotNet.Tensors.Helpers;
using BenchmarkDotNet.Attributes;
using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Indicators;
using Skender.Stock.Indicators;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

[MemoryDiagnoser, Config(typeof(TensorsGpuTimingConfig))]
public class SharedMoneyFlowIndexBenchmarks
{
    private const int Period = 14;
    [Params("TaLib.Functions.Mfi", "Skender.GetMfi")] public string Operation { get; set; } = "TaLib.Functions.Mfi";
    [Params(1_000, 10_000)] public int Count { get; set; }
    private Bar[] _bars = null!;
    private double[] _highs = null!, _lows = null!, _closes = null!, _volumes = null!;
    private Quote[] _quotes = null!;
    private Quote[][] _segments = null!;
    private int _chunks;
    private bool IsTa => Operation == "TaLib.Functions.Mfi";
    private int _priorDop;
    private int _priorUnstable;
    private bool _settingsSaved;

    [GlobalSetup]
    public void Setup()
    {
        if (Operation is not ("TaLib.Functions.Mfi" or "Skender.GetMfi")) throw new ArgumentException("Unsupported money-flow operation.");
        if (Count <= Period) throw new ArgumentOutOfRangeException(nameof(Count));
        _priorDop = CpuParallelSettings.MaxDegreeOfParallelism;
        _priorUnstable = TALib.Core.UnstablePeriodSettings.Get(TALib.Core.UnstableFunc.Mfi);
        _settingsSaved = true;
        try
        {
            TALib.Core.UnstablePeriodSettings.Set(TALib.Core.UnstableFunc.Mfi, 0);
            CpuParallelSettings.MaxDegreeOfParallelism = 8;
            _closes = Enumerable.Range(0, Count).Select(i => 10 + (i * 13 % 101) / 8d).ToArray();
            _highs = _closes.Select((v, i) => v + 1 + (i % 5) / 16d).ToArray();
            _lows = _closes.Select((v, i) => v - 1 - (i % 7) / 16d).ToArray();
            _volumes = Enumerable.Range(0, Count).Select(i => (double)(i % 13 + 1)).ToArray();
            _bars = _closes.Select((v, i) => new Bar(DateTime.UnixEpoch.AddMinutes(i), v, _highs[i], _lows[i], v, _volumes[i])).ToArray();
            _chunks = Math.Min(Count - Period, Math.Min(8, Environment.ProcessorCount));
            // These dyadic inputs convert to decimal and back exactly. Prepare
            // quotes and overlapping worker slices outside every measured call.
            _quotes = _bars.Select(b => new Quote { Date = b.Time, Open = (decimal)b.Open,
                High = (decimal)b.High, Low = (decimal)b.Low, Close = (decimal)b.Close,
                Volume = (decimal)b.Volume }).ToArray();
            _segments = Enumerable.Range(0, _chunks).Select(chunk =>
            {
                int start = (Count - Period) * chunk / _chunks;
                int end = (Count - Period) * (chunk + 1) / _chunks;
                return _quotes[start..(end + Period)];
            }).ToArray();
            var native = NativeValues(NativeSingle());
            CheckNative(native, NativeValues(NativeParallel()));
            var expected = MoneyFlowIndexComparison.Reference(_bars, Period, IsTa);
            foreach (var history in Enum.GetValues<IndicatorHistoryMode>())
            {
                var indicator = new WindowMoneyFlowIndex(Period, minimumTotalOne: IsTa);
                using var run = Build(indicator, history);
                for (int i = 0; i < Count; i++)
                {
                    if (BitConverter.DoubleToInt64Bits(expected[i] ?? 0) != BitConverter.DoubleToInt64Bits(run[indicator.Value][i])
                        || run[indicator.IsDefined][i] != (expected[i].HasValue ? 1d : 0d))
                        throw new InvalidOperationException($"Exact MFI builder mismatch at {i}.");
                }
                CheckNative(native, run[indicator.Value].Slice(Period).ToArray());
            }
        }
        catch { Cleanup(); throw; }
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        if (!_settingsSaved) return;
        TALib.Core.UnstablePeriodSettings.Set(TALib.Core.UnstableFunc.Mfi, _priorUnstable);
        CpuParallelSettings.MaxDegreeOfParallelism = _priorDop;
        _settingsSaved = false;
    }
    [Benchmark] public IIndicatorRun BuilderFull() => Build(new WindowMoneyFlowIndex(Period, minimumTotalOne: IsTa), IndicatorHistoryMode.Full);
    [Benchmark] public IIndicatorRun BuilderLatestOnly() => Build(new WindowMoneyFlowIndex(Period, minimumTotalOne: IsTa), IndicatorHistoryMode.LatestOnly);
    internal IIndicatorRun Build(IIndicator indicator, IndicatorHistoryMode history, bool gpu = false)
    {
        var builder = new StockIndicatorBuilder().ConfigureSource(Bars.From(_bars))
            .ConfigureIndicators(indicator).ConfigureHistory(history)
            .ConfigureExecution(gpu ? IndicatorExecutionBackend.Gpu : IndicatorExecutionBackend.Cpu);
        var run = builder.BuildAsync().GetAwaiter().GetResult();
        if (gpu && builder.LastExecution?.Backend != IndicatorExecutionBackend.Gpu)
        {
            run.Dispose();
            throw new InvalidOperationException("Required GPU MFI did not execute on the device.");
        }
        return run;
    }

    [Benchmark(Baseline = true)]
    public object NativeSingle()
    {
        // GetMfi eagerly calculates its native List<MfiResult>; retain that
        // result directly rather than charging a normalization adapter.
        if (!IsTa) return _quotes.GetMfi(Period);
        var output = GC.AllocateUninitializedArray<double>(Count - Period);
        Native(0, Count, output);
        return output;
    }

    [Benchmark]
    public object NativeParallel()
    {
        if (!IsTa)
        {
            var results = new IEnumerable<MfiResult>[_chunks];
            CpuParallelSettings.LightweightParallel(_chunks, _chunks, chunk =>
                results[chunk] = _segments[chunk].GetMfi(Period));
            return results;
        }
        var output = GC.AllocateUninitializedArray<double>(Count - Period);
        int chunks = Math.Min(output.Length, Math.Min(8, Environment.ProcessorCount));
        CpuParallelSettings.LightweightParallel(chunks, chunks, chunk =>
        {
            int start = output.Length * chunk / chunks, end = output.Length * (chunk + 1) / chunks;
            Native(start, end - start + Period, output.AsSpan(start, end - start));
        });
        return output;
    }

    private double[] NativeValues(object result)
    {
        if (result is double[] packed) return packed;
        var groups = result is IEnumerable<MfiResult>[] partitions ? partitions
            : new[] { (IEnumerable<MfiResult>)result };
        var values = new List<double>(Count - Period);
        for (int chunk = 0; chunk < groups.Length; chunk++)
        {
            int start = groups.Length == 1 ? 0 : (Count - Period) * chunk / _chunks;
            int end = groups.Length == 1 ? Count - Period : (Count - Period) * (chunk + 1) / _chunks;
            int i = 0;
            foreach (var row in groups[chunk])
            {
                if (i >= end - start + Period || row.Date != _quotes[start + i].Date
                    || row.Mfi.HasValue != (i >= Period))
                    throw new InvalidOperationException("Unexpected native Skender MFI alignment.");
                if (i >= Period) values.Add(row.Mfi!.Value);
                i++;
            }
            if (i != end - start + Period)
                throw new InvalidOperationException("Unexpected native Skender MFI length.");
        }
        return values.ToArray();
    }

    private void Native(int start, int count, Span<double> output)
    {
        var status = TALib.Functions.Mfi<double>(_highs.AsSpan(start, count), _lows.AsSpan(start, count),
            _closes.AsSpan(start, count), _volumes.AsSpan(start, count), System.Range.All, output, out var range, Period);
        if (status != TALib.Core.RetCode.Success || range.Start.Value != Period
            || range.End.Value - range.Start.Value != output.Length)
            throw new InvalidOperationException("Unexpected native MFI output range.");
    }

    private static void CheckNative(double[] expected, double[] actual)
    {
        if (expected.Length != actual.Length) throw new InvalidOperationException("MFI output length mismatch.");
        for (int i = 0; i < expected.Length; i++)
            if (!double.IsFinite(expected[i]) || !double.IsFinite(actual[i])
                || Math.Abs(expected[i] - actual[i]) > 1e-10 * Math.Max(1, Math.Abs(expected[i])))
                throw new InvalidOperationException($"Native MFI mismatch at {i}.");
    }
}


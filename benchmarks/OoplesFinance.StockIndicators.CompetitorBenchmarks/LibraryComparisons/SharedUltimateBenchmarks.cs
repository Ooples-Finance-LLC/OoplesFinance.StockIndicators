using AiDotNet.Tensors.Helpers;
using BenchmarkDotNet.Attributes;
using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Indicators;
using Skender.Stock.Indicators;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

[MemoryDiagnoser, Config(typeof(TensorsGpuTimingConfig))]
public class SharedUltimateBenchmarks
{
    private const int Period = 28;
    [Params("TaLib.Functions.UltOsc", "Skender.GetUltimate")] public string Operation { get; set; } = "TaLib.Functions.UltOsc";
    [Params(1_000, 10_000)] public int Count { get; set; }
    private Bar[] _bars = null!;
    private double[] _highs = null!, _lows = null!, _closes = null!, _volumes = null!;
    private Quote[] _quotes = null!;
    private Quote[][] _segments = null!;
    private int _chunks;
    private bool IsTa => Operation == "TaLib.Functions.UltOsc";
    private int _priorDop;
    private bool _settingsSaved;

    [GlobalSetup]
    public void Setup()
    {
        if (Operation is not ("TaLib.Functions.UltOsc" or "Skender.GetUltimate")) throw new ArgumentException("Unsupported ultimate-oscillator operation.");
        if (Count <= Period) throw new ArgumentOutOfRangeException(nameof(Count));
        _priorDop = CpuParallelSettings.MaxDegreeOfParallelism;
        _settingsSaved = true;
        try
        {
            CpuParallelSettings.MaxDegreeOfParallelism = 8;
            _closes = Enumerable.Range(0, Count).Select(i => 10 + (i * 13 % 101) / 8d).ToArray();
            _highs = _closes.Select((v, i) => v + 1 + (i % 5) / 16d).ToArray();
            _lows = _closes.Select((v, i) => v - 1 - (i % 7) / 16d).ToArray();
            _volumes = Enumerable.Range(0, Count).Select(i => (double)(i % 13 + 1)).ToArray();
            _bars = _closes.Select((v, i) => new Bar(DateTime.UnixEpoch.AddMinutes(i), v, _highs[i], _lows[i], v, _volumes[i])).ToArray();
            _chunks = Math.Min(Count - Period, Math.Min(8, Environment.ProcessorCount));
            // These dyadic inputs convert to decimal and back exactly. Prepare
            // quotes and overlapping worker slices outside every measured call.
            _quotes = SharedBenchmarkSupport.ToQuotes(_bars);
            _segments = SharedBenchmarkSupport.OverlappingQuotes(_quotes, Count, Period, _chunks);
            var native = NativeValues(NativeSingle());
            SharedBenchmarkSupport.RequireNativeTolerance(native, NativeValues(NativeParallel()));
            var expected = TrueRangeRatioComparison.UltimateReference(_bars, [7, 14, Period], IsTa, false);
            foreach (var history in Enum.GetValues<IndicatorHistoryMode>())
            {
                var indicator = new WindowUltimateOscillator(7, 14, Period, zeroRangeContribution: IsTa);
                using var run = Build(indicator, history);
                SharedBenchmarkSupport.VerifyNullableColumn(expected, run, indicator.Value, indicator.IsDefined, Count);
                SharedBenchmarkSupport.RequireNativeTolerance(native, run[indicator.Value].Slice(Period).ToArray());
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
    [Benchmark] public IIndicatorRun BuilderFull() => Build(new WindowUltimateOscillator(7, 14, Period, zeroRangeContribution: IsTa), IndicatorHistoryMode.Full);
    [Benchmark] public IIndicatorRun BuilderLatestOnly() => Build(new WindowUltimateOscillator(7, 14, Period, zeroRangeContribution: IsTa), IndicatorHistoryMode.LatestOnly);
    internal IIndicatorRun Build(IIndicator indicator, IndicatorHistoryMode history, bool gpu = false)
    {
        var builder = new StockIndicatorBuilder().ConfigureSource(Bars.From(_bars))
            .ConfigureIndicators(indicator).ConfigureHistory(history)
            .ConfigureExecution(gpu ? IndicatorExecutionBackend.Gpu : IndicatorExecutionBackend.Cpu);
        return SharedBenchmarkSupport.BuildChecked(builder, gpu, "Required GPU Ultimate did not execute on the device.");
    }

    [Benchmark(Baseline = true)]
    public object NativeSingle()
    {
        // GetUltimate eagerly calculates its native List<UltimateResult>; retain that
        // result directly rather than charging a normalization adapter.
        if (!IsTa) return _quotes.GetUltimate(7, 14, Period);
        var output = GC.AllocateUninitializedArray<double>(Count - Period);
        Native(0, Count, output);
        return output;
    }

    [Benchmark]
    public object NativeParallel()
    {
        if (!IsTa)
        {
            var results = new IEnumerable<UltimateResult>[_chunks];
            CpuParallelSettings.LightweightParallel(_chunks, _chunks, chunk =>
                results[chunk] = _segments[chunk].GetUltimate(7, 14, Period));
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
        var groups = result is IEnumerable<UltimateResult>[] partitions ? partitions
            : new[] { (IEnumerable<UltimateResult>)result };
        var values = new List<double>(Count - Period);
        for (int chunk = 0; chunk < groups.Length; chunk++)
        {
            int start = groups.Length == 1 ? 0 : (Count - Period) * chunk / _chunks;
            int end = groups.Length == 1 ? Count - Period : (Count - Period) * (chunk + 1) / _chunks;
            int i = 0;
            foreach (var row in groups[chunk])
            {
                if (i >= end - start + Period || row.Date != _quotes[start + i].Date
                    || row.Ultimate.HasValue != (i >= Period))
                    throw new InvalidOperationException("Unexpected native Skender Ultimate alignment.");
                if (i >= Period) values.Add(row.Ultimate!.Value);
                i++;
            }
            if (i != end - start + Period)
                throw new InvalidOperationException("Unexpected native Skender Ultimate length.");
        }
        return values.ToArray();
    }

    private void Native(int start, int count, Span<double> output)
    {
        var status = TALib.Functions.UltOsc<double>(_highs.AsSpan(start, count), _lows.AsSpan(start, count),
            _closes.AsSpan(start, count), System.Range.All, output, out var range, 7, 14, Period);
        if (status != TALib.Core.RetCode.Success || range.Start.Value != Period
            || range.End.Value - range.Start.Value != output.Length)
            throw new InvalidOperationException("Unexpected native Ultimate output range.");
    }

}


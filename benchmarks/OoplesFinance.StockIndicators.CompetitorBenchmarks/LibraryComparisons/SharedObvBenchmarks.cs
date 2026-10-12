using AiDotNet.Tensors.Helpers;
using BenchmarkDotNet.Attributes;
using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

[MemoryDiagnoser, Config(typeof(TensorsGpuTimingConfig))]
public class SharedObvBenchmarks
{
    [Params("TaLib.Functions.Obv")] public string Operation { get; set; } = "TaLib.Functions.Obv";
    [Params(1_000, 10_000)] public int Count { get; set; }
    private Bar[] _bars = null!;
    private double[] _closes = null!, _volumes = null!;
    private int _chunks, _priorDop;
    private bool _settingsSaved;

    [GlobalSetup]
    public void Setup()
    {
        if (Operation != "TaLib.Functions.Obv") throw new ArgumentException("Unsupported OBV operation.");
        if (Count < 2) throw new ArgumentOutOfRangeException(nameof(Count));
        _priorDop = CpuParallelSettings.MaxDegreeOfParallelism;
        _settingsSaved = true;
        try
        {
            CpuParallelSettings.MaxDegreeOfParallelism = 8;
            // Integer volumes make every prefix and parallel offset exact.
            // No prepared prefix, output, or offset is reused by measured calls.
            _closes = Enumerable.Range(0, Count).Select(i => 10 + ((i / 2) * 13 % 101) / 8d).ToArray();
            _volumes = Enumerable.Range(0, Count).Select(i => (double)(i % 13 + 1)).ToArray();
            _bars = _closes.Select((v, i) => new Bar(DateTime.UnixEpoch.AddMinutes(i), v, v + 1, v - 1, v, _volumes[i])).ToArray();
            _chunks = Math.Max(1, Math.Min(Count / 2, Math.Min(8, Environment.ProcessorCount)));
            var serial = NativeSingle();
            Check(serial, NativeParallel());
            var indicator = new SeededOnBalanceVolume();
            using var reference = new StockIndicatorBuilder().ConfigureSource(Bars.From(_bars))
                .ConfigureIndicators(indicator).ConfigureBehavior(_ => { }).BuildAsync().GetAwaiter().GetResult();
            foreach (var history in Enum.GetValues<IndicatorHistoryMode>())
            {
                using var run = Build(indicator, history);
                Check(serial, run[indicator.Value].ToArray());
                foreach (var output in indicator.Outputs) Check(reference[output].ToArray(), run[output].ToArray());
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

    [Benchmark] public IIndicatorRun BuilderFull() => Build(new SeededOnBalanceVolume(), IndicatorHistoryMode.Full);
    [Benchmark] public IIndicatorRun BuilderLatestOnly() => Build(new SeededOnBalanceVolume(), IndicatorHistoryMode.LatestOnly);
    internal IIndicatorRun Build(IIndicator indicator, IndicatorHistoryMode history, bool gpu = false)
    {
        var builder = new StockIndicatorBuilder().ConfigureSource(Bars.From(_bars)).ConfigureIndicators(indicator)
            .ConfigureHistory(history).ConfigureExecution(gpu ? IndicatorExecutionBackend.Gpu : IndicatorExecutionBackend.Cpu);
        var run = builder.BuildAsync().GetAwaiter().GetResult();
        if (gpu && builder.LastExecution?.Backend != IndicatorExecutionBackend.Gpu)
        {
            run.Dispose();
            throw new InvalidOperationException("OBV benchmark did not execute on GPU.");
        }
        return run;
    }

    [Benchmark(Baseline = true)]
    public double[] NativeSingle()
    {
        var result = GC.AllocateUninitializedArray<double>(Count);
        Native(0, Count, result);
        return result;
    }

    [Benchmark]
    public double[] NativeParallel()
    {
        var result = GC.AllocateUninitializedArray<double>(Count);
        var offsets = new double[_chunks];
        CpuParallelSettings.LightweightParallel(_chunks, _chunks, chunk =>
        {
            int start = Count * chunk / _chunks, end = Count * (chunk + 1) / _chunks;
            Native(start, end - start, result.AsSpan(start, end - start));
        });
        double carry = result[Count / _chunks - 1];
        for (int chunk = 1; chunk < _chunks; chunk++)
        {
            int start = Count * chunk / _chunks, end = Count * (chunk + 1) / _chunks;
            double first = _closes[start] > _closes[start - 1] ? _volumes[start]
                : _closes[start] < _closes[start - 1] ? -_volumes[start] : 0;
            offsets[chunk] = carry + first - _volumes[start];
            carry = result[end - 1] + offsets[chunk];
        }
        CpuParallelSettings.LightweightParallel(_chunks, _chunks, chunk =>
        {
            if (chunk == 0) return;
            int start = Count * chunk / _chunks, end = Count * (chunk + 1) / _chunks;
            for (int i = start; i < end; i++) result[i] += offsets[chunk];
        });
        return result;
    }

    private void Native(int start, int count, Span<double> result)
    {
        var status = TALib.Functions.Obv<double>(_closes.AsSpan(start, count), _volumes.AsSpan(start, count),
            System.Range.All, result, out var range);
        if (status != TALib.Core.RetCode.Success || range.Start.Value != 0 || range.End.Value != result.Length)
            throw new InvalidOperationException("Unexpected native OBV output range.");
    }

    private static void Check(double[] expected, double[] actual)
    {
        if (expected.Length != actual.Length) throw new InvalidOperationException("OBV output length differs.");
        for (int i = 0; i < expected.Length; i++)
            if (BitConverter.DoubleToInt64Bits(expected[i]) != BitConverter.DoubleToInt64Bits(actual[i]))
                throw new InvalidOperationException($"OBV output differs at {i}.");
    }
}

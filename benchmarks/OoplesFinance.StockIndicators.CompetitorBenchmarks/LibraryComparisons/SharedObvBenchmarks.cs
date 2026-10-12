using AiDotNet.Tensors.Helpers;
using BenchmarkDotNet.Attributes;
using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Indicators;
using Skender.Stock.Indicators;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

[MemoryDiagnoser, Config(typeof(TensorsGpuTimingConfig))]
public class SharedObvBenchmarks
{
    [Params("TaLib.Functions.Obv", "Skender.GetObv", "Trady.Indicator.OnBalanceVolume")]
    public string Operation { get; set; } = "TaLib.Functions.Obv";
    [Params(1_000, 10_000)] public int Count { get; set; }
    private Bar[] _bars = null!;
    private double[] _closes = null!, _volumes = null!;
    private Quote[] _quotes = null!;
    private (decimal Close, decimal Volume)[] _tuples = null!;
    private (decimal Close, decimal Volume)[][] _segments = null!;
    private int _chunks, _priorDop;
    private bool _settingsSaved;
    private bool IsSkender => Operation == "Skender.GetObv";
    private bool IsTrady => Operation == "Trady.Indicator.OnBalanceVolume";
    internal SeededOnBalanceVolume Indicator() => IsSkender ? new(ObvSeed.Zero, 20) : new();

    [GlobalSetup]
    public void Setup()
    {
        if (Operation is not ("TaLib.Functions.Obv" or "Skender.GetObv" or "Trady.Indicator.OnBalanceVolume"))
            throw new ArgumentException("Unsupported OBV operation.");
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
            _quotes = _bars.Select(b => new Quote { Date = b.Time, Open = (decimal)b.Open,
                High = (decimal)b.High, Low = (decimal)b.Low, Close = (decimal)b.Close, Volume = (decimal)b.Volume }).ToArray();
            _tuples = _bars.Select(b => ((decimal)b.Close, (decimal)b.Volume)).ToArray();
            _segments = Enumerable.Range(0, _chunks).Select(chunk =>
                _tuples[(Count * chunk / _chunks)..(Count * (chunk + 1) / _chunks)]).ToArray();
            var serial = NativeColumns(NativeSingle());
            var parallel = NativeColumns(NativeParallel());
            for (int slot = 0; slot < 3; slot++) Check(serial[slot], parallel[slot]);
            var indicator = Indicator();
            using var reference = new StockIndicatorBuilder().ConfigureSource(Bars.From(_bars))
                .ConfigureIndicators(indicator).ConfigureBehavior(_ => { }).BuildAsync().GetAwaiter().GetResult();
            foreach (var history in Enum.GetValues<IndicatorHistoryMode>())
            {
                using var run = Build(indicator, history);
                for (int slot = 0; slot < 3; slot++)
                {
                    var actual = run[indicator.Outputs[slot]].ToArray();
                    Check(serial[slot], actual);
                    Check(reference[indicator.Outputs[slot]].ToArray(), actual);
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

    [Benchmark] public IIndicatorRun BuilderFull() => Build(Indicator(), IndicatorHistoryMode.Full);
    [Benchmark] public IIndicatorRun BuilderLatestOnly() => Build(Indicator(), IndicatorHistoryMode.LatestOnly);
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
    public object NativeSingle()
    {
        // Retain the eagerly calculated native collection without an adapter copy.
        if (IsSkender) return _quotes.GetObv(20);
        if (IsTrady) return new Trady.Analysis.Indicator.OnBalanceVolumeByTuple(_tuples).Compute();
        var result = GC.AllocateUninitializedArray<double>(Count);
        Native(0, Count, result);
        return result;
    }

    [Benchmark]
    public object NativeParallel()
    {
        if (IsSkender)
        {
            var rows = new ObvResult[Count];
            CpuParallelSettings.LightweightParallel(_chunks, _chunks, chunk =>
            {
                int start = Count * chunk / _chunks, end = Count * (chunk + 1) / _chunks;
                // Full-prefix replay preserves the native average's rounding and
                // startup. Recomputed prefixes and publication are measured.
                int index = 0;
                foreach (var row in _quotes.Take(end).GetObv(20))
                {
                    if (index >= start) rows[index] = row;
                    index++;
                }
            });
            return rows;
        }
        if (IsTrady) return NativeTradyParallel();
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

    private decimal?[] NativeTradyParallel()
    {
        var result = new decimal?[Count];
        var offsets = new decimal[_chunks];
        CpuParallelSettings.LightweightParallel(_chunks, _chunks, chunk =>
        {
            int start = Count * chunk / _chunks;
            var rows = new Trady.Analysis.Indicator.OnBalanceVolumeByTuple(_segments[chunk]).Compute();
            for (int i = 0; i < rows.Count; i++) result[start + i] = rows[i];
        });
        decimal carry = result[Count / _chunks - 1]!.Value;
        for (int chunk = 1; chunk < _chunks; chunk++)
        {
            int start = Count * chunk / _chunks, end = Count * (chunk + 1) / _chunks;
            decimal first = _tuples[start].Close > _tuples[start - 1].Close ? _tuples[start].Volume
                : _tuples[start].Close < _tuples[start - 1].Close ? -_tuples[start].Volume : 0;
            offsets[chunk] = carry + first - _tuples[start].Volume;
            carry = result[end - 1]!.Value + offsets[chunk];
        }
        CpuParallelSettings.LightweightParallel(_chunks, _chunks, chunk =>
        {
            if (chunk == 0) return;
            int start = Count * chunk / _chunks, end = Count * (chunk + 1) / _chunks;
            for (int i = start; i < end; i++) result[i] += offsets[chunk];
        });
        return result;
    }

    // Only setup normalizes native results. Timed methods retain native collections.
    private double[][] NativeColumns(object result)
    {
        var columns = new[] { new double[Count], new double[Count], new double[Count] };
        if (IsSkender)
        {
            // A deferred iterator would make the object-returning benchmark invalid.
            if (result is not IReadOnlyList<ObvResult> rows || rows.Count != Count)
                throw new InvalidOperationException("Native Skender OBV must return a complete eager collection.");
            for (int i = 0; i < Count; i++)
            {
                if (rows[i].Date != _quotes[i].Date || rows[i].ObvSma.HasValue != (i >= 20))
                    throw new InvalidOperationException("Native Skender OBV alignment differs.");
                columns[0][i] = rows[i].Obv;
                columns[1][i] = rows[i].ObvSma ?? 0;
                columns[2][i] = rows[i].ObvSma.HasValue ? 1 : 0;
            }
        }
        else if (IsTrady)
        {
            var rows = (IReadOnlyList<decimal?>)result;
            if (rows.Count != Count) throw new InvalidOperationException("Native Trady OBV length differs.");
            for (int i = 0; i < Count; i++)
                columns[0][i] = (double)(rows[i] ?? throw new InvalidOperationException("Missing Trady OBV value."));
        }
        else columns[0] = (double[])result;
        return columns;
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

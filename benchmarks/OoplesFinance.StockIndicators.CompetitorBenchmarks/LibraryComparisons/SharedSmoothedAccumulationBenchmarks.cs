using AiDotNet.Tensors.Helpers;
using BenchmarkDotNet.Attributes;
using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Indicators;
using Skender.Stock.Indicators;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

[MemoryDiagnoser, Config(typeof(TensorsGpuTimingConfig))]
public class SharedSmoothedAccumulationBenchmarks
{
    private const int Fast = 3, Slow = 10, Lookback = Slow - 1;
    [Params("Skender.GetChaikinOsc", "TaLib.Functions.AdOsc")]
    public string Operation { get; set; } = "Skender.GetChaikinOsc";
    [Params(1_000, 10_000)] public int Count { get; set; }
    private Bar[] _bars = null!;
    private Quote[] _quotes = null!;
    private double[] _highs = null!, _lows = null!, _closes = null!, _volumes = null!;
    private int _chunks, _priorDop;
    private TripleRateComparison.Settings? _settings;
    private bool _settingsSaved;
    private bool IsTa => Operation == "TaLib.Functions.AdOsc";

    [GlobalSetup]
    public void Setup()
    {
        if (Operation is not ("Skender.GetChaikinOsc" or "TaLib.Functions.AdOsc"))
            throw new ArgumentException("Unsupported Chaikin oscillator operation.");
        if (Count < 1) throw new ArgumentOutOfRangeException(nameof(Count));
        _priorDop = CpuParallelSettings.MaxDegreeOfParallelism;
        _settingsSaved = true;
        try
        {
            _settings = new TripleRateComparison.Settings(false, 0);
            CpuParallelSettings.MaxDegreeOfParallelism = 8;
            // Dyadic candles convert exactly to native decimal quotes. Flat
            // ranges and signed flows exercise accumulation and both EMA seeds.
            _lows = Enumerable.Range(0, Count).Select(i => 10 + (i * 13 % 101) / 8d).ToArray();
            var ranges = Enumerable.Range(0, Count).Select(i => i % 31 == 0 ? 0d : 1 << (i % 3)).ToArray();
            _highs = _lows.Select((v, i) => v + ranges[i]).ToArray();
            _closes = _lows.Select((v, i) => v + ranges[i] * (i % 9) / 8).ToArray();
            _volumes = Enumerable.Range(0, Count).Select(i => (double)(i % 13 + 1)).ToArray();
            var data = CompetitorData.FromOhlcv(_lows, _highs, _lows, _closes, _volumes);
            _bars = data.IndicatorBars;
            _quotes = data.Quotes.ToArray();
            _chunks = Math.Min(Count, Math.Min(8, Environment.ProcessorCount));
            var pair = ComparisonPairs.Get(Operation);
            var expected = pair.Reference!(data, Slow);
            var nativeExpected = pair.CompetitorReference!(data, Slow);
            CheckNative(NativeSingle(), nativeExpected);
            CheckNative(NativeParallel(), nativeExpected);
            foreach (var history in Enum.GetValues<IndicatorHistoryMode>())
            {
                var indicator = Indicator();
                using var run = Build(indicator, history);
                for (int slot = 0; slot < 4; slot++)
                {
                    var column = IsTa && slot > 0 ? null : expected.Outputs[SmoothedAccumulationComparison.Names[slot]];
                    var native = IsTa && slot > 0 ? null : nativeExpected.Outputs[SmoothedAccumulationComparison.Names[slot]];
                    if (run[indicator.Outputs[slot]].Length != Count || run[indicator.Outputs[slot + 4]].Length != Count)
                        throw new InvalidOperationException("Chaikin builder column length differs.");
                    for (int i = 0; i < Count; i++)
                    {
                        bool present = column?.Present![i] ?? false;
                        double value = present ? column!.Values[i] : 0;
                        Exact(value, run[indicator.Outputs[slot]][i]);
                        Exact(present ? 1 : 0, run[indicator.Outputs[slot + 4]][i]);
                        if (native is not null && (present != native.Present![i]
                            || present && Math.Abs(value - native.Values[i]) > 1e-10 * Math.Max(1, Math.Abs(value))))
                            throw new InvalidOperationException($"Native/exact Chaikin disagreement at {i}, column {slot}.");
                    }
                }
            }
        }
        catch { Cleanup(); throw; }
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        if (!_settingsSaved) return;
        _settings?.Dispose();
        _settings = null;
        CpuParallelSettings.MaxDegreeOfParallelism = _priorDop;
        _settingsSaved = false;
    }

    [Benchmark] public IIndicatorRun BuilderFull() => Build(Indicator(), IndicatorHistoryMode.Full);
    [Benchmark] public IIndicatorRun BuilderLatestOnly() => Build(Indicator(), IndicatorHistoryMode.LatestOnly);
    [Benchmark(Baseline = true)]
    public object NativeSingle() => IsTa ? Native(Count) : _quotes.GetChaikinOsc(Fast, Slow);

    [Benchmark]
    public object NativeParallel()
    {
        if (IsTa)
        {
            var output = new double[Math.Max(0, Count - Lookback)];
            int chunks = Math.Min(_chunks, output.Length);
            if (chunks == 0) return output;
            CpuParallelSettings.LightweightParallel(chunks, chunks, chunk =>
            {
                int start = output.Length * chunk / chunks, end = output.Length * (chunk + 1) / chunks;
                // A range starting at this partition would reseed the native
                // cumulative line and EMAs. Replay the complete prefix instead.
                var prefix = Native(end + Lookback);
                prefix.AsSpan(start, end - start).CopyTo(output.AsSpan(start));
            });
            return output;
        }
        var rows = new ChaikinOscResult[Count];
        CpuParallelSettings.LightweightParallel(_chunks, _chunks, chunk =>
        {
            int start = Count * chunk / _chunks, end = Count * (chunk + 1) / _chunks;
            int index = 0;
            foreach (var row in _quotes.Take(end).GetChaikinOsc(Fast, Slow))
            {
                if (index >= start) rows[index] = row;
                index++;
            }
        });
        return rows;
    }

    private SmoothedAccumulationOscillator Indicator() => new(Fast, Slow, IsTa, includeDetails: !IsTa);
    private IIndicatorRun Build(SmoothedAccumulationOscillator indicator, IndicatorHistoryMode history) =>
        SharedBenchmarkSupport.BuildCpu(_bars, indicator, history);

    private double[] Native(int count)
    {
        var output = new double[Math.Max(0, count - Lookback)];
        if (output.Length == 0) return output; // NOSONAR: S2583 - Public Count includes startup cases 1 and 9 in fixture tests; output can be empty.
        var code = TALib.Functions.AdOsc<double>(_highs.AsSpan(0, count), _lows.AsSpan(0, count),
            _closes.AsSpan(0, count), _volumes.AsSpan(0, count), System.Range.All, output, out var range, Fast, Slow);
        if (code != TALib.Core.RetCode.Success || range.Start.Value != Lookback || range.End.Value != count)
            throw new InvalidOperationException("Native Chaikin output range differs.");
        return output;
    }

    private void CheckNative(object result, ComparisonSeries expected)
    {
        if (IsTa)
        {
            if (result is not double[] values || values.Length != Math.Max(0, Count - Lookback))
                throw new InvalidOperationException("Native TA Chaikin must retain its packed output.");
            var column = expected.Outputs["Oscillator"];
            for (int i = 0; i < Count; i++)
            {
                if (column.Present![i] != (i >= Lookback)) throw new InvalidOperationException("TA Chaikin presence differs.");
                if (i >= Lookback) Exact(column.Values[i], values[i - Lookback]);
            }
            return;
        }
        if (result is not IReadOnlyList<ChaikinOscResult> rows || rows.Count != Count)
            throw new InvalidOperationException("Native Skender Chaikin must retain its eager result.");
        for (int i = 0; i < Count; i++)
        {
            if (rows[i].Date != _quotes[i].Date) throw new InvalidOperationException("Chaikin quote dates differ.");
            double?[] values = [rows[i].Oscillator, rows[i].MoneyFlowMultiplier, rows[i].MoneyFlowVolume, rows[i].Adl];
            for (int slot = 0; slot < 4; slot++)
            {
                var column = expected.Outputs[SmoothedAccumulationComparison.Names[slot]];
                if (column.Present![i] != values[slot].HasValue) throw new InvalidOperationException("Chaikin native presence differs.");
                if (values[slot].HasValue) Exact(column.Values[i], values[slot]!.Value);
            }
        }
    }

    private static void Exact(double expected, double actual)
    {
        if (BitConverter.DoubleToInt64Bits(expected) != BitConverter.DoubleToInt64Bits(actual))
            throw new InvalidOperationException("Chaikin arithmetic differs from its independent reference.");
    }
}

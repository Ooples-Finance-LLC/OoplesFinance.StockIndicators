using AiDotNet.Tensors.Helpers;
using BenchmarkDotNet.Attributes;
using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Indicators;
using Skender.Stock.Indicators;
using TALib;
using TI = Trady.Analysis.Indicator;
using AroonResult = Skender.Stock.Indicators.AroonResult;
using Prices = (decimal High, decimal Low);
using Pair = (decimal? Up, decimal? Down);

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

[MemoryDiagnoser, Config(typeof(TensorsGpuTimingConfig))]
public class SharedAroonBenchmarks
{
    private const int Period = 20;
    [Params("TaLib.Functions.Aroon", "TaLib.Functions.AroonOsc", "Skender.GetAroon", "Trady.Indicator.Aroon", "Trady.Indicator.AroonOscillator")]
    public string Operation { get; set; } = "";
    [Params(1_000, 10_000)] public int Count { get; set; }
    private Bar[] _bars = null!;
    private double[] _high = null!, _low = null!;
    private Quote[] _quotes = null!;
    private Prices[] _tuples = null!;
    private Quote[][] _quoteChunks = null!;
    private Prices[][] _tupleChunks = null!;
    private int[] _skip = null!;
    private int _priorDop, _chunks;
    private bool Trady => Operation.StartsWith("Trady.", StringComparison.Ordinal);
    private bool Skender => Operation == "Skender.GetAroon";
    private bool Oscillator => Operation.EndsWith("Osc", StringComparison.Ordinal) || Operation.EndsWith("Oscillator", StringComparison.Ordinal);

    [GlobalSetup]
    public void Setup()
    {
        _priorDop = CpuParallelSettings.MaxDegreeOfParallelism;
        CpuParallelSettings.MaxDegreeOfParallelism = 8;
        _bars = Enumerable.Range(0, Count).Select(i =>
        {
            double close = 100 + (i * 13 % 101) / 8d;
            return new Bar(DateTime.UnixEpoch.AddDays(i), close, close + (i % 7 + 1) / 8d, close - (i % 5 + 1) / 8d, close, 1);
        }).ToArray();
        _high = _bars.Select(b => b.High).ToArray(); _low = _bars.Select(b => b.Low).ToArray();
        _quotes = _bars.Select(b => new Quote { Date = b.Time, Open = (decimal)b.Open, High = (decimal)b.High, Low = (decimal)b.Low, Close = (decimal)b.Close, Volume = 1 }).ToArray();
        _tuples = _bars.Select(b => ((decimal)b.High, (decimal)b.Low)).ToArray();
        _chunks = Math.Min(8, Environment.ProcessorCount);
        _quoteChunks = new Quote[_chunks][]; _tupleChunks = new Prices[_chunks][]; _skip = new int[_chunks];
        for (int chunk = 0; chunk < _chunks; chunk++)
        {
            int start = Count * chunk / _chunks, end = Count * (chunk + 1) / _chunks;
            int seed = Math.Max(0, start - Period + (Trady ? 1 : 0));
            _skip[chunk] = start - seed;
            // IEnumerable-native input preparation is outside timing, including worker slices.
            _quoteChunks[chunk] = _quotes[seed..end]; _tupleChunks[chunk] = _tuples[seed..end];
        }
        var expected = Scan();
        var serial = NativeSingle(); var parallel = NativeParallel();
        Presence(serial, expected[3]); Presence(parallel, expected[3]);
        int[] slots = Oscillator ? [2] : Skender ? [0, 1, 2] : [0, 1];
        foreach (int slot in slots)
        {
            AsinFeasibilityBenchmarks.RequireSame(expected[slot], Values(serial, slot));
            AsinFeasibilityBenchmarks.RequireSame(Values(serial, slot), Values(parallel, slot));
        }
        foreach (var history in Enum.GetValues<IndicatorHistoryMode>())
        {
            var indicator = AroonComparison.Create(Operation, Period);
            using var run = Build(indicator, history);
            for (int slot = 0; slot < 6; slot++)
                AsinFeasibilityBenchmarks.RequireSame(expected[slot], run[indicator.Outputs[slot]].ToArray());
        }
    }
    [GlobalCleanup] public void Cleanup() => CpuParallelSettings.MaxDegreeOfParallelism = _priorDop;
    [Benchmark] public IIndicatorRun BuilderFull() => Build(AroonComparison.Create(Operation, Period), IndicatorHistoryMode.Full);
    [Benchmark] public IIndicatorRun BuilderLatestOnly() => Build(AroonComparison.Create(Operation, Period), IndicatorHistoryMode.LatestOnly);
    private IIndicatorRun Build(WindowAroon indicator, IndicatorHistoryMode history) => new StockIndicatorBuilder()
        .ConfigureSource(Bars.From(_bars)).ConfigureIndicators(indicator).ConfigureHistory(history).BuildAsync().GetAwaiter().GetResult();

    public readonly record struct NativeResult(double[]? Up = null, double[]? Down = null, double[]? Osc = null,
        IEnumerable<AroonResult>? Quotes = null, IReadOnlyList<Pair>? Pairs = null, IReadOnlyList<decimal?>? Oscillators = null);
    [Benchmark(Baseline = true)] public NativeResult NativeSingle()
    {
        if (Skender) return new(Quotes: _quotes.GetAroon(Period)); // The public API eagerly returns its result list.
        if (Trady) return Oscillator ? new(Oscillators: new TI.AroonOscillatorByTuple(_tuples, Period).Compute())
            : new(Pairs: new TI.AroonByTuple(_tuples, Period).Compute());
        var output = Allocate(); NativeTa(Period, Count, output); return output;
    }
    [Benchmark] public NativeResult NativeParallel()
    {
        var output = Allocate();
        CpuParallelSettings.LightweightParallel(_chunks, _chunks, chunk =>
        {
            int start = Count * chunk / _chunks, end = Count * (chunk + 1) / _chunks;
            if (Skender)
            {
                var destination = (AroonResult[])output.Quotes!;
                int i = -_skip[chunk];
                foreach (var row in _quoteChunks[chunk].GetAroon(Period))
                {
                    if (i >= 0) destination[start + i] = row;
                    i++;
                }
            }
            else if (Trady)
            {
                if (Oscillator) Copy(new TI.AroonOscillatorByTuple(_tupleChunks[chunk], Period).Compute(startIndex: _skip[chunk]), (decimal?[])output.Oscillators!, start);
                else Copy(new TI.AroonByTuple(_tupleChunks[chunk], Period).Compute(startIndex: _skip[chunk]), (Pair[])output.Pairs!, start);
            }
            else NativeTa(Math.Max(Period, start), end, output);
        });
        return output;
    }
    private NativeResult Allocate() => Skender ? new(Quotes: new AroonResult[Count])
        : Trady ? Oscillator ? new(Oscillators: new decimal?[Count]) : new(Pairs: new Pair[Count])
        : Oscillator ? new(Osc: new double[Count - Period]) : new(Up: new double[Count - Period], Down: new double[Count - Period]);
    private static void Copy<T>(IReadOnlyList<T> from, T[] to, int start)
    {
        for (int i = 0; i < from.Count; i++) to[start + i] = from[i];
    }
    private void NativeTa(int start, int end, NativeResult output)
    {
        System.Range range;
        var input = new System.Range(start, end - 1); // TA-Lib input end is inclusive.
        int at = start - Period, length = end - start;
        var code = Oscillator ? Functions.AroonOsc<double>(_high, _low, input, output.Osc.AsSpan(at, length), out range, Period)
            : Functions.Aroon<double>(_high, _low, input, output.Down.AsSpan(at, length), output.Up.AsSpan(at, length), out range, Period);
        if (code != TALib.Core.RetCode.Success || range.Start.Value != start || range.End.Value != end)
            throw new InvalidOperationException("Unexpected Aroon native range.");
    }
    private double[] Values(NativeResult output, int slot)
    {
        if (output.Quotes is not null) return output.Quotes.Select(r => (slot == 0 ? r.AroonUp : slot == 1 ? r.AroonDown : r.Oscillator) ?? 0).ToArray();
        if (output.Pairs is not null) return output.Pairs.Select(r => (double)((slot == 0 ? r.Up : r.Down) ?? 0m)).ToArray();
        if (output.Oscillators is not null) return output.Oscillators.Select(r => (double)(r ?? 0m)).ToArray();
        var row = slot == 0 ? output.Up! : slot == 1 ? output.Down! : output.Osc!;
        return Enumerable.Repeat(0d, Period).Concat(row).ToArray();
    }
    private static void Presence(NativeResult output, double[] expected)
    {
        if (output.Quotes is not null)
        {
            var rows = output.Quotes.ToArray();
            AsinFeasibilityBenchmarks.RequireSame(expected, rows.Select(r => r.AroonUp.HasValue ? 1d : 0).ToArray());
            AsinFeasibilityBenchmarks.RequireSame(expected, rows.Select(r => r.AroonDown.HasValue ? 1d : 0).ToArray());
            AsinFeasibilityBenchmarks.RequireSame(expected, rows.Select(r => r.Oscillator.HasValue ? 1d : 0).ToArray());
        }
        if (output.Pairs is not null)
        {
            AsinFeasibilityBenchmarks.RequireSame(expected, output.Pairs.Select(r => r.Up.HasValue ? 1d : 0).ToArray());
            AsinFeasibilityBenchmarks.RequireSame(expected, output.Pairs.Select(r => r.Down.HasValue ? 1d : 0).ToArray());
        }
        if (output.Oscillators is not null)
            AsinFeasibilityBenchmarks.RequireSame(expected, output.Oscillators.Select(r => r.HasValue ? 1d : 0).ToArray());
    }
    private double[][] Scan()
    {
        var result = Enumerable.Range(0, 6).Select(_ => new double[Count]).ToArray();
        int size = Period + (Trady ? 0 : 1);
        for (int i = size - 1; i < Count; i++)
        {
            int high = i, low = i;
            for (int j = i - 1; j >= i - size + 1; j--)
            {
                if (Skender ? _high[j] >= _high[high] : _high[j] > _high[high]) high = j;
                if (Skender ? _low[j] <= _low[low] : _low[j] < _low[low]) low = j;
            }
            result[0][i] = (double)(100m * (Period - (i - high)) / Period);
            result[1][i] = (double)(100m * (Period - (i - low)) / Period);
            result[2][i] = (double)(100m * (high - low) / Period);
            result[3][i] = result[4][i] = result[5][i] = 1;
        }
        return result;
    }
}

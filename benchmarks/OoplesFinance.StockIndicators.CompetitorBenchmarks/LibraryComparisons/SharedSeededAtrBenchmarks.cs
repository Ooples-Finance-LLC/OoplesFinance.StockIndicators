using AiDotNet.Tensors.Helpers;
using BenchmarkDotNet.Attributes;
using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Indicators;
using Skender.Stock.Indicators;
using TaCore = TALib.Core;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

[MemoryDiagnoser, Config(typeof(TensorsGpuTimingConfig))]
public class SharedSeededAtrBenchmarks
{
    private const int Period = 20;
    [Params("Skender.GetAtr", "TaLib.Functions.Atr", "TaLib.Functions.Natr", "Trady.Indicator.AverageTrueRange")]
    public string Operation { get; set; } = "";
    [Params(1_000, 10_000)] public int Count { get; set; }
    internal bool UseGpuGrid { get; init; }
    private CompetitorData _data = null!;
    private Quote[][] _quotes = null!;
    private (decimal High, decimal Low, decimal Close)[][] _tuples = null!;
    private int _priorDop, _priorAtr, _priorNatr;
    private bool _settingsSaved;
    private bool Details => Operation == "Skender.GetAtr";
    private bool Normalized => Operation == "TaLib.Functions.Natr";
    private bool Trady => Operation.StartsWith("Trady", StringComparison.Ordinal);

    [GlobalSetup]
    public void Setup()
    {
        if (Operation is not ("Skender.GetAtr" or "TaLib.Functions.Atr" or "TaLib.Functions.Natr" or "Trady.Indicator.AverageTrueRange"))
            throw new ArgumentException("Unsupported seeded ATR operation.", nameof(Operation));
        if (Count <= Period) throw new ArgumentOutOfRangeException(nameof(Count));
        _priorDop = CpuParallelSettings.MaxDegreeOfParallelism;
        _priorAtr = TaCore.UnstablePeriodSettings.Get(TaCore.UnstableFunc.Atr);
        _priorNatr = TaCore.UnstablePeriodSettings.Get(TaCore.UnstableFunc.Natr);
        _settingsSaved = true;
        try
        {
            CpuParallelSettings.MaxDegreeOfParallelism = 8;
            TaCore.UnstablePeriodSettings.Set(TaCore.UnstableFunc.Atr, 0);
            TaCore.UnstablePeriodSettings.Set(TaCore.UnstableFunc.Natr, 0);
            if (UseGpuGrid)
            {
                var closes = Enumerable.Range(0, Count).Select(i => 10 + (i * 13 % 101) / 8d).ToArray();
                var highs = closes.Select((v, i) => v + (i % 3 + 1) / 4d).ToArray();
                var lows = closes.Select(v => v - .5).ToArray();
                _data = CompetitorData.FromOhlcv(closes, highs, lows, closes, Enumerable.Repeat(1d, Count).ToArray());
            }
            else _data = CompetitorData.Create(Count);
            int chunks = Math.Min(Math.Min(8, Environment.ProcessorCount), Count / (Period + 1));
            _quotes = new Quote[chunks][];
            _tuples = new (decimal, decimal, decimal)[chunks][];
            var tuples = _data.Candles.Select(c => (c.High, c.Low, c.Close)).ToArray();
            for (int c = 0; c < chunks; c++)
            {
                int end = Count * (c + 1) / chunks;
                _quotes[c] = _data.Quotes.Take(end).ToArray();
                _tuples[c] = tuples[..end];
            }
            var pair = Normalized ? NormalizedAtrComparison.Pair : SeededAtrComparison.Pairs.Single(p => p.Id == Operation);
            var expected = pair.Reference!(_data, Period);
            var native = pair.CompetitorReference!(_data, Period);
            foreach (var history in Enum.GetValues<IndicatorHistoryMode>())
            {
                var indicator = Create(); using var run = Build(indicator, history);
                var names = Details ? new[] { "Tr", "Atr", "Atrp" } : new[] { "Value" };
                for (int slot = 0; slot < names.Length; slot++)
                {
                    var output = expected.Outputs[names[slot]];
                    var present = Enumerable.Range(0, Count).Select(i => output.Present?[i] ?? i >= output.FirstValid).ToArray();
                    AsinFeasibilityBenchmarks.RequireSame(output.Values.Select((v, i) => present[i] ? v : 0).ToArray(), run[indicator.Outputs[slot]].ToArray());
                    if (Details) AsinFeasibilityBenchmarks.RequireSame(present.Select(v => v ? 1d : 0d).ToArray(), run[indicator.Outputs[slot + 3]].ToArray());
                }
            }
            Check(NativeSingle(), native);
            Check(NativeParallel(), native);
        }
        catch { Cleanup(); throw; }
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        if (!_settingsSaved) return;
        CpuParallelSettings.MaxDegreeOfParallelism = _priorDop;
        TaCore.UnstablePeriodSettings.Set(TaCore.UnstableFunc.Atr, _priorAtr);
        TaCore.UnstablePeriodSettings.Set(TaCore.UnstableFunc.Natr, _priorNatr);
        _settingsSaved = false;
    }
    internal IIndicator Create() => Details ? new AverageTrueRangeWithDetails(Period)
        : Normalized ? new NormalizedSeededAverageTrueRange(Period) : new SeededAverageTrueRange(Period);
    [Benchmark] public IIndicatorRun BuilderFull() => Build(Create(), IndicatorHistoryMode.Full);
    [Benchmark] public IIndicatorRun BuilderLatestOnly() => Build(Create(), IndicatorHistoryMode.LatestOnly);
    internal IIndicatorRun Build(IIndicator indicator, IndicatorHistoryMode history, bool gpu = false)
    {
        var builder = new StockIndicatorBuilder()
            .ConfigureSource(Bars.From(_data.IndicatorBars)).ConfigureIndicators(indicator).ConfigureHistory(history)
            .ConfigureExecution(gpu ? IndicatorExecutionBackend.Gpu : IndicatorExecutionBackend.Cpu);
        return SharedBenchmarkSupport.BuildChecked(builder, gpu, "Required GPU seeded ATR did not execute on the device.");
    }

    public readonly record struct NativeResult(double[]? Packed = null, IReadOnlyList<AtrResult>? Rows = null, IReadOnlyList<decimal?>? Tuples = null);
    [Benchmark(Baseline = true)] public NativeResult NativeSingle() => Prefix(_quotes.Length - 1);
    private NativeResult Prefix(int chunk)
    {
        if (Details) return new(Rows: (IReadOnlyList<AtrResult>)_quotes[chunk].GetAtr(Period));
        if (Trady) return new(Tuples: new Trady.Analysis.Indicator.AverageTrueRangeByTuple(_tuples[chunk], Period).Compute());
        int end = _quotes[chunk].Length;
        var packed = new double[end - Period];
        System.Range range;
        var code = Normalized
            ? TALib.Functions.Natr<double>(_data.Highs, _data.Lows, _data.Closes, 0..(end - 1), packed, out range, Period)
            : TALib.Functions.Atr<double>(_data.Highs, _data.Lows, _data.Closes, 0..(end - 1), packed, out range, Period);
        if (code != TaCore.RetCode.Success || !range.Equals(new System.Range(Period, end)))
            throw new InvalidOperationException("Unexpected native ATR packed output extent.");
        return new(Packed: packed);
    }
    [Benchmark] public NativeResult NativeParallel()
    {
        var result = Details ? new NativeResult(Rows: new AtrResult[Count])
            : Trady ? new NativeResult(Tuples: new decimal?[Count]) : new NativeResult(Packed: new double[Count - Period]);
        int chunks = _quotes.Length;
        CpuParallelSettings.LightweightParallel(chunks, chunks, chunk =>
        {
            // Recursive smoothing must keep the original seed and entire prefix.
            var local = Prefix(chunk);
            int start = Count * chunk / chunks, end = _quotes[chunk].Length;
            if (Details)
                for (int i = start; i < end; i++) ((AtrResult[])result.Rows!)[i] = local.Rows![i];
            else if (Trady)
                for (int i = start; i < end; i++) ((decimal?[])result.Tuples!)[i] = local.Tuples![i];
            else
            {
                start = Math.Max(start, Period);
                local.Packed!.AsSpan(start - Period, end - start).CopyTo(result.Packed.AsSpan(start - Period));
            }
        });
        return result;
    }
    private void Check(NativeResult actual, ComparisonSeries expected)
    {
        if (actual.Packed is not null)
        {
            AsinFeasibilityBenchmarks.RequireSame(expected.Outputs["Value"].Values[Period..], actual.Packed);
            return;
        }
        var names = Details ? new[] { "Tr", "Atr", "Atrp" } : new[] { "Value" };
        for (int slot = 0; slot < names.Length; slot++)
        {
            var values = Details ? actual.Rows!.Select(r => slot == 0 ? r.Tr : slot == 1 ? r.Atr : r.Atrp).ToArray()
                : actual.Tuples!.Select(v => (double?)v).ToArray();
            var reference = expected.Outputs[names[slot]];
            if (values.Length != Count || values.Where((v, i) => v.HasValue != (reference.Present?[i] ?? i >= reference.FirstValid)).Any())
                throw new InvalidOperationException("Native ATR presence differs from its independent reference.");
            AsinFeasibilityBenchmarks.RequireSame(reference.Values.Where((_, i) => values[i].HasValue).ToArray(),
                values.Where(v => v.HasValue).Select(v => v!.Value).ToArray());
        }
    }
}

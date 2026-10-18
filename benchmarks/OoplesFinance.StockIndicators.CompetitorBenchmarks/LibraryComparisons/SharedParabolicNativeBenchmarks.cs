using AiDotNet.Tensors.Helpers;
using BenchmarkDotNet.Attributes;
using Skender.Stock.Indicators;
using TALib;
using Trady.Analysis.Indicator;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

[MemoryDiagnoser, Config(typeof(TensorsGpuTimingConfig))]
public class SharedParabolicNativeBenchmarks
{
    [Params(1000, 10000)] public int Count { get; set; }
    [Params("TaLib.Functions.Sar", "TaLib.Functions.SarExt", "Skender.GetParabolicSar", "Trady.Indicator.ParabolicStopAndReverse")]
    public string Operation { get; set; } = "";
    private SharedParabolicSnapshotBenchmarks _owned = null!;
    private CompetitorData _data = null!;
    private (decimal High, decimal Low)[] _prices = null!;
    private int[] _ends = null!;
    private int _kind, _priorDop;

    [GlobalSetup]
    public void Setup()
    {
        _priorDop = CpuParallelSettings.MaxDegreeOfParallelism;
        ArgumentOutOfRangeException.ThrowIfNegative(Count);
        _kind = Array.IndexOf(ParabolicComparison.Ids, Operation);
        if (_kind < 0) throw new ArgumentException("Unknown parabolic mapping.");
        CpuParallelSettings.MaxDegreeOfParallelism = 8;
        _owned = new SharedParabolicSnapshotBenchmarks { Count = Count, Operation = Operation };
        _owned.Setup();
        var pair = ParabolicComparison.Pair(_kind);
        _data = ComparisonVerifier.BenchmarkFixture(pair, Count);
        _prices = _data.Candles.Select(c => (c.High, c.Low)).ToArray();
        int workers = Math.Min(Count, Math.Min(8, Environment.ProcessorCount));
        _ends = Enumerable.Range(1, workers).Select(i => (int)((long)Count * i / workers)).ToArray();
        var expected = pair.CompetitorReference!(_data, 20);
        foreach (var rows in new[] { NativeSingle(), NativeParallel() })
            ComparisonVerifier.Compare(expected, Normalize(rows), Operation, pair.ErrorBudget);
    }
    [GlobalCleanup] public void Cleanup() => CpuParallelSettings.MaxDegreeOfParallelism = _priorDop;
    [Benchmark] public object Snapshot() => _owned.Snapshot();
    [Benchmark(Baseline = true)] public object NativeSingle() => Prefix(Count);
    private object Prefix(int end)
    {
        if (_kind == 2) return (List<ParabolicSarResult>)_data.Quotes.Take(end).GetParabolicSar(.02, .2, .02);
        if (_kind == 3) return new ParabolicStopAndReverseByTuple(_prices.Take(end), .02m, .2m).Compute();
        var output = new double[Math.Max(0, end - 1)];
        if (end < 2) return output;
        System.Range range;
        var code = _kind == 0
            ? Functions.Sar<double>(_data.Highs.AsSpan(0, end), _data.Lows.AsSpan(0, end), System.Range.All, output, out range, .02, .2)
            : Functions.SarExt<double>(_data.Highs.AsSpan(0, end), _data.Lows.AsSpan(0, end), System.Range.All, output, out range, 0, 0, .02, .02, .2, .02, .02, .2);
        if (code != TALib.Core.RetCode.Success || range.Start.Value != 1 || range.End.Value != end)
            throw new InvalidOperationException("Native SAR packed range differs.");
        return output;
    }
    [Benchmark] public object NativeParallel()
    {
        Array output = _kind switch
        {
            2 => new ParabolicSarResult[Count],
            3 => new decimal?[Count],
            _ => new double[Math.Max(0, Count - 1)]
        };
        if (_ends.Length == 0) return output;
        CpuParallelSettings.LightweightParallel(_ends.Length, _ends.Length, worker =>
        {
            int start = worker == 0 ? 0 : _ends[worker - 1], end = _ends[worker];
            // Replay the full prefix, including Trady's sequential cache transitions.
            // Skender suppresses startup through its first reversal; no future bars are needed.
            var rows = Prefix(end);
            if (_kind == 2)
                ((List<ParabolicSarResult>)rows).CopyTo(start, (ParabolicSarResult[])output, start, end - start);
            else if (_kind == 3)
            {
                var values = (IReadOnlyList<decimal?>)rows;
                for (int i = start; i < end; i++) ((decimal?[])output)[i] = values[i];
            }
            else
            {
                start = Math.Max(1, start);
                if (end > start) Array.Copy((double[])rows, start - 1, output, start - 1, end - start);
            }
        });
        return output;
    }
    internal ComparisonSeries Normalize(object source)
    {
        if (_kind == 2)
        {
            var rows = ((IEnumerable<ParabolicSarResult>)source).ToArray();
            if (!rows.Select(r => r.Date).SequenceEqual(_data.Quotes.Select(q => q.Date)))
                throw new InvalidOperationException("Native SAR dates differ.");
            return ParabolicComparison.Series([rows.Select(r => r.Sar).ToArray(),
                rows.Select(r => r.IsReversal.HasValue ? (double?)(r.IsReversal.Value ? 1 : 0) : null).ToArray()], _kind);
        }
        if (_kind == 3) return ParabolicComparison.Series([((IEnumerable<decimal?>)source).Select(v => (double?)v).ToArray()], _kind);
        var packed = (double[])source;
        var values = new double?[Count];
        for (int i = 0; i < packed.Length; i++) values[i + 1] = packed[i];
        return ParabolicComparison.Series([values], _kind);
    }
}

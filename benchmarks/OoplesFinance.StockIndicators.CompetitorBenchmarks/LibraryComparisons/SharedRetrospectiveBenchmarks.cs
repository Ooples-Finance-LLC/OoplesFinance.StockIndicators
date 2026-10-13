using AiDotNet.Tensors.Helpers;
using BenchmarkDotNet.Attributes;
using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Indicators;
using Skender.Stock.Indicators;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

[MemoryDiagnoser, Config(typeof(TensorsGpuTimingConfig))]
public class SharedRetrospectiveBenchmarks
{
    private const int Wing = 20;
    [Params(1_000, 10_000)] public int Count { get; set; }
    private Bar[] _bars = null!;
    private Quote[] _quotes = null!;
    private int _priorDop;

    [GlobalSetup]
    public void Setup()
    {
        _priorDop = CpuParallelSettings.MaxDegreeOfParallelism;
        if (Count < 1) throw new ArgumentOutOfRangeException(nameof(Count));
        CpuParallelSettings.MaxDegreeOfParallelism = 8;
        _bars = Enumerable.Range(0, Count).Select(i =>
        {
            double p = 100 + (i * 13 % 101) / 8d;
            return new Bar(DateTime.UnixEpoch.AddMinutes(i), p, p + 2, p - 1, p, 1);
        }).ToArray();
        _quotes = _bars.Select(b => new Quote { Date = b.Time, Open = (decimal)b.Open,
            High = (decimal)b.High, Low = (decimal)b.Low, Close = (decimal)b.Close, Volume = 1 }).ToArray();
        var expected = new[] { new double[Count], new double[Count], new double[Count], new double[Count] };
        var serial = NativeSingle(); var parallel = NativeParallel();
        // Exhaustive strict window scan is independent of production monotonic deques.
        // All fixture prices are exactly representable in both decimal and binary64.
        for (int i = 0; i < Count; i++)
        {
            bool high = i >= Wing && i < Count - Wing, low = high;
            if (high)
                for (int j = i - Wing; j <= i + Wing; j++)
                    if (j != i)
                    {
                        high &= _bars[i].High > _bars[j].High;
                        low &= _bars[i].Low < _bars[j].Low;
                    }
            expected[0][i] = high ? _bars[i].High : 0; expected[1][i] = low ? _bars[i].Low : 0;
            expected[2][i] = high ? 1 : 0; expected[3][i] = low ? 1 : 0;
            foreach (var row in new[] { serial[i], parallel[i] })
                if (row.Date != _bars[i].Time || row.FractalBear.HasValue != high || row.FractalBull.HasValue != low
                    || high && (double)row.FractalBear!.Value != expected[0][i] // NOSONAR: S1244 - These dyadic fixture prices convert exactly; comparison intentionally rejects any difference.
                    || low && (double)row.FractalBull!.Value != expected[1][i]) // NOSONAR: S1244 - These dyadic fixture prices convert exactly; comparison intentionally rejects any difference.
                    throw new InvalidOperationException($"Native fractal mismatch at {i}.");
        }
        if (Count >= 1000 && (!expected[2].Contains(1d) || !expected[3].Contains(1d)))
            throw new InvalidOperationException("Fixture must exercise present values.");
        foreach (var history in Enum.GetValues<IndicatorHistoryMode>())
        {
            var indicator = new RetrospectiveFractals(Wing, Wing);
            using var run = Build(indicator, history);
            for (int slot = 0; slot < 4; slot++)
                AsinFeasibilityBenchmarks.RequireSame(expected[slot], run[indicator.Outputs[slot]].ToArray());
        }
    }
    [GlobalCleanup] public void Cleanup() => CpuParallelSettings.MaxDegreeOfParallelism = _priorDop;
    [Benchmark] public IIndicatorRun BuilderFull() => Build(new(Wing, Wing), IndicatorHistoryMode.Full);
    [Benchmark] public IIndicatorRun BuilderLatestOnly() => Build(new(Wing, Wing), IndicatorHistoryMode.LatestOnly);
    [Benchmark(Baseline = true)] public List<FractalResult> NativeSingle() =>
        (List<FractalResult>)_quotes.GetFractal(Wing, Wing, EndType.HighLow);
    [Benchmark] public FractalResult[] NativeParallel()
    {
        int workers = Math.Min(Count, Math.Min(8, Environment.ProcessorCount));
        var result = new FractalResult[Count];
        CpuParallelSettings.LightweightParallel(workers, workers, worker =>
        {
            int start = Count * worker / workers, end = Count * (worker + 1) / workers;
            int from = Math.Max(0, start - Wing), until = Math.Min(Count, end + Wing), index = from;
            // Only the two required wings overlap. Native preparation, allocation
            // and discarded overlap rows remain inside the timed method.
            foreach (var row in _quotes.Skip(from).Take(until - from).GetFractal(Wing, Wing, EndType.HighLow))
            {
                if (index >= start && index < end) result[index] = row;
                index++;
            }
        });
        return result;
    }
    private IIndicatorRun Build(RetrospectiveFractals indicator, IndicatorHistoryMode history) => new StockIndicatorBuilder()
        .ConfigureSource(Bars.From(_bars)).ConfigureIndicators(indicator).ConfigureHistory(history)
        .ConfigureExecution(IndicatorExecutionBackend.Cpu).BuildAsync().GetAwaiter().GetResult();
}

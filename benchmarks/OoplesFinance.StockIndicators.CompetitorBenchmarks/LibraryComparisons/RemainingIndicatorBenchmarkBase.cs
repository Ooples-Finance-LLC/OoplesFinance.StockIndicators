using BenchmarkDotNet.Attributes;
using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

public abstract class RemainingIndicatorBenchmarkBase
{
    [Params(1_000, 10_000)] public int Count { get; set; }
    [Params(IndicatorHistoryMode.Full, IndicatorHistoryMode.LatestOnly)]
    public IndicatorHistoryMode History { get; set; }
    private Bar[] _bars = null!;
    protected virtual double OpenOffset => 0;
    protected virtual double GetOpen(int index, double price) => price + OpenOffset;
    protected abstract IIndicator Create();

    [GlobalSetup]
    public void Setup()
    {
        _bars = Enumerable.Range(0, Count).Select(i =>
        {
            double price = 10 + (i * 13 % 101) / 8d;
            return new Bar(default, GetOpen(i, price), price + 1, price - 2, price, i % 7 + 1);
        }).ToArray();
        var indicator = Create();
        using var expected = Build(indicator, legacy: true);
        using var actual = Build(indicator, legacy: false);
        foreach (var output in indicator.Outputs)
        {
            var reference = expected[output].ToArray();
            var candidate = actual[output].ToArray();
            if (reference.Length != Count || candidate.Length != Count)
                throw new InvalidOperationException("Benchmark output length mismatch.");
            for (int i = 0; i < Count; i++)
                if (BitConverter.DoubleToInt64Bits(reference[i]) != BitConverter.DoubleToInt64Bits(candidate[i]))
                    throw new InvalidOperationException($"Benchmark output mismatch at {i}.");
        }
    }

    // Both measured arms include construction, execution, reading and disposal.
    protected double Run(bool legacy)
    {
        var indicator = Create();
        using var run = Build(indicator, legacy);
        return run[indicator.Outputs[0]][Count - 1];
    }

    private IIndicatorRun Build(IIndicator indicator, bool legacy)
    {
        var builder = new StockIndicatorBuilder().ConfigureSource(Bars.From(_bars))
            .ConfigureIndicators(indicator).ConfigureHistory(History)
            .ConfigureExecution(IndicatorExecutionBackend.Cpu);
        if (legacy) builder.ConfigureBehavior(_ => { });
        return builder.BuildAsync().GetAwaiter().GetResult();
    }
}

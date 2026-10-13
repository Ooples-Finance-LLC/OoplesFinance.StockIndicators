using AiDotNet.Tensors.Helpers;
using BenchmarkDotNet.Attributes;
using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Indicators;
using System.Numerics;
using static OoplesFinance.StockIndicators.CompetitorBenchmarks.MoneyFlowReferenceArithmetic;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

[MemoryDiagnoser, Config(typeof(TensorsGpuTimingConfig))]
public class SharedPriceCandleBenchmarks
{
    [Params(1000, 10000)] public int Count { get; set; }
    [Params("Skender.GetDoji", "Skender.GetMarubozu", "Skender.GetHeikinAshi")] public string Operation { get; set; } = "";
    private Bar[] _bars = null!;
    private int _priorDop;
    [GlobalSetup]
    public void Setup()
    {
        _priorDop = CpuParallelSettings.MaxDegreeOfParallelism;
        CpuParallelSettings.MaxDegreeOfParallelism = 8;
        var pair = ComparisonPairs.Get(Operation);
        var data = ComparisonVerifier.BenchmarkFixture(pair, Count); _bars = data.IndicatorBars;
        var owner = Create();
        double[][] expected;
        if (owner is PriceCandlePattern pattern) expected = Reference(_bars, pattern);
        else
        {
            var reference = pair.Reference!(data, 20);
            expected = pair.OutputNames!.Select(name => reference.Outputs[name].Values).ToArray();
        }
        // Exact independent references; saved native timing is never rerun here.
        foreach (var mode in Enum.GetValues<IndicatorHistoryMode>())
        {
            using var run = Build(owner, mode);
            for (int slot = 0; slot < expected.Length; slot++)
                AsinFeasibilityBenchmarks.RequireSame(expected[slot], run[owner.Outputs[slot]].ToArray());
        }
    }
    private static double[][] Reference(Bar[] bars, PriceCandlePattern owner)
    {
        var result = Enumerable.Range(0, 12).Select(_ => new double[bars.Length]).ToArray();
        var percent = Units(owner.Percent);
        for (int i = 0; i < bars.Length; i++)
        {
            var b = bars[i]; var open = Units(b.Open); var close = Units(b.Close);
            var high = Units(b.High); var low = Units(b.Low);
            var range = high - low; var body = BigInteger.Abs(close - open);
            var upper = high - BigInteger.Max(open, close); var lower = BigInteger.Min(open, close) - low;
            bool match = owner.Kind == PriceCandleKind.RelativeDoji
                ? !open.IsZero && body * 100 * Grid <= BigInteger.Abs(open) * percent
                : range.IsZero || body * 100 * Grid >= range * percent;
            result[0][i] = !match ? 0 : owner.Kind == PriceCandleKind.RelativeDoji ? 1 : close > open ? 100 : -100;
            result[1][i] = match ? b.Close : 0; result[2][i] = match ? 1 : 0;
            result[3][i] = Round(range, Grid); result[4][i] = Round(body, Grid);
            result[5][i] = Round(upper, Grid); result[6][i] = Round(lower, Grid);
            result[7][i] = range.IsZero ? 1 : Round(body, range);
            result[8][i] = range.IsZero ? 1 : Round(upper, range);
            result[9][i] = range.IsZero ? 1 : Round(lower, range);
            result[10][i] = close > open ? 1 : 0; result[11][i] = close < open ? 1 : 0;
        }
        return result;
    }
    [GlobalCleanup] public void Cleanup() => CpuParallelSettings.MaxDegreeOfParallelism = _priorDop;
    [Benchmark] public IIndicatorRun BuilderFull() => Build(Create(), IndicatorHistoryMode.Full);
    [Benchmark] public IIndicatorRun BuilderLatestOnly() => Build(Create(), IndicatorHistoryMode.LatestOnly);
    private IIndicator Create() => Operation == "Skender.GetHeikinAshi" ? new HeikinAshiCandles()
        : new PriceCandlePattern(Operation == "Skender.GetDoji" ? PriceCandleKind.RelativeDoji : PriceCandleKind.Marubozu);
    private IIndicatorRun Build(IIndicator owner, IndicatorHistoryMode mode) => new StockIndicatorBuilder()
        .ConfigureSource(Bars.From(_bars)).ConfigureIndicators(owner).ConfigureHistory(mode)
        .ConfigureExecution(IndicatorExecutionBackend.Cpu).BuildAsync().GetAwaiter().GetResult();
}

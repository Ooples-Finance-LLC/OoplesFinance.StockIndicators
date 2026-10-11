using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Validation;

namespace OoplesFinance.StockIndicators.Tests.Unit.ModelsTests;

public sealed class ExponentialStageExecutionTests
{
    public static IEnumerable<object[]> Variants =>
        from mode in Enumerable.Range(0, 3)
        from order in new[] { 2, 3 }
        select new object[] { mode, order };

    private static IndicatorBase Create(int mode, int order, int period) => mode == 0
        ? new CompensatedExponentialAverage(period, order)
        : new SeededExponentialAverage(period, order, mode == 1 ? ExponentialSeedMode.Shared : ExponentialSeedMode.Cascaded);

    [Theory, MemberData(nameof(Variants))]
    public void CompactStagesPreserveReferenceBitsAcrossResetAndExtremeInputs(int mode, int order)
    {
        double[][] fixtures =
        [
            Enumerable.Range(0, 83).Select(i => 100 + (i * 17 % 43) / 8d).ToArray(),
            [double.MaxValue, -double.MaxValue, 1, -1, double.Epsilon, -double.Epsilon, 0, -0d],
            [0, double.Epsilon, 3 * double.Epsilon, -double.Epsilon, 0, 1, Math.BitIncrement(1d)],
            Enumerable.Repeat(double.MaxValue, 43).ToArray(),
            Enumerable.Repeat(0d, 43).ToArray()
        ];
        foreach (int period in new[] { 1, 2, 20, int.MaxValue })
        {
            var indicator = Create(mode, order, period);
            var state = (IIndicatorState)indicator.CreateState();
            foreach (var prices in fixtures)
            {
                var bars = prices.Select(p => new Bar(default, 0, 1, -1, p, 1)).ToArray();
                var expected = mode == 0 ? CompensatedAverageReference.Calculate(bars, period, order)
                    : ((SeededExponentialAverage)indicator).Reference(bars);
                state.Reset();
                for (int i = 0; i < bars.Length; i++)
                {
                    Assert.Equal(BitConverter.DoubleToInt64Bits(expected[i]),
                        BitConverter.DoubleToInt64Bits(state.Update(in bars[i])));
                    // The independent compensated reference stops at genuine
                    // overflow, as does builder execution; reset before next input.
                    if (!double.IsFinite(expected[i])) break;
                }
            }
        }
    }

    [Theory, MemberData(nameof(Variants))]
    public void OrdinaryStreamingStagesAllocateNoPerBarObjects(int mode, int order)
    {
        var state = (IIndicatorState)Create(mode, order, 20).CreateState();
        var bars = Enumerable.Range(0, 97).Select(i => new Bar(default, 0, 1, -1, 100 + i / 8d, 1)).ToArray();
        double result = 0;
        for (int i = 0; i < 1000; i++) result = state.Update(in bars[i % bars.Length]);
        state.Reset(); // Include seeding and residual-mass compensation in the measured pass.
        var before = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 0; i < 4000; i++) result = state.Update(in bars[i % bars.Length]);
        Assert.Equal(0, GC.GetAllocatedBytesForCurrentThread() - before);
        Assert.True(double.IsFinite(result));
    }
}

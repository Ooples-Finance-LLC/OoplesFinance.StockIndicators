using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.Tests.Unit.ModelsTests;

public sealed class DojiShadowComparisonTests
{
    private static bool Expected(double upper, double lower, double top, double bottom, int multiplier)
    {
        var comparison = new ExactMeanAccumulator();
        comparison.Add(upper);
        comparison.Add(lower, -1);
        comparison.Add(top, -multiplier);
        comparison.Add(bottom, multiplier);
        return comparison.Sign > 0;
    }

    private static void Check(double upper, double lower, double top, double bottom, int multiplier) =>
        Assert.Equal(Expected(upper, lower, top, bottom, multiplier),
            AverageRangeDojiState.ShadowLongerThanBody(upper, lower, top, bottom, multiplier));

    [Fact]
    public void BoundaryDifferencesRetainStrictComparisons()
    {
        double[] values = [0, double.Epsilon, -double.Epsilon, 1, -1,
            Math.ScaleB(1, -1022), double.MaxValue, -double.MaxValue];
        foreach (double upper in values)
            foreach (double lower in values)
                foreach (double top in values)
                    foreach (double bottom in values)
                        foreach (int multiplier in new[] { 1, 2 }) Check(upper, lower, top, bottom, multiplier);
    }

    [Fact]
    public void RoundedAndOverflowingDifferencesCanHideStrictMatches()
    {
        double tiny = Math.ScaleB(1, -55), large = double.MaxValue * .4;
        (double Upper, double Lower, double Top, double Bottom, int Multiplier)[] cases =
            [(2, 1, 1, Math.ScaleB(1, -54), 1), (tiny, -1, .5, tiny, 2),
             (large, -double.MaxValue, double.MaxValue, large, 2)];
        foreach (var (upper, lower, top, bottom, multiplier) in cases)
        {
            Assert.True(Expected(upper, lower, top, bottom, multiplier));
            Assert.False(upper - lower > (top - bottom) * multiplier);
            Check(upper, lower, top, bottom, multiplier);
        }
    }

    [Fact]
    public void OrdinaryAndWideInputsMatchExactFallback()
    {
        var random = new Random(20261105);
        for (int i = 0; i < 2000; i++)
        {
            double bottom = Math.ScaleB(random.NextDouble() + 1, random.Next(-1000, 1000));
            double top = bottom * (1 + random.NextDouble() / 16);
            double upper = top * (1 + random.NextDouble() / 8);
            double lower = bottom * (1 - random.NextDouble() / 8);
            foreach (int multiplier in new[] { 1, 2, 3 }) Check(upper, lower, top, bottom, multiplier);
            double Next() => Math.ScaleB(random.NextDouble() * 2 - 1, random.Next(-1074, 1024));
            upper = Next(); lower = Next(); top = Next(); bottom = Next();
            foreach (int multiplier in new[] { 1, 2, 3 }) Check(upper, lower, top, bottom, multiplier);
        }
    }
}

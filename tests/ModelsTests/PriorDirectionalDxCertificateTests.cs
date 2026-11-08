using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Validation;

namespace OoplesFinance.StockIndicators.Tests.Unit.ModelsTests;

public sealed class PriorDirectionalDxCertificateTests
{
    [Fact]
    public void OrdinaryDirectionalRatiosMatchIndependentReference()
    {
        var random = new Random(9137);
        var bars = Enumerable.Range(0, 180).Select(i =>
        {
            double low = 20 + random.NextDouble() * 10;
            double high = low + .125 + random.NextDouble() * 8;
            return new Bar(default, low, high, low, low + random.NextDouble() * (high - low), i);
        }).ToArray();
        Verify(bars);
    }

    [Fact]
    public void WideSubnormalAndInvertedStagesRetainExactFallback()
    {
        double m = double.MaxValue, e = double.Epsilon;
        Bar[] cycle = [new(default, 0, m, -m, 0, 1), new(default, 0, -m, -m, m, 1),
            new(default, 0, m, m, -m, 1), new(default, 0, 3 * e, e, 2 * e, 1),
            new(default, 0, e, -e, 0, 1), new(default, 0, 1, 4, 2, 1),
            new(default, 0, 4, 1, 3, 1), new(default, 0, 1e100, -1e100, 0, 1)];
        Verify(Enumerable.Range(0, 144).Select(i => cycle[i % cycle.Length]).ToArray());
    }

    [Fact]
    public void BalancedAndFlatRunsPreserveHoldStartupSuppressionAndReset()
    {
        var bars = Enumerable.Range(0, 180).Select(i =>
        {
            double center = i < 50 ? 10 : i < 110 ? 10 + (i % 4 == 0 ? 1 : -1) : 11;
            double radius = i < 30 || i >= 140 ? 0 : 2;
            return new Bar(default, center, center + radius, center - radius, center, 0);
        }).ToArray();
        Verify(bars);
    }

    private static void Verify(Bar[] bars)
    {
        foreach (var measure in Enum.GetValues<PriorDirectionalMeasure>())
        foreach (int period in new[] { 1, 2, 3, 14 })
        foreach (int unstable in new[] { 0, 3 })
        {
            var expected = PriorDirectionalReference.Calculate(bars, measure, period, unstable);
            var state = (IMultiOutputState)new PriorSeededDirectionalMeasure(measure, period, unstable).CreateState();
            var output = new double[2];
            for (int replay = 0; replay < 2; replay++)
            {
                state.Reset();
                for (int row = 0; row < bars.Length; row++)
                {
                    state.Update(in bars[row], output);
                    Assert.Equal(expected[row].HasValue ? 1d : 0d, output[1]);
                    Assert.Equal(BitConverter.DoubleToInt64Bits(expected[row] ?? 0), BitConverter.DoubleToInt64Bits(output[0]));
                }
            }
        }
    }
}

using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.Tests.Unit.ModelsTests;

public sealed class AverageDirectDifferenceTests
{
    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(2)]
    public void RoundedMeansRetainExactDifferenceAndCanonicalZero(int kind)
    {
        double[] values = [0d, -0d, double.Epsilon, -double.Epsilon,
            Math.ScaleB(1, -1022), -Math.ScaleB(1, -1022),
            1, -1, Math.BitIncrement(1), Math.BitDecrement(1),
            double.MaxValue, -double.MaxValue,
            Math.BitIncrement(Math.ScaleB(1, -54)), -Math.BitIncrement(Math.ScaleB(1, -54))];
        foreach (double a in values)
        foreach (double b in values)
        {
            IMultiOutputState state = kind switch
            {
                0 => new MovingAverageDifference.State(1, 2, false),
                1 => new MovingAverageDifference.State(1, 2, true),
                _ => new AwesomeWithDetails.State(1, 2, false)
            };
            var mean = new ExactMeanAccumulator();
            mean.Add(a); mean.Add(b, kind == 1 ? 2 : 1);
            var exact = new ExactMeanAccumulator();
            exact.Add(b); exact.Add(mean.Mean(kind == 1 ? 3 : 2), -1);
            double expected = exact.Mean(1);
            var output = new double[kind == 2 ? 4 : 2];
            for (int replay = 0; replay < 2; replay++)
            {
                state.Reset();
                state.Update(new Bar(default, a, a, a, a, 1), output);
                Assert.Equal(0L, BitConverter.DoubleToInt64Bits(output[0]));
                state.Update(new Bar(default, b, b, b, b, 1), output);
                Assert.Equal(BitConverter.DoubleToInt64Bits(expected), BitConverter.DoubleToInt64Bits(output[0]));
            }
        }
    }
}

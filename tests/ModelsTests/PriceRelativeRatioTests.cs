using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Validation;

namespace OoplesFinance.StockIndicators.Tests.Unit.ModelsTests;

public sealed class PriceRelativeRatioTests
{
    [Fact]
    public void BoundaryRatiosMatchIndependentRationalRoundingBits()
    {
        double[] values = [0d, -0d, double.Epsilon, -double.Epsilon,
            2 * double.Epsilon, 3 * double.Epsilon, 2.2250738585072014E-308,
            Math.BitDecrement(2.2250738585072014E-308), Math.BitIncrement(2.2250738585072014E-308),
            0.5, -0.5, 1, -1, 2, -2, 3, Math.BitDecrement(1), Math.BitIncrement(1),
            double.MaxValue, -double.MaxValue];
        foreach (double evaluation in values)
            foreach (double basis in values)
                Check(evaluation, basis);
    }

    [Fact]
    public void FiniteBitPatternsMatchIndependentRationalRoundingBits()
    {
        var random = new Random(20261028);
        for (int i = 0; i < 2048; i++)
        {
            double Next()
            {
                long bits = random.NextInt64();
                if (i % 2 != 0) bits |= long.MinValue;
                bits &= ~0x7ff0000000000000L;
                bits |= (long)random.Next(2047) << 52;
                return BitConverter.Int64BitsToDouble(bits);
            }
            Check(Next(), Next());
        }
    }

    private static void Check(double evaluation, double basis)
    {
        var bar = new Bar(default, basis, 1, 0, evaluation, 1);
        var expected = PriceRelativeReference.Values([bar], null, null,
            CandlePriceField.Close, CandlePriceField.Open)[0][0];
        var state = new PriceRelativeStrength.State(null, null,
            CandlePriceField.Close, CandlePriceField.Open);
        Span<double> output = stackalloc double[6];
        for (int replay = 0; replay < 2; replay++)
        {
            state.Update(in bar, output);
            Assert.Equal(expected.HasValue ? 1d : 0d, output[3]);
            Assert.Equal(BitConverter.DoubleToInt64Bits(expected ?? 0),
                BitConverter.DoubleToInt64Bits(output[0]));
            state.Reset();
        }
    }
}

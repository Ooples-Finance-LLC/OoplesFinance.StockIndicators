using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Validation;

namespace OoplesFinance.StockIndicators.Tests.Unit.ModelsTests;

public sealed class PriceRelativeReturnTests
{
    [Fact]
    public void CancellationUnderflowOverflowAndMissingHistoryMatchReference()
    {
        Check(1, -1, 2, -2); // Exact zero with negative denominator.
        Check(double.MaxValue, double.MaxValue, double.Epsilon, 2 * double.Epsilon);
        Check(double.MaxValue, double.MaxValue, 2 * double.Epsilon, double.Epsilon);
        Check(double.Epsilon, double.Epsilon, double.MaxValue, double.MaxValue);
        Check(double.Epsilon, double.Epsilon, double.MaxValue, double.MaxValue / 2);
        Check(-double.Epsilon, double.Epsilon, double.MaxValue, double.MaxValue);
        Check(0, 1, 2, 3);
        Check(1, -0d, 2, 3);
        Check(1, 1, 2, 0);
        Check(1, 1, double.MaxValue, double.Epsilon); // Ratio overflow returns early.
    }

    [Fact]
    public void RandomExactReturnExpressionsRetainRoundingAndResetBits()
    {
        var random = new Random(20261029);
        for (int i = 0; i < 2048; i++)
        {
            double Next() => Math.ScaleB((random.NextDouble() + 0.5) * (random.Next(2) == 0 ? -1 : 1), random.Next(-500, 501));
            Check(Next(), Next(), Next(), Next());
        }
    }

    private static void Check(double oldEvaluation, double oldBase, double evaluation, double basis)
    {
        Bar[] bars = [new(default, oldBase, 1, 0, oldEvaluation, 1), new(default, basis, 1, 0, evaluation, 1)];
        var expected = PriceRelativeReference.Values(bars, 1, null, CandlePriceField.Close, CandlePriceField.Open);
        var state = new PriceRelativeStrength.State(1, null, CandlePriceField.Close, CandlePriceField.Open);
        Span<double> output = stackalloc double[6];
        for (int replay = 0; replay < 2; replay++)
        {
            for (int i = 0; i < bars.Length; i++)
            {
                state.Update(in bars[i], output);
                for (int slot = 0; slot < 3; slot++)
                {
                    Assert.Equal(expected[slot][i].HasValue ? 1d : 0d, output[slot + 3]);
                    Assert.Equal(BitConverter.DoubleToInt64Bits(expected[slot][i] ?? 0), BitConverter.DoubleToInt64Bits(output[slot]));
                }
            }
            state.Reset();
        }
    }
}

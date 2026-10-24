using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.Tests.Unit.ModelsTests;

[Collection("IndicatorValuesDispatch")]
public sealed class TillsonWordSmoothingTests
{
    [Fact]
    public void WordSmoothingMatchesExactBoundaryRatios()
    {
        const long mask = (1L << 52) - 1;
        foreach (int exponent in new[] { 1, 2, 500, 1023, 1500, 2045, 2046 })
        foreach (int period in new[] { 1, 2, 3, 20, 1023, 2047 })
        foreach (long a in new[] { 0L, 1L, mask - 1, mask })
        foreach (long b in new[] { 0L, 1L, mask - 1, mask })
            Check(BitConverter.Int64BitsToDouble(((long)exponent << 52) | a),
                BitConverter.Int64BitsToDouble(((long)exponent << 52) | b), period);
        // Equal operands with d=3 exercise the reciprocal's one-low correction.
        Check(1, 1, 2);
        // d=4, adjacent significands: both even and odd quotient midpoint ties.
        Check(1, Math.BitIncrement(1), 3);
        Check(Math.BitIncrement(1), Math.BitIncrement(Math.BitIncrement(1)), 3);
    }

    [Fact]
    public void WordSmoothingMatchesExactRandomRatios()
    {
        var random = new Random(309248);
        for (int i = 0; i < 4096; i++)
        {
            long exponent = (long)random.Next(1, 2047) << 52;
            double prior = BitConverter.Int64BitsToDouble(exponent | random.NextInt64(1L << 52));
            double input = BitConverter.Int64BitsToDouble(exponent | random.NextInt64(1L << 52));
            Check(prior, input, random.Next(1, 2048));
        }
    }

    [Fact]
    public void UnsupportedInputsRetainFallback()
    {
        Assert.False(default(CertifiedTillson.WordSmoother).TrySmooth(1, 1, out _));
        foreach (int period in new[] { int.MinValue, -1, 0, 2048, int.MaxValue })
            Assert.False(new CertifiedTillson.WordSmoother(period).TrySmooth(1, 1, out _));
        var smoother = new CertifiedTillson.WordSmoother(20);
        foreach (double value in new[] { 0d, -0d, -1d, double.Epsilon,
            Math.BitDecrement(Math.ScaleB(1, -1022)), double.NaN,
            double.PositiveInfinity, double.NegativeInfinity, 2d })
        {
            Assert.False(smoother.TrySmooth(value, 1, out _));
            Assert.False(smoother.TrySmooth(1, value, out _));
        }
    }

    [Fact]
    public void OriginalStateBitsSurviveWarmupFallbackTransitionsAndReset()
    {
        double[] prices = Enumerable.Range(0, 128).Select(i => 100 + i % 7d)
            .Concat(new[] { 0d, -100d, double.Epsilon, Math.ScaleB(1, -1022),
                Math.ScaleB(1, 500), Math.ScaleB(-1, 500), 128d, Math.BitDecrement(128d) })
            .Concat(Enumerable.Repeat(100d, 1024)).ToArray();
        foreach (TillsonSeed seed in Enum.GetValues<TillsonSeed>())
        foreach (int period in new[] { 1, 3, 20, 2047, 2048, int.MaxValue })
        {
            var original = (IMultiOutputState)new TillsonAverage(period, .7, seed, 7).CreateState();
            var candidate = new ValuesBarExecution.TillsonValueState(period, .7, seed, 7);
            var expected = new double[2]; var actual = new double[2];
            for (int repeat = 0; repeat < 2; repeat++)
            {
                original.Reset(); candidate.Reset();
                for (int i = 0; i < prices.Length; i++)
                {
                    double price = prices[i];
                    var bar = new Bar(DateTime.UnixEpoch.AddMinutes(i), price, price, price, price, 1);
                    original.Update(bar, expected); candidate.Update(bar, actual);
                    Equal(expected[0], actual[0]); Equal(expected[1], actual[1]);
                }
            }
        }
    }

    private static void Check(double prior, double input, int period)
    {
        Assert.True(new CertifiedTillson.WordSmoother(period).TrySmooth(prior, input, out double actual));
        var exact = new ExactMeanAccumulator();
        exact.Add(prior, period - 1L); exact.Add(input, 2);
        Equal(exact.Mean((long)period + 1), actual);
    }

    private static void Equal(double expected, double actual) =>
        Assert.Equal(BitConverter.DoubleToInt64Bits(expected), BitConverter.DoubleToInt64Bits(actual));
}

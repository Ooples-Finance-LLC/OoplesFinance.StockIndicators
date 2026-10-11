using OoplesFinance.StockIndicators.Helpers;

namespace OoplesFinance.StockIndicators.Tests.Unit.ModelsTests;

public sealed class SharedRocPublicationTests
{
    [Fact]
    public void PublicationPreservesExactBitsAcrossExponentAndExtendedRangeBoundaries()
    {
        var random = new Random(72941);
        var bytes = new byte[8];
        var values = new List<double> { 0, -0d, double.Epsilon, -double.Epsilon,
            double.MaxValue, -double.MaxValue, double.NaN, double.PositiveInfinity, double.NegativeInfinity };
        for (int exponent = 0; exponent < 2047; exponent++)
        {
            random.NextBytes(bytes);
            long fraction = BitConverter.ToInt64(bytes) & 0xfffffffffffffL;
            long bits = ((long)exponent << 52) | fraction;
            values.Add(BitConverter.Int64BitsToDouble(bits));
            values.Add(BitConverter.Int64BitsToDouble(bits | long.MinValue));
        }
        foreach (double value in values)
        foreach (int shift in new[] { 0, 32, 1024 })
        {
            var stage = new RocBankValue(value, shift);
            double expected = 0, actual = 0;
            var expectedError = Record.Exception(() =>
            {
                var sum = new ExactMeanAccumulator(); stage.AddTo(ref sum); expected = sum.Mean(1);
            });
            var actualError = Record.Exception(() => actual = stage.Publish());
            if (expectedError is not null)
            {
                Assert.NotNull(actualError);
                Assert.Equal(expectedError.GetType(), actualError.GetType());
                Assert.Equal(expectedError.Message, actualError.Message);
            }
            else
            {
                Assert.Null(actualError);
                Assert.Equal(BitConverter.DoubleToInt64Bits(expected), BitConverter.DoubleToInt64Bits(actual));
            }
        }
    }
}

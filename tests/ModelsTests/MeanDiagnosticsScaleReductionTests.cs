using System.Numerics;
using OoplesFinance.StockIndicators.Helpers;
using Xunit.Abstractions;

namespace OoplesFinance.StockIndicators.Tests.Unit.ModelsTests;

public sealed class MeanDiagnosticsScaleReductionTests(ITestOutputHelper output)
{
    [Fact]
    public void RelativeErrorRetainsOriginalBitsAcrossScalesCancellationAndReset()
    {
        var random = new Random(2551074);
        double[][] cases =
        [
            [1, -1, 3, -3, 7, -7, 0, -0d, 11, 13, 17],
            [double.Epsilon, -double.Epsilon, double.MaxValue, -double.MaxValue,
                Math.ScaleB(1, -1022), Math.ScaleB(1, 700), Math.ScaleB(-1, -700), 1, -1],
            Enumerable.Range(0, 97).Select(i => 100 + i / 8d + (i % 7) / 10d).ToArray(),
            Enumerable.Range(0, 97).Select(_ =>
            {
                var bits = random.NextInt64(long.MinValue, long.MaxValue);
                var value = BitConverter.Int64BitsToDouble(bits);
                return double.IsFinite(value) ? value : double.Epsilon;
            }).ToArray()
        ];
        foreach (var prices in cases)
        foreach (int period in new[] { 1, 2, 3, 20, int.MaxValue })
        {
            var window = new RoundedMeanErrorWindow(period);
            var values = new Queue<BigInteger>();
            for (int replay = 0; replay < 2; replay++)
            {
                window.Reset();
                values.Clear();
                foreach (var price in prices)
                {
                    window.Add(price);
                    if (values.Count == period) values.Dequeue();
                    values.Enqueue(ExactVarianceWindow.Units(price));
                    double mean = window.Mean;
                    var expected = Original(values, mean);
                    var actual = window.SignedRelativeError(mean);
                    Assert.Equal(expected.Defined, actual.Defined);
                    Assert.Equal(BitConverter.DoubleToInt64Bits(expected.Value), BitConverter.DoubleToInt64Bits(actual.Value));
                }
            }
        }
    }

    [Fact]
    public void OrdinaryWindowReducesRelativeErrorAllocations()
    {
        var window = new RoundedMeanErrorWindow(20);
        var values = new Queue<BigInteger>();
        for (int i = 0; i < 20; i++)
        {
            double price = 100 + i / 8d + (i % 7) / 10d;
            window.Add(price);
            values.Enqueue(ExactVarianceWindow.Units(price));
        }
        double mean = window.Mean;
        double prior = 0, current = 0;
        for (int i = 0; i < 16; i++)
        {
            prior = Original(values, mean).Value;
            current = window.SignedRelativeError(mean).Value;
        }
        long start = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 0; i < 128; i++) prior = Original(values, mean).Value;
        long originalBytes = GC.GetAllocatedBytesForCurrentThread() - start;
        start = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 0; i < 128; i++) current = window.SignedRelativeError(mean).Value;
        long currentBytes = GC.GetAllocatedBytesForCurrentThread() - start;
        Assert.Equal(BitConverter.DoubleToInt64Bits(prior), BitConverter.DoubleToInt64Bits(current));
        output.WriteLine($"128 relative-error evaluations: original={originalBytes}, reduced={currentBytes} bytes");
        Assert.True(currentBytes < originalBytes, $"{currentBytes} >= {originalBytes}");
    }

    // Frozen pre-change arithmetic, independent of the binary-scale reduction.
    private static (bool Defined, double Value) Original(Queue<BigInteger> values, double mean)
    {
        var center = ExactVarianceWindow.Units(mean);
        var numerator = BigInteger.Zero;
        var denominator = BigInteger.One;
        foreach (var value in values)
        {
            if (value.IsZero) return (false, 0);
            var top = BigInteger.Abs(value - center) * value.Sign;
            var bottom = BigInteger.Abs(value);
            var reduce = BigInteger.GreatestCommonDivisor(BigInteger.Abs(top), bottom);
            top /= reduce;
            bottom /= reduce;
            var common = BigInteger.GreatestCommonDivisor(denominator, bottom);
            numerator = numerator * (bottom / common) + top * (denominator / common);
            denominator = denominator / common * bottom;
            reduce = BigInteger.GreatestCommonDivisor(BigInteger.Abs(numerator), denominator);
            numerator /= reduce;
            denominator /= reduce;
        }
        return (true, ExactMeanAccumulator.UnitRatio(numerator << 1074, denominator * values.Count));
    }
}

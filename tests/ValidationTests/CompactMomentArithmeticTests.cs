using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Validation;

namespace OoplesFinance.StockIndicators.Tests.Unit.ValidationTests;

public sealed class CompactMomentArithmeticTests
{
    [Fact]
    public void SquaresAndRootsPreserveWideCancellationSubnormalsAndCopiedState()
    {
        double[] inputs = [0, -0d, double.Epsilon, -double.Epsilon, .1, -.3,
            Math.BitIncrement(1d), Math.ScaleB(1, 500), double.MaxValue, -double.MaxValue];
        foreach (var left in inputs)
        foreach (var right in inputs)
        {
            var sum = JurikDyadic.FromDouble(left);
            sum.Add(right);
            var original = sum;
            sum.Square();
            var exact = ReferenceFraction.FromDouble(left) + ReferenceFraction.FromDouble(right);
            var square = exact * exact;
            foreach (long count in new[] { 1L, 20L, (long)int.MaxValue * (int.MaxValue - 1) })
                Bits((square / new ReferenceFraction(count)).SqrtToDouble(), sum.SqrtMean(count));
            Bits(exact.ToDouble(), original.Mean(1));

            var fallback = new ExactMeanAccumulator();
            fallback.Add(left);
            fallback.Add(right);
            fallback.Square();
            Bits((square / new ReferenceFraction(20)).SqrtToDouble(), fallback.SqrtMean(20));
        }
    }

    [Fact]
    public void OrdinaryRollingMomentUpdatesAllocateNoPerSampleObjects()
    {
        const int period = 20;
        var prices = Enumerable.Range(0, 101).Select(i => 10 + (i * 13 % 101) / 8d).ToArray();
        var returns = Enumerable.Range(0, prices.Length)
            .Select(i => CertifiedLogReturn.Of(prices[(i + 1) % prices.Length], prices[i])).ToArray();
        var window = new double[period];
        var sum = new JurikDyadic();
        var squares = new JurikDyadic();
        void Update(int i)
        {
            var expired = JurikDyadic.FromDouble(window[i % period]);
            sum.Subtract(expired);
            expired.Square();
            squares.Subtract(expired);
            double value = returns[i % returns.Length];
            window[i % period] = value;
            var current = JurikDyadic.FromDouble(value);
            sum.AddExact(current);
            current.Square();
            squares.AddExact(current);
        }
        for (int i = 0; i < 1000; i++) Update(i);
        var before = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 1000; i < 5000; i++) Update(i);
        Assert.Equal(0, GC.GetAllocatedBytesForCurrentThread() - before);

        var expectedSum = new ReferenceFraction(0);
        var expectedSquares = new ReferenceFraction(0);
        foreach (double value in window)
        {
            var exact = ReferenceFraction.FromDouble(value);
            expectedSum += exact;
            expectedSquares += exact * exact;
        }
        var expected = ((new ReferenceFraction(period) * expectedSquares - expectedSum * expectedSum)
            / new ReferenceFraction(period * (period - 1))).SqrtToDouble();
        squares.Multiply(period);
        sum.Square();
        squares.Subtract(sum);
        Bits(expected, squares.SqrtMean(period * (period - 1)));
    }

    private static void Bits(double expected, double actual) =>
        Assert.Equal(BitConverter.DoubleToInt64Bits(expected), BitConverter.DoubleToInt64Bits(actual));
}

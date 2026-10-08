using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Streaming;
using OoplesFinance.StockIndicators.Validation;
using Number = OoplesFinance.StockIndicators.Helpers.MacZWindow.Number;

namespace OoplesFinance.StockIndicators.Tests.Unit.ValidationTests;

public sealed class UnroundedMovingAverageTests
{
    public static IEnumerable<object[]> Cases =>
        from kind in new[] { MovingAvgType.SimpleMovingAverage, MovingAvgType.WeightedMovingAverage,
            MovingAvgType.ExponentialMovingAverage, MovingAvgType.WildersSmoothingMethod }
        from length in new[] { 0, 1, 3, int.MaxValue }
        select new object[] { kind, length };

    [Theory, MemberData(nameof(Cases))]
    public void IndependentWindowAndRecurrenceSurvivePreviewResetAndCancellation(MovingAvgType kind, int length)
    {
        length = Math.Max(1, length);
        var prices = new[] { double.MaxValue, -double.MaxValue, 1, double.Epsilon, -3, 7, 0, -1, 9, 2 };
        var rational = prices.Select(ReferenceFraction.FromDouble).ToArray();
        var expected = new double[prices.Length];
        var previous = ReferenceFraction.FromDouble(0);
        ReferenceFraction R(double value) => ReferenceFraction.FromDouble(value);
        for (var i = 0; i < prices.Length; i++)
        {
            var value = R(0);
            if (length == 1) value = rational[i];
            else if (kind == MovingAvgType.SimpleMovingAverage || kind == MovingAvgType.WeightedMovingAverage)
            {
                for (var j = Math.Max(0, i - length + 1); j <= i; j++)
                    value += rational[j] * R(kind == MovingAvgType.WeightedMovingAverage ? length - (i - j) : 1);
                value = kind == MovingAvgType.WeightedMovingAverage
                    ? value / R(length) / R(length + 1L) * R(2)
                    : i + 1 < length ? R(0) : value / R(length);
            }
            else if (kind == MovingAvgType.ExponentialMovingAverage && i < length)
            {
                for (var j = 0; j <= i; j++) value += rational[j];
                value /= R(i + 1);
            }
            else
            {
                var alpha = R(kind == MovingAvgType.ExponentialMovingAverage ? 2 : 1)
                    / R(kind == MovingAvgType.ExponentialMovingAverage ? length + 1L : length);
                value = (R(1) - alpha) * previous + alpha * rational[i];
            }
            previous = value;
            expected[i] = value.ToDouble();
        }

        using var average = new UnroundedMovingAverage(kind, length);
        for (var replay = 0; replay < 2; replay++)
        {
            average.Next(Number.Of(37), true);
            average.Reset();
            for (var i = 0; i < prices.Length; i++)
            {
                average.Next(Number.Of(-17), false);
                foreach (var final in new[] { false, false, true })
                    Assert.Equal(expected[i], average.Next(Number.Of(prices[i]), final).Publish());
            }
        }
    }

    [Fact]
    public void UnpublishedStagesPreserveSmallResidualAfterOverflowCancellation()
    {
        using var first = new UnroundedMovingAverage(MovingAvgType.SimpleMovingAverage, 3);
        using var second = new UnroundedMovingAverage(MovingAvgType.SimpleMovingAverage, 1);
        first.Next(Number.Of(double.MaxValue), true);
        first.Next(Number.Of(1), true);
        var residual = first.Next(Number.Of(-double.MaxValue), true);
        Assert.Equal(1d / 3, second.Next(residual, true).Publish());
        Assert.Equal(0, (residual.Times(3) - Number.Of(1)).Sign);
    }

    [Fact]
    public void FallbackPreservesItsOwnPreviewAndResetContract()
    {
        const MovingAvgType kind = MovingAvgType.DoubleExponentialMovingAverage;
        using var actual = new UnroundedMovingAverage(kind, 3);
        using var expected = MovingAverageSmootherFactory.Create(kind, 3);
        for (var replay = 0; replay < 2; replay++)
        {
            actual.Reset(); expected.Reset();
            foreach (var value in new[] { 7d, -2, 3, 9, 1, 4 })
            foreach (var final in new[] { false, false, true })
                Assert.Equal(expected.Next(value, final), actual.Next(Number.Of(value), final).Publish());
        }
    }
}

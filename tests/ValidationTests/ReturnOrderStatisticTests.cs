using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Validation;

namespace OoplesFinance.StockIndicators.Tests.Unit.ValidationTests;

public sealed class ReturnOrderStatisticTests
{
    [Theory]
    [InlineData(1)]
    [InlineData(3)]
    [InlineData(100)]
    public void ExactReturnRanksMatchIndependentFractionsThroughEvictionAndRepeatedQueries(int length)
    {
        var levels = new[] { double.MaxValue, -double.MaxValue, double.Epsilon, -double.Epsilon, 0d, 1d, -1d, 2d, 4d, 8d };
        var values = levels.SelectMany(a => levels.Select(b => (a, b))).ToList();
        var random = new Random(244);
        for (var i = 0; i < 512; i++) values.Add((random.Next(-20, 21), random.Next(-20, 21)));
        using var order = new ReturnOrderStatistic(length);
        var history = new Queue<ReferenceFraction>();
        foreach (var (current, previous) in values)
        {
            var value = previous == 0 ? new ReferenceFraction(0) :
                (ReferenceFraction.FromDouble(current) - ReferenceFraction.FromDouble(previous)) / ReferenceFraction.FromDouble(previous);
            var expected = history.Count(v => v.CompareTo(value) < 0);
            Assert.Equal(expected, order.CountLessThan(current, previous));
            Assert.Equal(expected, order.CountLessThan(current, previous));
            order.Add(current, previous);
            if (history.Count == length) history.Dequeue();
            history.Enqueue(value);
        }
    }

    [Fact]
    public void DistinctOverflowingReturnsAndExactTiesRemainDistinguishable()
    {
        using var order = new ReturnOrderStatistic(4);
        order.Add(double.MaxValue / 2, double.Epsilon);
        Assert.Equal(1, order.CountLessThan(double.MaxValue, double.Epsilon));
        Assert.Equal(0, order.CountLessThan(double.MaxValue, 2 * double.Epsilon));
        order.Add(-double.MaxValue / 2, -double.Epsilon);
        Assert.Equal(2, order.CountLessThan(double.MaxValue, double.Epsilon));
        Assert.Equal(0, order.CountLessThan(double.MaxValue / 2, double.Epsilon));
    }
}

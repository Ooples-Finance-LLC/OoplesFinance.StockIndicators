using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Streaming;
using OoplesFinance.StockIndicators.Validation;

namespace OoplesFinance.StockIndicators.Tests.Unit.ValidationTests;

public sealed class MeanRoundoffTests
{
    [Fact]
    public void ForwardBoundEnclosesExactPrefixErrorsThroughCascadedCancellation()
    {
        double[] values = [1e100, 1e84, -9.9989e99, -1.0998790000010039e96,
            -1e100, 1e84, 9.9989e99, 1.0998790000010039e96, double.Epsilon, -double.Epsilon];
        var exact = new ReferenceFraction(0);
        double sum = 0, bound = 0;
        foreach (var value in values)
        {
            exact += ReferenceFraction.FromDouble(value);
            sum += value;
            bound = MeanRoundoff.AfterAddition(bound, sum);
            var actualError = (ReferenceFraction.FromDouble(sum) - exact).Abs();
            Assert.True(actualError.CompareTo(ReferenceFraction.FromDouble(bound)) <= 0);
        }
        Assert.True(MeanRoundoff.RequiresExact(sum, values.Length, bound));
    }

    [Fact]
    public void PreviewRoundoffCannotContaminateAReplacementCommit()
    {
        using var actual = new SimpleMovingAverageSmoother(4);
        using var control = new SimpleMovingAverageSmoother(4);
        foreach (var value in new[] { 1e100, 1e84, -9.9989e99 })
            Assert.Equal(control.Next(value, true), actual.Next(value, true));
        Assert.Equal(3.02500002500381e91, actual.Next(-1.0998790000010039e96, false));
        actual.Next(double.MaxValue, false);
        Assert.Equal(control.Next(0, true), actual.Next(0, true));
        foreach (var value in new[] { 1d, -1d, 2d, -2d, 3d })
            Assert.Equal(control.Next(value, true), actual.Next(value, true));
    }
}

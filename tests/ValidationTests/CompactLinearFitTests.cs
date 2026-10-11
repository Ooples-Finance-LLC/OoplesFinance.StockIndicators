using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Validation;

namespace OoplesFinance.StockIndicators.Tests.Unit.ValidationTests;

public sealed class CompactLinearFitTests
{
    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(7)]
    [InlineData(int.MaxValue)]
    public void CompactMomentsMatchMinimumUnitsIncludingPreviewAndCompoundReadings(int period)
    {
        using var original = new ExactLinearFitWindow(period, observedHistory: true);
        using var compact = new ExactLinearFitWindow(period, observedHistory: true, compact: true);
        var values = new[] { 0d, -0d, 12.5, 14, 13.125, double.MaxValue, -double.MaxValue,
            double.Epsilon, -double.Epsilon, 0, 1, 2, 3, 4, 5, 6, 7, 8 };
        for (var replay = 0; replay < 2; replay++)
        {
            original.Reset(); compact.Reset();
            foreach (var value in values)
            {
                // A discarded preview may need a finer grid than the next commit.
                Check(original.Next(double.Epsilon, false), compact.Next(double.Epsilon, false), double.Epsilon);
                Check(original.Next(value, false), compact.Next(value, false), value);
                Check(original.Next(value, true), compact.Next(value, true), value);
            }
        }
    }

    private static void Check(ExactLinearFitWindow.Fit expected, ExactLinearFitWindow.Fit actual, double input)
    {
        var period = expected.Count + 1;
        var left = new[] { expected.Last, expected.Next, expected.Slope, expected.Index, expected.WindowIntercept,
            expected.OneBasedWindowIntercept, expected.OneBasedGlobalIntercept, expected.GlobalIntercept,
            expected.WindowPosition(2), expected.EndpointWeights(period, true), expected.EndpointWeights(period, false),
            expected.PercentResidual(input), expected.PercentFitResidual(input), expected.CenteredLine(2, .5), expected.Offset(2, .5) };
        var right = new[] { actual.Last, actual.Next, actual.Slope, actual.Index, actual.WindowIntercept,
            actual.OneBasedWindowIntercept, actual.OneBasedGlobalIntercept, actual.GlobalIntercept,
            actual.WindowPosition(2), actual.EndpointWeights(period, true), actual.EndpointWeights(period, false),
            actual.PercentResidual(input), actual.PercentFitResidual(input), actual.CenteredLine(2, .5), actual.Offset(2, .5) };
        Assert.Equal(left.Select(BitConverter.DoubleToInt64Bits), right.Select(BitConverter.DoubleToInt64Bits));
        Assert.Equal(expected.RoundedLastUnits, actual.RoundedLastUnits);
        Assert.Equal(expected.CenteredUnits(2, .5, 2), actual.CenteredUnits(2, .5, 2));
        Assert.True(actual.Difference(expected).Numerator.IsZero);
    }

    [Theory]
    [InlineData(WindowRegressionOutput.Endpoint)]
    [InlineData(WindowRegressionOutput.Forecast)]
    [InlineData(WindowRegressionOutput.Slope)]
    [InlineData(WindowRegressionOutput.Intercept)]
    [InlineData(WindowRegressionOutput.Angle)]
    public async Task RegressionOutputsMatchIndependentFormulas(WindowRegressionOutput output)
    {
        var report = await IndicatorValidation.ValidateAsync(new(typeof(WindowLinearRegression), "compact-fit",
            () => new WindowLinearRegression(3, output)));
        report.ThrowIfInvalid();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task EndpointWeightsMatchIndependentFormulas(bool averageDuringWarmup)
    {
        var report = await IndicatorValidation.ValidateAsync(new(typeof(EndpointWeightedAverage), "compact-weights",
            () => new EndpointWeightedAverage(7, averageDuringWarmup)));
        report.ThrowIfInvalid();
    }
}

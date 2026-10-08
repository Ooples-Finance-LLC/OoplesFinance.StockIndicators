using OoplesFinance.StockIndicators.Core;
using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Models;
using OoplesFinance.StockIndicators.Streaming;
using OoplesFinance.StockIndicators.Validation;

namespace OoplesFinance.StockIndicators.Tests.Unit.ValidationTests;

public sealed class PriceMeanExtremeTests
{
    [Fact]
    public void LegacyAndNativeRoutesRetainFiniteMixedFieldMeans()
    {
        var maximum = double.MaxValue;
        var time = DateTime.UnixEpoch;
        StockData Data() => new(new List<double> { maximum, maximum }, new List<double> { maximum, 1 },
            new List<double> { maximum, -maximum }, new List<double> { maximum, 2 },
            new List<double> { 1, 1 }, new List<DateTime> { time, time.AddMinutes(1) });
        var routes = new (Func<StockData, StockData> Legacy, IStreamingIndicatorState Native, IInputSeries Preset, double Expected)[]
        {
            (d => d.CalculateAveragePrice(), new AveragePriceState(), InputSeries.AveragePrice, maximum / 2),
            (d => d.CalculateMedianPrice(), new MedianPriceState(), InputSeries.MedianPrice, -maximum / 2),
            (d => d.CalculateTypicalPrice(), new TypicalPriceState(), InputSeries.TypicalPrice, -maximum / 3),
            (d => d.CalculateFullTypicalPrice(), new FullTypicalPriceState(), InputSeries.FullTypicalPrice, .75),
            (d => d.CalculateWeightedClose(), new WeightedCloseState(), InputSeries.WeightedClose, -maximum / 4)
        };
        foreach (var route in routes)
        {
            Assert.Equal(new[] { maximum, route.Expected }, route.Legacy(Data()).CustomValuesList);
            var first = new OhlcvBar("X", BarTimeframe.Minutes(1), time, time, maximum, maximum, maximum, maximum, 1, true);
            var second = new OhlcvBar("X", BarTimeframe.Minutes(1), time.AddMinutes(1), time.AddMinutes(1), maximum, 1, -maximum, 2, 1, true);
            Assert.Equal(maximum, route.Preset.Next(first, true));
            Assert.Equal(route.Expected, route.Preset.Next(second, true));
            Assert.Equal(maximum, route.Native.Update(first, true, true).Value);
            Assert.Equal(route.Expected, route.Native.Update(second, false, true).Value);
            Assert.Equal(route.Expected, route.Native.Update(second, true, true).Value);
        }
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    [InlineData(6)]
    [InlineData(7)]
    public async Task EveryPriceMeanReceivesAllNumericalInputClasses(int kind)
    {
        IIndicator Create() => kind switch
        {
            0 => new AveragePrice(14), 1 => new MedianPrice(14), 2 => new TypicalPrice(14),
            3 => new FullTypicalPrice(14), 4 => new WeightedClose(14),
            5 => new MidRange(), 6 => new OhlcAverage(), _ => new HlcAverage()
        };
        var report = await IndicatorValidation.ValidateAsync(new(Create().GetType(), "numerical-classes", Create));
        report.ThrowIfInvalid();
        foreach (var fixture in IndicatorAdversarialCases.Generate(256, 244))
            Assert.Contains(report.FixtureEvidence, evidence => evidence.Name == fixture.Name && evidence.Passed);
    }

    [Fact]
    public void MeansRetainRepresentableResidualsAndSubnormals()
    {
        Assert.Equal(1d / 3, PriceMean.Of(double.MaxValue, 1, -double.MaxValue));
        Assert.Equal(.75, PriceMean.Of(double.MaxValue, 1, -double.MaxValue, 2));
        Assert.Equal(1d / 3, PriceMean.Of(1e100, 1, -1e100));
        Assert.Equal(3.02500002500381e91, PriceMean.Of(1e100, 1e84, -9.9989e99, -1.0998790000010039e96));
        Assert.Equal(double.MaxValue, PriceMean.Of(double.MaxValue, double.MaxValue));
        Assert.Equal(double.Epsilon, PriceMean.Of(double.Epsilon, double.Epsilon, double.Epsilon));
        Assert.Equal(0, PriceMean.Of(double.Epsilon, 0));
        Assert.Equal(2 * double.Epsilon, PriceMean.Of(3 * double.Epsilon, 0));
    }

    [Fact]
    public void CorePriceMeansHandleMixedFieldsWithoutOverflowOrLostResiduals()
    {
        double[] open = [double.MaxValue, double.MaxValue];
        double[] high = [double.MaxValue, 1];
        double[] low = [double.MaxValue, -double.MaxValue];
        double[] close = [double.MaxValue, 2];
        var output = new double[2];
        OscillatorCore.FullTypicalPrice(open, high, low, close, output);
        Assert.Equal(new[] { double.MaxValue, .75 }, output);
        TrendCore.TypicalPrice(open, high, low, output);
        Assert.Equal(new[] { double.MaxValue, 1d / 3 }, output);
        TrendCore.MedianPrice(open, low, output);
        Assert.Equal(new[] { double.MaxValue, 0d }, output);
        TrendCore.AveragePrice(open, low, output);
        Assert.Equal(new[] { double.MaxValue, 0d }, output);
        TrendCore.WeightedClose(open, low, close, output);
        Assert.Equal(new[] { double.MaxValue, 1d }, output);
    }
}

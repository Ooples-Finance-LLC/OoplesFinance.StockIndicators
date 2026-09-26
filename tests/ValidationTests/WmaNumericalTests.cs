using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Core;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Streaming;
using OoplesFinance.StockIndicators.Validation;

namespace OoplesFinance.StockIndicators.Tests.Unit.ValidationTests;

public sealed class WmaNumericalTests
{
    [Theory]
    [InlineData(1)]
    [InlineData(3)]
    [InlineData(14)]
    public async Task AllNamesAutomaticallyReceiveAllNumericalClasses(int period)
    {
        foreach (var indicator in new IIndicator[] { new Wma(period), new LinearWeightedMovingAverageCore(period), new SimplifiedWeightedMovingAverage(period) })
        {
            var type = indicator.GetType();
            var report = await IndicatorValidation.ValidateAsync(new(type, "numerical-windows",
                () => type == typeof(Wma) ? new Wma(period) : type == typeof(LinearWeightedMovingAverageCore)
                    ? new LinearWeightedMovingAverageCore(period) : new SimplifiedWeightedMovingAverage(period)));
            report.ThrowIfInvalid();
            foreach (var fixture in IndicatorAdversarialCases.Generate(256, 244))
                Assert.Contains(report.FixtureEvidence, f => f.Name == fixture.Name && f.Completed && f.Passed);
        }
    }

    public static IEnumerable<object[]> Windows()
    {
        foreach (var period in new[] { 1, 2, 3, 14, 37 })
            foreach (var fixture in IndicatorAdversarialCases.Generate(96, 244))
                yield return new object[] { period, fixture.Bars.Select(b => b.Close).ToArray() };
        yield return new object[] { 3, new[] { double.MaxValue, double.MaxValue, double.MaxValue, 0d, 0d, 0d, 1d } };
        yield return new object[] { 3, new[] { 3d, 6d, 9d, 12d } };
    }

    [Theory]
    [MemberData(nameof(Windows))]
    public async Task EveryWindowMatchesAnIndependentExactWeightedSum(int period, double[] values)
    {
        var expected = new double[values.Length];
        for (var i = 0; i < values.Length; i++)
        {
            var sum = new ReferenceFraction(0);
            for (var age = 0; age < period && age <= i; age++)
                sum += ReferenceFraction.FromDouble(values[i - age]) * new ReferenceFraction(period - age);
            expected[i] = (sum / new ReferenceFraction((long)period * (period + 1L) / 2)).ToDouble();
        }
        var actual = new double[values.Length];
        MovingAverageCore.WeightedMovingAverage(values, actual, period);
        Assert.Equal(expected, actual);
        MovingAverageCore.SimplifiedWeightedMovingAverage(values, actual, period);
        Assert.Equal(expected, actual);
        var bars = values.Select((v, i) => new Bar(DateTime.UnixEpoch.AddMinutes(i), v, v, v, v, 1)).ToArray();
        var wma = new Wma(period);
        var linear = new LinearWeightedMovingAverageCore(period);
        var simplified = new SimplifiedWeightedMovingAverage(period);
        using var run = await new StockIndicatorBuilder().ConfigureSource(Bars.From(bars)).ConfigureIndicators(wma, linear, simplified).BuildAsync();
        Assert.Equal(expected, run[wma].ToArray());
        Assert.Equal(expected, run[linear].ToArray());
        Assert.Equal(expected, run[simplified].ToArray());
        StockData Data() => new(values.ToList(), values.ToList(), values.ToList(), values.ToList(),
            values.Select(_ => 1d).ToList(), bars.Select(b => b.Time).ToList());
        Assert.Equal(expected, Data().CalculateWeightedMovingAverage(period).CustomValuesList);
        Assert.Equal(expected, Data().CalculateLinearWeightedMovingAverage(period).CustomValuesList);
        Assert.Equal(expected, Data().CalculateSimplifiedWeightedMovingAverage(period).CustomValuesList);
        using var simplifiedState = new SimplifiedWeightedMovingAverageState(period);
        using var state = new WeightedMovingAverageState(period);
        using var alias = new LinearWeightedMovingAverageState(period);
        using var smoother = new WeightedMovingAverageSmoother(period);
        for (var replay = 0; replay < 2; replay++)
        {
            state.Reset(); alias.Reset(); smoother.Reset(); simplifiedState.Reset();
            for (var i = 0; i < values.Length; i++)
            {
                var b = bars[i];
                var input = new OhlcvBar("WMA", BarTimeframe.Minutes(1), b.Time, b.Time, b.Open, b.High, b.Low, b.Close, b.Volume, true);
                smoother.Next(double.MaxValue, false);
                Assert.Equal(expected[i], smoother.Next(values[i], false));
                Assert.Equal(expected[i], smoother.Next(values[i], true));
                Assert.Equal(expected[i], state.Update(input, false, true).Value);
                Assert.Equal(expected[i], state.Update(input, true, true).Value);
                Assert.Equal(expected[i], alias.Update(input, false, true).Value);
                Assert.Equal(expected[i], alias.Update(input, true, true).Value);
                Assert.Equal(expected[i], simplifiedState.Update(input, false, true).Value);
                Assert.Equal(expected[i], simplifiedState.Update(input, true, true).Value);
            }
        }
    }
}


using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Core;
using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Streaming;
using OoplesFinance.StockIndicators.Validation;

namespace OoplesFinance.StockIndicators.Tests.Unit.ValidationTests;

public sealed class PopulationDeviationNumericalTests
{
    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(14)]
    public async Task AutomaticNumericalClassesCoverBothOutputs(int period)
    {
        foreach (var kind in new[] { MovingAvgType.SimpleMovingAverage, MovingAvgType.WeightedMovingAverage, MovingAvgType.ExponentialMovingAverage })
        {
            var report = await IndicatorValidation.ValidateAsync(new(typeof(StandardDevation), "numerical-deviation",
                () => new StandardDevation(period, kind)));
            report.ThrowIfInvalid();
            foreach (var fixture in IndicatorAdversarialCases.Generate(256, 244))
                Assert.Contains(report.FixtureEvidence, f => f.Name == fixture.Name && f.Completed && f.Passed);
        }
    }

    public static IEnumerable<object[]> Windows()
    {
        foreach (var period in new[] { 1, 2, 3, 14 })
        foreach (var fixture in IndicatorAdversarialCases.Generate(40, 244))
            yield return new object[] { period, fixture.Bars.Select(b => b.Close).ToArray() };
        yield return new object[] { 2, new[] { double.MaxValue, -double.MaxValue, 0d, 1d, 2d } };
        yield return new object[] { 2, new[] { 0d, double.Epsilon, 3 * double.Epsilon, -double.Epsilon, 0d } };
        yield return new object[] { 3, new[] { 1e-200, 2e-200, 3e-200, 1e200, -1e200, 0d, 1d, 2d, 3d } };
    }

    [Theory]
    [MemberData(nameof(Windows))]
    public async Task BatchNativePreviewAndResetMatchIndependentCenteredRationals(int period, double[] values)
    {
        var expected = BuiltInFormulaReferences.PopulationDeviation(values, period);
        var actual = new double[values.Length];
        VolatilityCore.StandardDeviation(values, actual, period);
        for (var i = 0; i < values.Length; i++) Close(expected[i], actual[i]);
        var bars = values.Select((v, i) => new Bar(DateTime.UnixEpoch.AddMinutes(i), v, v, v, v, 1)).ToArray();
        var indicator = new StandardDevation(period, MovingAvgType.SimpleMovingAverage);
        using var run = await new StockIndicatorBuilder().ConfigureSource(Bars.From(bars)).ConfigureIndicators(indicator).BuildAsync();
        Assert.Equal(actual, run[indicator].ToArray());
        var stock = new StockData(values.ToList(), values.ToList(), values.ToList(), values.ToList(),
            values.Select(_ => 1d).ToList(), bars.Select(b => b.Time).ToList());
        Assert.Equal(actual, stock.CalculateStandardDevation(length: period).CustomValuesList);
        using var state = new StandardDeviationState(length: period);
        for (var replay = 0; replay < 2; replay++)
        {
            state.Reset();
            for (var i = 0; i < values.Length; i++)
            {
                var b = bars[i];
                var input = new OhlcvBar("STD", BarTimeframe.Minutes(1), b.Time, b.Time, b.Open, b.High, b.Low, b.Close, b.Volume, true);
                Assert.Equal(actual[i], state.Update(input, false, true).Value);
                Assert.Equal(actual[i], state.Update(input, true, true).Value);
            }
        }
    }

    [Theory]
    [InlineData(0, 1, 0)]
    [InlineData(0, 3, 2)]
    [InlineData(-1, 2, 2)]
    public void SubnormalMidpointsRoundToEven(int first, int second, int expected)
    {
        var exact = new ExactPopulationDeviation();
        exact.Add(first * double.Epsilon); exact.Add(second * double.Epsilon);
        Assert.Equal(expected * double.Epsilon, exact.Value());
    }

    [Fact]
    public void ExactFallbackMatchesIndependentOracleAcrossExponentGrid()
    {
        var random = new Random(244);
        for (var trial = 0; trial < 100; trial++)
        {
            var values = Enumerable.Range(0, 2 + trial % 7).Select(_ =>
                BitConverter.Int64BitsToDouble(((long)random.Next(2047) << 52) | (long)random.Next())
                * (random.Next(2) == 0 ? -1 : 1)).ToArray();
            var exact = new ExactPopulationDeviation();
            foreach (var value in values) exact.Add(value);
            Assert.Equal(BuiltInFormulaReferences.PopulationDeviation(values, values.Length).Last(), exact.Value());
        }
    }

    private static void Close(double expected, double actual)
    {
        Assert.True(double.IsFinite(actual));
        if (expected == 0 || Math.Abs(expected) < 2.2250738585072014E-308) Assert.Equal(expected, actual);
        else Assert.True(Math.Abs((actual - expected) / expected) <= 1e-12, $"Expected {expected:R}; actual {actual:R}");
    }
}

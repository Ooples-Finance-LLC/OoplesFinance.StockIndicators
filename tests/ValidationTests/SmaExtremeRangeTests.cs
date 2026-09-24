using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Streaming;
using OoplesFinance.StockIndicators.Validation;

namespace OoplesFinance.StockIndicators.Tests.Unit.ValidationTests;

public sealed class SmaExtremeRangeTests
{
    [Theory]
    [InlineData(1)]
    [InlineData(14)]
    public async Task SmaAutomaticallyReceivesEveryDeclaredNumericalInputClass(int period)
    {
        var report = await IndicatorValidation.ValidateAsync(new(typeof(Sma), "extreme-domain", () => new Sma(period)));
        report.ThrowIfInvalid();
        foreach (var fixture in IndicatorAdversarialCases.Generate(256, 244))
            Assert.Contains(report.FixtureEvidence, evidence => evidence.Name == fixture.Name && evidence.Passed);
    }

    public static IEnumerable<object[]> AdjacentValues()
    {
        foreach (var bits in new[] { 0L, 1L, 2L, 0xfffffffffffffL, 0x10000000000000L,
            0x3fefffffffffffffL, 0x3ff0000000000000L, 0x7feffffffffffffeL })
            foreach (var sign in new[] { 1, -1 }) yield return new object[] { bits, sign };
    }

    [Theory]
    [MemberData(nameof(AdjacentValues))]
    public void ExactMidpointsRoundToTheEvenNeighborAcrossExponentBoundaries(long lowerBits, int sign)
    {
        var sum = new ExactMeanAccumulator();
        sum.Add(sign * BitConverter.Int64BitsToDouble(lowerBits));
        sum.Add(sign * BitConverter.Int64BitsToDouble(lowerBits + 1));
        var expected = sign * BitConverter.Int64BitsToDouble((lowerBits & 1) == 0 ? lowerBits : lowerBits + 1);
        Assert.Equal(BitConverter.DoubleToInt64Bits(expected), BitConverter.DoubleToInt64Bits(sum.Mean(2)));
    }

    public static IEnumerable<object[]> Cases()
    {
        var maximum = double.MaxValue;
        yield return new object[] { 1, new[] { maximum, maximum, -maximum, 1d }, new[] { maximum, maximum, -maximum, 1d } };
        yield return new object[] { 2, new[] { maximum, maximum, 0, 0, 2d, 4d }, new[] { 0, maximum, maximum / 2, 0, 1d, 3d } };
        yield return new object[] { 3, new[] { maximum, maximum, maximum, -maximum, -maximum, -maximum },
            new[] { 0, 0, maximum, maximum / 3, -maximum / 3, -maximum } };
        yield return new object[] { 7, Enumerable.Repeat(-maximum, 9).ToArray(),
            Enumerable.Repeat(0d, 6).Concat(Enumerable.Repeat(-maximum, 3)).ToArray() };
        yield return new object[] { 3, new[] { maximum, 1d, -maximum }, new[] { 0, 0, 1d / 3 } };
        yield return new object[] { 4, new[] { maximum, 1d, -maximum, 2d }, new[] { 0, 0, 0, .75 } };
        yield return new object[] { 3, new[] { 1e100, 1d, -1e100 }, new[] { 0, 0, 1d / 3 } };
        // Neither individual cancellation meets the old 1e-4 trigger, yet their cumulative
        // effect used to exceed the declared 1e-9 relative budget by nearly eight times.
        yield return new object[] { 4, new[] { 1e100, 1e84, -9.9989e99, -1.0998790000010039e96 },
            new[] { 0, 0, 0, 3.02500002500381e91 } };
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public async Task RepresentableMeansSurviveOverflowingSumsAndWindowEviction(int period, double[] prices, double[] expected)
    {
        var bars = prices.Select((price, i) => new Bar(DateTime.UnixEpoch.AddMinutes(i), price, price, price, price, 1)).ToArray();
        var indicator = new Sma(period);
        using var run = await new StockIndicatorBuilder().ConfigureSource(Bars.From(bars)).ConfigureIndicators(indicator).BuildAsync();
        Assert.Equal(expected, run[indicator].ToArray());
        var legacy = new StockData(prices.ToList(), prices.ToList(), prices.ToList(), prices.ToList(),
            prices.Select(_ => 1d).ToList(), bars.Select(bar => bar.Time).ToList());
        Assert.Equal(expected, legacy.CalculateSimpleMovingAverage(period).CustomValuesList);
        using var state = new SimpleMovingAverageState(period);
        using var smoother = new SimpleMovingAverageSmoother(period);
        for (var replay = 0; replay < 2; replay++)
        {
            state.Reset(); smoother.Reset();
            for (var i = 0; i < bars.Length; i++)
            {
                var bar = bars[i];
                var input = new OhlcvBar("AAPL", BarTimeframe.Minutes(1), bar.Time, bar.Time,
                    bar.Open, bar.High, bar.Low, bar.Close, bar.Volume, true);
                Assert.Equal(expected[i], state.Update(input, false, true).Value);
                Assert.Equal(expected[i], state.Update(input, false, true).Value);
                Assert.Equal(expected[i], state.Update(input, true, true).Value);
                Assert.Equal(expected[i], smoother.Next(bar.Close, false));
                Assert.Equal(expected[i], smoother.Next(bar.Close, true));
            }
        }
    }

    [Fact]
    public void OverflowingPreviewCannotContaminateTheCommittedWindow()
    {
        using var smoother = new SimpleMovingAverageSmoother(2);
        Assert.Equal(0, smoother.Next(double.MaxValue, true));
        Assert.Equal(double.MaxValue, smoother.Next(double.MaxValue, false));
        Assert.Equal(0, smoother.Next(-double.MaxValue, true));
        Assert.Equal(-double.MaxValue / 2, smoother.Next(0, true));
    }

    [Theory]
    [InlineData(1L, 2, 0L)] // Half of the smallest subnormal rounds to even zero.
    [InlineData(3L, 2, 2L)]
    [InlineData(-1L, 2, long.MinValue)] // Negative underflow retains its sign.
    [InlineData(-3L, 2, unchecked(long.MinValue + 2))]
    public void ExactFallbackRoundsSubnormalTiesToEven(long units, int divisor, long expectedBits)
    {
        var sum = new ExactMeanAccumulator();
        sum.Add(units * double.Epsilon);
        Assert.Equal(expectedBits, BitConverter.DoubleToInt64Bits(sum.Mean(divisor)));
    }

    [Fact]
    public void ExactFallbackRetainsSmallValuesAfterCancellationAndRoundsNormalTies()
    {
        var sum = new ExactMeanAccumulator();
        foreach (var value in new[] { double.MaxValue, 1d, -double.MaxValue }) sum.Add(value);
        Assert.Equal(1d / 3, sum.Mean(3));
        sum = new();
        sum.Add(1);
        sum.Add(BitConverter.Int64BitsToDouble(BitConverter.DoubleToInt64Bits(1d) + 1));
        Assert.Equal(1, sum.Mean(2));
    }
}

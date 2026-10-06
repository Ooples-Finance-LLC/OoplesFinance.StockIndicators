using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Streaming;
using OoplesFinance.StockIndicators.Validation;

namespace OoplesFinance.StockIndicators.Tests.Unit.ValidationTests;

public sealed class PairedNumericalTests
{
    private static readonly SeriesKey Primary = new("P", BarTimeframe.Minutes(1));
    private static readonly SeriesKey Market = new("M", BarTimeframe.Minutes(1));
    private static OhlcvBar Bar(SeriesKey key, int i, double value) => new(key.Symbol, key.Timeframe,
        DateTime.UnixEpoch.AddMinutes(i), DateTime.UnixEpoch.AddMinutes(i), value, value, value, value, 1, true);

    public static IEnumerable<object[]> Cases => IndicatorValidationDiscovery.DiscoverMultiSeries(new[] { typeof(IIndicator).Assembly })
        .Select(c => new object[] { c });

    [Theory, MemberData(nameof(Cases))]
    public void EveryConfigurationExecutesAllPairedNumericalClasses(MultiSeriesIndicatorValidationCase testCase)
    {
        var report = MultiSeriesIndicatorValidation.Validate(testCase, new IndicatorValidationOptions { BarsPerFixture = 32 });
        report.ThrowIfInvalid();
        foreach (var fixture in IndicatorAdversarialCases.Generate(32, 244))
        foreach (var shape in new[] { "identical", "constant", "independent", "scaled", "zero" })
            Assert.Single(report.FixtureEvidence, f => f.Name == fixture.Name + "/" + shape && f.Passed && f.Completed);
    }

    [Fact]
    public void IdenticalPriceMomentumLegsCancelBeyondBinary64()
    {
        var state = new ComparePriceMomentumOscillatorState(Primary, Market, 2, 2);
        Assert.Equal(new[] { 0d, 0, 0 }, Replay(state, new[] { double.Epsilon, double.MaxValue, -double.MaxValue }));
    }

    [Fact]
    public void IdenticalSectorReturnsCancelBeforePublication()
    {
        using var state = new SectorRotationModelState(Primary, Market, MovingAvgType.ExponentialMovingAverage, 1, 2);
        Assert.Equal(new[] { 0d, 0, 0 }, Replay(state, new[] { double.Epsilon, double.MaxValue, -double.MaxValue }));
    }

    [Fact]
    public void RelativeStrengthVotesRemainFiniteForSubnormalAndExtendedRatios()
    {
        using var state = new RelativeStrength3DIndicatorState(Primary, Market, MovingAvgType.ExponentialMovingAverage, 1, 1, 1, 1, 1);
        Assert.Equal(new[] { 100d, 100 }, Replay(state, new[] { double.Epsilon, double.MaxValue }));
        state.Reset();
        var context = new MultiSeriesContext(new SeriesStore());
        state.Update(context, Market, Bar(Market, 0, double.Epsilon), true, false);
        Assert.Equal(100, state.Update(context, Primary, Bar(Primary, 0, double.MaxValue), true, false).Value);
    }

    [Fact]
    public void OppositeExtremePricesHaveUnitRelativeNormalizedVolatility()
    {
        using var state = new RelativeNormalizedVolatilityState(Primary, Market, MovingAvgType.SimpleMovingAverage, 2);
        Assert.Equal(new[] { 0d, 1 }, Replay(state, new[] { -double.MaxValue, double.MaxValue }));
    }

    [Fact]
    public void UnrepresentableOutputRejectsWithoutCommittingPrimaryState()
    {
        var state = new ComparePriceMomentumOscillatorState(Primary, Market, 2, 2);
        var context = new MultiSeriesContext(new SeriesStore());
        state.Update(context, Market, Bar(Market, 0, 1), true, false);
        state.Update(context, Primary, Bar(Primary, 0, double.Epsilon), true, false);
        state.Update(context, Market, Bar(Market, 1, 1), true, false);
        foreach (var final in new[] { false, true })
        {
            var error = Assert.Throws<IndicatorOutputException>(() => state.Update(context, Primary, Bar(Primary, 1, double.MaxValue), final, true));
            Assert.Equal(1, error.BarIndex); Assert.Equal(0, error.OutputSlot); Assert.Equal(double.PositiveInfinity, error.Value);
        }
        Assert.Equal(0, state.Update(context, Primary, Bar(Primary, 1, double.Epsilon), true, true).Value);
    }

    [Fact]
    public void PublicBatchRoutesUseTheSameExtendedComponents()
    {
        StockData Data(double[] values) => new(values,values,values,values,values.Select(_=>1d),
            values.Select((_,i)=>DateTime.UnixEpoch.AddMinutes(i)));
        var extreme = new[] { double.Epsilon, double.MaxValue, -double.MaxValue };
        Assert.Equal(new[] {0d,0,0},Data(extreme).CalculateComparePriceMomentumOscillator(Data(extreme),length1:2,length2:2).CustomValuesList);
        Assert.Equal(new[] {0d,0,0},Data(extreme).CalculateSectorRotationModel(Data(extreme),length1:1,length2:2).CustomValuesList);
        Assert.Equal(new[] {100d,100,100},Data(extreme).CalculateRelativeStrength3DIndicator(Data(extreme),length1:1,length2:1,length3:1,length4:1,length5:1).CustomValuesList);
        var opposite = new[] {-double.MaxValue,double.MaxValue};
        Assert.Equal(new[] {0d,1},Data(opposite).CalculateRelativeNormalizedVolatility(Data(opposite),length:2).CustomValuesList);
    }

    private static double[] Replay(IMultiSeriesIndicatorState state, double[] prices)
    {
        var context = new MultiSeriesContext(new SeriesStore());
        var results = new double[prices.Length];
        for (var pass = 0; pass < 2; pass++)
        {
            state.Reset();
            for (var i = 0; i < prices.Length; i++)
            {
                state.Update(context, Market, Bar(Market, i, prices[i]), true, true);
                var preview = state.Update(context, Primary, Bar(Primary, i, prices[i]), false, true);
                var actual = state.Update(context, Primary, Bar(Primary, i, prices[i]), true, true);
                Assert.True(actual.HasValue); Assert.Equal(preview.Value, actual.Value);
                if (pass == 0) results[i] = actual.Value; else Assert.Equal(results[i], actual.Value);
            }
        }
        return results;
    }
}

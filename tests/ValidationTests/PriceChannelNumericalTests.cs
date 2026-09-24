using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Builder.Specs;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Streaming;
using OoplesFinance.StockIndicators.Validation;

namespace OoplesFinance.StockIndicators.Tests.Unit.ValidationTests;

public sealed class PriceChannelNumericalTests
{
    public static IEnumerable<object[]> Cases => IndicatorValidationDiscovery.Discover(new[] { typeof(IIndicator).Assembly })
        .Where(c => c.IndicatorType == typeof(PriceChannel) || c.IndicatorType == typeof(PriceChannelMiddle) || c.IndicatorType == typeof(PriceChannelUpper) || c.IndicatorType == typeof(PriceChannelLower)).Select(c => new object[] { c });

    [Theory, MemberData(nameof(Cases))]
    public async Task EveryConfigurationReceivesEveryNumericalClass(IndicatorValidationCase testCase)
    {
        var report = await IndicatorValidation.ValidateAsync(testCase);
        report.ThrowIfInvalid();
        foreach (var sign in new[] { "positive", "negative" })
            Assert.Contains(report.FixtureEvidence, f => f.Name == "price-channel-" + sign + "-output-overflow" && f.Completed && f.Passed);
        foreach (var fixture in IndicatorAdversarialCases.Generate(256, 244))
            Assert.Contains(report.FixtureEvidence, f => f.Name == fixture.Name && f.Completed && f.Passed);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(3)]
    [InlineData(14)]
    public async Task AllStagesMatchTheIndependentReferenceAcrossRoutes(int length)
    {
        var averages = new (IMovingAverage Average, int Kind)[] {
            (new Sma(),1), (new Wma(),2), (new Ema(),3), (new Wwma(),6),
            (new SymmetricallyWeightedMovingAverage(),7), (new FibonacciWeightedMovingAverage(),8),
            (new SquareRootWeightedMovingAverage(),9), (new ParabolicWma(),10), (new CubedWeightedMovingAverage(),11),
            (new QuickMovingAverage(),12), (new JsaMovingAverage(),13), (new QuadraticMovingAverage(),14),
            (new Kama(),15), (new SineWma(),16), (new NaturalMa(),17), (new EhlersHannMovingAverage(),18), (new Vidya(),19) };
        foreach (var fixture in IndicatorAdversarialCases.Generate(32, 244).Concat(new[] {
            Fixture("bands-overflow-middle-finite", Enumerable.Repeat(double.MaxValue, 32).ToArray()),
            Fixture("band-small-contribution", new[] { 1e100, 1e100, 1e100, double.Epsilon, double.Epsilon }) }))
        foreach (var (average, kind) in averages)
        {
            var bars = fixture.Bars;
            var expected = BuiltInFormulaReferences.RoundedPriceChannel(bars, length, kind, .06);
            IIndicator indicator = new PriceChannel(length, .06, average);
            var builtIn = (IBuiltInIndicator)indicator;
            var keys = indicator.Outputs.Count == 1 ? new[] { builtIn.BatchOutputKey ?? "MiddleChannel" } : GeneratedIndicatorOutputs.KeysFor(builtIn.BatchName);
            if (keys.Any(key => expected[key].Any(double.IsInfinity)))
                await Assert.ThrowsAsync<IndicatorOutputException>(() => new StockIndicatorBuilder().ConfigureSource(Bars.From(bars)).ConfigureIndicators(indicator).BuildAsync());
            else
            {
                using var run = await new StockIndicatorBuilder().ConfigureSource(Bars.From(bars)).ConfigureIndicators(indicator).BuildAsync();
                for (var slot = 0; slot < keys.Count; slot++) Assert.Equal(expected[keys[slot]], run[indicator.Outputs[slot]].ToArray());
            }
            var data = new StockData(bars.Select(b => b.Open), bars.Select(b => b.High), bars.Select(b => b.Low),
                bars.Select(b => b.Close), bars.Select(b => b.Volume), bars.Select(b => b.Time));
            var maType = ((IBuiltInMovingAverage)average).AvgType;
            data.CalculatePriceChannel(maType, length, .06);
            foreach (var (key, values) in expected) Assert.Equal(values, data.OutputValues[key]);
            var spec = IndicatorSpecs.Create(builtIn.BatchName, builtIn.CreateOptions());
            foreach (var state in new[] { StatefulIndicatorFactory.Create(spec), StreamingIndicatorFactory.CreateState(spec) })
            {
                Assert.NotNull(state);
                using var lifetime = state as IDisposable;
                for (var replay = 0; replay < 2; replay++)
                {
                    state.Reset();
                    for (var i = 0; i < bars.Count; i++)
                    foreach (var commit in new[] { false, true })
                    {
                        var b = bars[i];
                        var native = new OhlcvBar("FAST", BarTimeframe.Minutes(1), b.Time, b.Time, b.Open, b.High, b.Low, b.Close, b.Volume, true);
                        var result = state.Update(native, commit, true);
                        foreach (var (key, values) in expected) Assert.Equal(values[i], result.Outputs![key]);
                    }
                }
            }
        }
    }

    [Fact]
    public void PercentageBandsRoundOnceAndDoNotCreateAnIntermediateInfinity()
    {
        Assert.Equal(double.MaxValue, OoplesFinance.StockIndicators.Helpers.RoundedPercentageBand.Of(double.MaxValue, 0, 1));
        Assert.Equal(0, OoplesFinance.StockIndicators.Helpers.RoundedPercentageBand.Of(double.MaxValue, 1, -1));
        Assert.Equal(double.MaxValue, OoplesFinance.StockIndicators.Helpers.RoundedPercentageBand.Of(double.MaxValue / 2, 1, 1));
        var center = ReferenceFraction.FromDouble(1e100);
        var fraction = ReferenceFraction.FromDouble(1e-16);
        foreach (var direction in new[] { 1, -1 })
            Assert.Equal((center * (new ReferenceFraction(1) + new ReferenceFraction(direction) * fraction)).ToDouble(),
                OoplesFinance.StockIndicators.Helpers.RoundedPercentageBand.Of(1e100, 1e-16, direction));
    }

    [Fact]
    public async Task SelectedPricesAndAliasOutputsArePreserved()
    {
        var bars = Fixture("selection", new[] { 100d, 120, 110, 150, 90 }).Bars;
        var source = new Sma(2);
        IIndicator[] indicators = { new PriceChannel(3).Of(source), new PriceChannelMiddle(3).Of(source),
            new PriceChannelUpper(3).Of(source), new PriceChannelLower(3).Of(source) };
        using var run = await new StockIndicatorBuilder().ConfigureSource(Bars.From(bars)).ConfigureIndicators(indicators.Prepend(source).ToArray()).BuildAsync();
        var expected = BuiltInFormulaReferences.RoundedPriceChannel(Fixture("selected", run[source].ToArray()).Bars, 3, 3, .06);
        foreach (var indicator in indicators)
        {
            var builtIn = (IBuiltInIndicator)indicator;
            var keys = indicator.Outputs.Count == 1 ? new[] { builtIn.BatchOutputKey ?? "MiddleChannel" } : GeneratedIndicatorOutputs.KeysFor(builtIn.BatchName);
            for (var slot = 0; slot < keys.Count; slot++) Assert.Equal(expected[keys[slot]], run[indicator.Outputs[slot]].ToArray());
        }
    }

    private static IndicatorValidationFixture Fixture(string name, double[] values) => new(name,
        values.Select((v, i) => new Bar(DateTime.UnixEpoch.AddMinutes(i), v, v, v, v, 1)));
}

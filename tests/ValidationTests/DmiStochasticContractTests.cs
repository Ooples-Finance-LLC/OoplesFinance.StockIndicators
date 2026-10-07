using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Builder.Compute;
using OoplesFinance.StockIndicators.Builder.Specs;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Streaming;
using OoplesFinance.StockIndicators.Validation;

namespace OoplesFinance.StockIndicators.Tests.Unit.ValidationTests;

public sealed class DmiStochasticContractTests
{
    public static IEnumerable<object[]> Cases => IndicatorValidationDiscovery.Discover(new[] { typeof(IIndicator).Assembly })
        .Where(c => c.IndicatorType == typeof(DMIStochastic)).Select(c => new object[] { c });

    [Theory]
    [InlineData(MovingAvgType.ExponentialMovingAverage)]
    [InlineData(MovingAvgType.WildersSmoothingMethod)]
    [InlineData(MovingAvgType.DoubleExponentialMovingAverage)]
    public void SupportedAndFallbackSmoothersKeepNativeBatchParity(MovingAvgType kind)
    {
        var bars = IndicatorValidationFixtures.Create(48, 0, false).Single(f => f.Name == "walk-31").Bars;
        var data = new StockData(bars.Select(b => b.Open), bars.Select(b => b.High), bars.Select(b => b.Low),
            bars.Select(b => b.Close), bars.Select(b => b.Volume), bars.Select(b => b.Time));
        var expected = data.CalculateDMIStochastic(kind, 10).OutputValues["DmiStochastic"];
        using var state = new DMIStochasticState(kind, 10);
        for (var replay = 0; replay < 2; replay++)
        {
            state.Reset();
            for (var i = 0; i < bars.Count; i++)
            {
                var b = bars[i];
                var bar = new OhlcvBar("DMI", BarTimeframe.Minutes(1), b.Time, b.Time, b.Open, b.High, b.Low, b.Close, b.Volume, true);
                var preview = state.Update(bar, false, true).Outputs!["DmiStochastic"];
                var actual = state.Update(bar, true, true).Outputs!["DmiStochastic"];
                Assert.Equal(preview, actual);
                Assert.InRange(Math.Abs(actual - expected[i]), 0, 1e-9);
            }
        }
    }

    [Theory, MemberData(nameof(Cases))]
    public Task EveryDiscoveredConfigurationMatchesItsFormula(IndicatorValidationCase testCase) =>
        IndicatorValidation.ValidateAndThrowAsync(testCase);

    [Theory, MemberData(nameof(Cases))]
    public async Task NearlyFlatDirectionalSpreadsAgreeAcrossRoutesAndPreviews(IndicatorValidationCase testCase)
    {
        var indicator = testCase.Factory();
        var options = (DMIStochasticSpecOptions)((IBuiltInIndicator)indicator).CreateOptions();
        var rules = BuiltInFormulaReferences.For(indicator).ToArray();
        foreach (var fixture in IndicatorValidationFixtures.Create(48, 0, false)
            .Where(f => f.Name is "rising" or "falling" or "flat" or "walk-31"))
        {
            var bars = fixture.Bars;
            StockData Data() => new(bars.Select(b => b.Open), bars.Select(b => b.High), bars.Select(b => b.Low),
                bars.Select(b => b.Close), bars.Select(b => b.Volume), bars.Select(b => b.Time));
            void Check(string route, double[] actual)
            {
                foreach (var rule in rules)
                    rule.Check(new IndicatorValidationContext(testCase.Name + "/" + fixture.Name + "/" + route, bars, new[] { actual }, 0));
            }
            Check("batch", Data().CalculateDMIStochastic(options.MaType, options.Length).OutputValues["DmiStochastic"].ToArray());
            using var context = new ComputeContext();
            using var raw = IndicatorCompute.ComputeDMIStochasticFast(Data(), context, options.Length, options.MaType);
            Check("fast", raw.Span.ToArray());
            using var run = await new StockIndicatorBuilder().ConfigureSource(Bars.From(bars)).ConfigureIndicators(indicator).BuildAsync();
            Check("builder", run[indicator].ToArray());
            using var state = new DMIStochasticState(options.MaType, options.Length);
            for (var replay = 0; replay < 2; replay++)
            {
                state.Reset();
                var actual = new double[bars.Count];
                for (var i = 0; i < bars.Count; i++)
                {
                    var b = bars[i];
                    var bar = new OhlcvBar("DMI", BarTimeframe.Minutes(1), b.Time, b.Time, b.Open, b.High, b.Low, b.Close, b.Volume, true);
                    var preview = state.Update(bar, false, true).Outputs!["DmiStochastic"];
                    Assert.Equal(preview, state.Update(bar, false, true).Outputs!["DmiStochastic"]);
                    actual[i] = state.Update(bar, true, true).Outputs!["DmiStochastic"];
                    Assert.Equal(preview, actual[i]);
                }
                Check("native", actual);
            }
        }
    }
}

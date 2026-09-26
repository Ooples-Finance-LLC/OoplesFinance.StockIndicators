using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Builder.Compute;
using OoplesFinance.StockIndicators.Builder.Specs;
using OoplesFinance.StockIndicators.Core;
using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Streaming;
using OoplesFinance.StockIndicators.Validation;

namespace OoplesFinance.StockIndicators.Tests.Unit.ValidationTests;

public sealed class RegressionChannelNumericalTests
{
    public static IEnumerable<object[]> Cases => IndicatorValidationDiscovery.Discover(new[] { typeof(IIndicator).Assembly })
        .Where(c => c.IndicatorType == typeof(StandardDeviationChannel))
        .Select(c => new object[] { c });

    [Theory, MemberData(nameof(Cases))]
    public async Task EveryConfigurationReceivesEveryNumericalClass(IndicatorValidationCase testCase)
    {
        var report = await IndicatorValidation.ValidateAsync(testCase);
        report.ThrowIfInvalid();
        foreach (var fixture in IndicatorAdversarialCases.Generate(256, 244))
            Assert.Contains(report.FixtureEvidence, f => f.Name == fixture.Name && f.Completed && f.Passed);
    }

    [Theory, MemberData(nameof(Cases))]
    public void EveryRouteMatchesIndependentFit(IndicatorValidationCase testCase)
    {
        var builtIn = (IBuiltInIndicator)testCase.Factory();
        var options = builtIn.CreateOptions();
        var length = (int)options.GetType().GetProperty("Length")!.GetValue(options)!;
        var spec = new IndicatorSpec(builtIn.BatchName, options);
        foreach (var fixture in IndicatorAdversarialCases.Generate(32, 244))
        {
            var bars = fixture.Bars;
            var expected = BuiltInFormulaReferences.RoundedRegressionChannel(bars, length, 2);
            var prices = bars.Select(b => b.Close).ToArray();
            StockData Data() => new StockData(bars.Select(b => b.Open), bars.Select(b => b.High), bars.Select(b => b.Low),
                prices, bars.Select(b => b.Volume), bars.Select(b => b.Time));
            var actual = new double[bars.Count];
            VolatilityCore.StandardDeviationChannel(prices, actual, length);
            Assert.Equal(expected["MiddleBand"], actual);
            var legacy = Data().CalculateStandardDeviationChannel(length);
            foreach (var pair in expected) Assert.Equal(pair.Value, legacy.OutputValues[pair.Key]);
            using (var context = new ComputeContext())
            {
                foreach (var pair in new[] { ("MiddleBand", IndicatorCompute.ChannelBand.Middle),
                    ("UpperBand", IndicatorCompute.ChannelBand.Upper), ("LowerBand", IndicatorCompute.ChannelBand.Lower) })
                {
                    using var buffer = IndicatorCompute.ComputeStandardDeviationChannelFast(Data(), context, length, 2, pair.Item2);
                    Assert.Equal(expected[pair.Item1], buffer.ToArray());
                }
            }
            foreach (var state in new[] { StatefulIndicatorFactory.Create(spec), StreamingIndicatorFactory.CreateState(spec) })
            {
                Assert.NotNull(state);
                using var lifetime = state as IDisposable;
                for (var replay = 0; replay < 2; replay++)
                {
                    state.Reset();
                    for (var i = 0; i < bars.Count; i++)
                    {
                        var b = bars[i];
                        var native = new OhlcvBar("FIT", BarTimeframe.Minutes(1), b.Time, b.Time, b.Open, b.High, b.Low, b.Close, b.Volume, true);
                        foreach (var commit in new[] { false, true })
                        {
                            var outputs = state.Update(native, commit, true).Outputs!;
                            foreach (var pair in expected) Assert.Equal(pair.Value[i], outputs[pair.Key]);
                        }
                    }
                }
            }
        }
    }

    [Fact]
    public async Task SelectedInputReachesAllChannelOutputs()
    {
        var bars = IndicatorAdversarialCases.Generate(40, 244).Single(f => f.Name.EndsWith("/negative")).Bars;
        var source = new Sma(2);
        var indicator = new StandardDeviationChannel(3).Of(source);
        using var run = await new StockIndicatorBuilder().ConfigureSource(Bars.From(bars)).ConfigureIndicators(source, indicator).BuildAsync();
        var selected = run[source].ToArray();
        var projected = bars.Select((b, i) => new Bar(b.Time, selected[i], selected[i], selected[i], selected[i], b.Volume)).ToArray();
        var expected = BuiltInFormulaReferences.RoundedRegressionChannel(projected, 3, 2);
        for (var slot = 0; slot < 3; slot++) Assert.Equal(expected[new[] { "UpperBand", "MiddleBand", "LowerBand" }[slot]], run[indicator.Outputs[slot]].ToArray());
        foreach (var chained in new[] { false, true })
        {
            var data = new StockData(bars.Select(b => b.Open), bars.Select(b => b.High), bars.Select(b => b.Low),
                bars.Select(b => b.Close), bars.Select(b => b.Volume), bars.Select(b => b.Time));
            if (chained) data.CustomValuesList = selected.ToList(); else data.InputValues = selected.ToList();
            using var context = new ComputeContext();
            using var buffer = IndicatorCompute.ComputeStandardDeviationChannelFast(data, context, 3, 2, IndicatorCompute.ChannelBand.Lower);
            Assert.Equal(expected["LowerBand"], buffer.ToArray());
            var legacy = data.CalculateStandardDeviationChannel(3);
            foreach (var pair in expected) Assert.Equal(pair.Value, legacy.OutputValues[pair.Key]);
        }
    }

    [Theory]
    [InlineData(-2d)]
    [InlineData(0d)]
    [InlineData(0.5d)]
    [InlineData(double.MaxValue)]
    public void DirectMultiplierRoutesMatchIndependentBands(double multiplier)
    {
        var bars = IndicatorAdversarialCases.Generate(24, 244).Single(f => f.Name.EndsWith("/negative")).Bars;
        var expected = BuiltInFormulaReferences.RoundedRegressionChannel(bars, 3, multiplier);
        var data = new StockData(bars.Select(b => b.Open), bars.Select(b => b.High), bars.Select(b => b.Low),
            bars.Select(b => b.Close), bars.Select(b => b.Volume), bars.Select(b => b.Time));
        using var context = new ComputeContext();
        using var upper = IndicatorCompute.ComputeStandardDeviationChannelFast(data, context, 3, multiplier, IndicatorCompute.ChannelBand.Upper);
        using var lower = IndicatorCompute.ComputeStandardDeviationChannelFast(data, context, 3, multiplier, IndicatorCompute.ChannelBand.Lower);
        Assert.Equal(expected["UpperBand"], upper.ToArray()); Assert.Equal(expected["LowerBand"], lower.ToArray());
        var legacy = data.CalculateStandardDeviationChannel(3, multiplier);
        foreach (var pair in expected) Assert.Equal(pair.Value, legacy.OutputValues[pair.Key]);
        using var state = new StandardDeviationChannelState(3, multiplier);
        for (var i = 0; i < bars.Count; i++)
        {
            var v = bars[i];
            var bar = new OhlcvBar("BAND", BarTimeframe.Minutes(1), v.Time, v.Time, v.Open, v.High, v.Low, v.Close, v.Volume, true);
            var outputs = state.Update(bar, true, true).Outputs!;
            foreach (var pair in expected) Assert.Equal(pair.Value[i], outputs[pair.Key]);
        }
    }

    [Fact]
    public void OffsetRetainsFiniteResultsDespiteUnrepresentableComponents()
    {
        using var fit = new ExactLinearFitWindow(3);
        fit.Next(-double.MaxValue, true);
        fit.Next(double.MaxValue, true);
        var value = fit.Next(double.MaxValue, true);
        Assert.Equal(double.PositiveInfinity, value.Last);
        Assert.True(double.IsFinite(value.Offset(double.MaxValue, -1)));
        fit.Reset();
        value = fit.Next(-double.MaxValue, true);
        Assert.Equal(double.MaxValue, value.Offset(double.MaxValue, 2));
    }
}

using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Builder.Compute;
using OoplesFinance.StockIndicators.Builder.Specs;
using OoplesFinance.StockIndicators.Core;
using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Streaming;
using OoplesFinance.StockIndicators.Validation;

namespace OoplesFinance.StockIndicators.Tests.Unit.ValidationTests;

public sealed class LinearRegressionNumericalTests
{
    public static IEnumerable<object[]> Cases => IndicatorValidationDiscovery.Discover(new[] { typeof(IIndicator).Assembly })
        .Where(c => new[] { typeof(LinearChannelMiddle), typeof(LinReg), typeof(LinRegSlope), typeof(LinRegIntercept), typeof(LinearRegressionSlope), typeof(LinearRegressionIntercept) }.Contains(c.IndicatorType))
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
            var expected = BuiltInFormulaReferences.RoundedLinearRegression(bars, length);
            var prices = bars.Select(b => b.Close).ToArray();
            StockData Data() => new StockData(bars.Select(b => b.Open), bars.Select(b => b.High), bars.Select(b => b.Low),
                prices, bars.Select(b => b.Volume), bars.Select(b => b.Time));
            var actual = new double[bars.Count];
            MovingAverageCore.LinearRegression(prices, actual, length);
            Assert.Equal(expected["LinearRegression"], actual);
            TrendCore.LinearRegressionSlope(prices, actual, length);
            Assert.Equal(expected["Slope"], actual);
            var legacy = Data().CalculateLinearRegression(length);
            foreach (var pair in expected) Assert.Equal(pair.Value, legacy.OutputValues[pair.Key]);
            using (var context = new ComputeContext())
            {
                foreach (var pair in new[] { ("LinearRegression", IndicatorCompute.LinearRegressionSeries.Fit),
                    ("PredictedTomorrow", IndicatorCompute.LinearRegressionSeries.PredictedTomorrow),
                    ("Slope", IndicatorCompute.LinearRegressionSeries.Slope), ("Intercept", IndicatorCompute.LinearRegressionSeries.Intercept) })
                {
                    using var buffer = IndicatorCompute.ComputeLinRegFast(Data(), context, length, pair.Item2);
                    Assert.Equal(expected[pair.Item1], buffer.ToArray());
                }
                using var intercept = IndicatorCompute.ComputeLinRegInterceptFast(Data(), context, length);
                Assert.Equal(expected["Intercept"], intercept.ToArray());
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
    public async Task SelectedInputReachesAllAliases()
    {
        var bars = IndicatorAdversarialCases.Generate(40, 244).Single(f => f.Name.EndsWith("/negative")).Bars;
        var source = new Sma(2);
        foreach (IIndicator indicator in new IIndicator[] { new LinReg(3).Of(source), new LinRegSlope(3).Of(source), new LinRegIntercept(3).Of(source),
            new LinearRegressionSlope(3).Of(source), new LinearRegressionIntercept(3).Of(source), new LinearChannelMiddle(3).Of(source) })
        {
            using var run = await new StockIndicatorBuilder().ConfigureSource(Bars.From(bars)).ConfigureIndicators(source, indicator).BuildAsync();
            var selected = run[source].ToArray();
            var projected = bars.Select((b, i) => new Bar(b.Time, selected[i], selected[i], selected[i], selected[i], b.Volume)).ToArray();
            var expected = BuiltInFormulaReferences.RoundedLinearRegression(projected, 3);
            var builtIn = (IBuiltInIndicator)indicator;
            var key = builtIn.BatchOutputKey ?? "LinearRegression";
            Assert.Equal(expected[key], run[indicator.Outputs[0]].ToArray());
            foreach (var chained in new[] { false, true })
            {
                var data = new StockData(bars.Select(b => b.Open), bars.Select(b => b.High), bars.Select(b => b.Low),
                    bars.Select(b => b.Close), bars.Select(b => b.Volume), bars.Select(b => b.Time));
                if (chained) data.CustomValuesList = selected.ToList(); else data.InputValues = selected.ToList();
                using var context = new ComputeContext();
                using var buffer = key == "Slope" ? IndicatorCompute.ComputeLinRegSlopeFast(data, context, 3)
                    : key == "Intercept" ? IndicatorCompute.ComputeLinRegInterceptFast(data, context, 3)
                    : IndicatorCompute.ComputeLinRegFast(data, context, 3);
                Assert.Equal(expected[key], buffer.ToArray());
            }
        }
    }

    [Fact]
    public void FiniteFitSurvivesAnUnrepresentableSlopeAndForecast()
    {
        using var fit = new ExactLinearFitWindow(2);
        fit.Next(-double.MaxValue, true);
        var value = fit.Next(double.MaxValue, true);
        Assert.Equal(double.MaxValue, value.Last);
        Assert.Equal(double.PositiveInfinity, value.Slope);
        Assert.Equal(double.PositiveInfinity, value.Next);
        Assert.Equal(-double.MaxValue, value.GlobalIntercept);
        fit.Reset();
        fit.Next(double.Epsilon, true);
        value = fit.Next(0, true);
        Assert.Equal(0, value.Last);
        Assert.Equal(-double.Epsilon, value.Slope);
    }

    [Fact]
    public void ExactFitCancelsLargeOffsetsAndRecoversAfterExpiry()
    {
        using var fit = new ExactLinearFitWindow(3);
        for (var i = 0; i < 100; i++)
        {
            var value = fit.Next(1e16 + 2 * i, true);
            Assert.Equal(1e16 + 2 * i, value.Last);
            Assert.Equal(i == 0 ? 0 : 2, value.Slope);
            Assert.Equal(1e16, value.GlobalIntercept);
        }
        fit.Next(double.MaxValue, true);
        fit.Next(-double.MaxValue, true);
        for (var i = 0; i < 3; i++) fit.Next(7, true);
        var recovered = fit.Next(7, false);
        Assert.Equal(7, recovered.Last); Assert.Equal(0, recovered.Slope);
        Assert.Equal(7, recovered.GlobalIntercept);
    }
}

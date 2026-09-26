using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Builder.Compute;
using OoplesFinance.StockIndicators.Builder.Specs;
using OoplesFinance.StockIndicators.Core;
using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Streaming;
using OoplesFinance.StockIndicators.Validation;

namespace OoplesFinance.StockIndicators.Tests.Unit.ValidationTests;

public sealed class ChandeForecastNumericalTests
{
    [Fact]
    public void NormalizationPreservesScaleReflectionAndAnOverflowingHiddenFit()
    {
        foreach (var scale in new[] { double.Epsilon, 1d, Math.Pow(2, 500) })
        foreach (var sign in new[] { -1, 1 })
        {
            using var window = new ExactLinearFitWindow(3);
            Assert.Equal(0, window.Next(sign * scale, true).PercentResidual(sign * scale));
            Assert.Equal(0, window.Next(sign * 2 * scale, true).PercentResidual(sign * 2 * scale));
            Assert.Equal(25d / 6, window.Next(sign * 4 * scale, false).PercentResidual(sign * 4 * scale));
            Assert.Equal(25d / 6, window.Next(sign * 4 * scale, true).PercentResidual(sign * 4 * scale));
            Assert.Equal(0, window.Next(0, true).PercentResidual(0));
            window.Reset();
            Assert.Equal(0, window.Next(sign * scale, true).PercentResidual(sign * scale));
        }
        using var extremes = new ExactLinearFitWindow(3);
        extremes.Next(-double.MaxValue, true); extremes.Next(double.MaxValue, true);
        var fit = extremes.Next(double.MaxValue, true);
        Assert.Equal(double.PositiveInfinity, fit.Last);
        Assert.Equal(-100d / 3, fit.PercentResidual(double.MaxValue));
        extremes.Reset();
        extremes.Next(double.MaxValue, true); extremes.Next(double.MaxValue, true);
        Assert.Equal(double.NegativeInfinity, extremes.Next(double.Epsilon, true).PercentResidual(double.Epsilon));
        for (var i = 0; i < 3; i++) extremes.Next(7, true);
        Assert.Equal(0, extremes.Next(7, false).PercentResidual(7));
    }

    public static IEnumerable<object[]> Cases => IndicatorValidationDiscovery.Discover(new[] { typeof(IIndicator).Assembly })
        .Where(c => c.IndicatorType == typeof(ChandeForecastOscillator))
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
    public void EveryRouteMatchesIndependentNormalizedFit(IndicatorValidationCase testCase)
    {
        var indicator = testCase.Factory();
        var builtIn = (IBuiltInIndicator)indicator;
        var options = builtIn.CreateOptions();
        var length = (int)options.GetType().GetProperty("Length")!.GetValue(options)!;
        var key = "Cfo";
        var spec = new IndicatorSpec(builtIn.BatchName, options);
        foreach (var fixture in IndicatorAdversarialCases.Generate(32, 244))
        {
            var bars = fixture.Bars;
            var expected = BuiltInFormulaReferences.RoundedForecastOscillator(bars, length);
            var prices = bars.Select(b => b.Close).ToArray();
            var actual = new double[bars.Count];
            OscillatorCore.ChandeForecastOscillator(prices, actual, length);
            Assert.Equal(expected, actual);
            StockData Data() => new StockData(bars.Select(b => b.Open), bars.Select(b => b.High), bars.Select(b => b.Low),
                prices, bars.Select(b => b.Volume), bars.Select(b => b.Time));
            var legacy = Data().CalculateChandeForecastOscillator(length);
            Assert.Equal(expected, legacy.OutputValues[key]);
            using (var context = new ComputeContext())
            {
                using var buffer = IndicatorCompute.ComputeChandeForecastOscillatorFast(Data(), context, length);
                Assert.Equal(expected, buffer.ToArray());
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
                        var native = new OhlcvBar("ERROR", BarTimeframe.Minutes(1), b.Time, b.Time, b.Open, b.High, b.Low, b.Close, b.Volume, true);
                        foreach (var commit in new[] { false, true })
                            Assert.Equal(expected[i], state.Update(native, commit, true).Outputs![key]);
                    }
                }
            }
        }
    }

    [Fact]
    public async Task SelectedInputReachesBuilderAndLegacyRoutes()
    {
        var bars = IndicatorAdversarialCases.Generate(40, 244).Single(f => f.Name.EndsWith("/negative")).Bars;
        var source = new Sma(2);
        var indicator = new ChandeForecastOscillator(3).Of(source);
        using var run = await new StockIndicatorBuilder().ConfigureSource(Bars.From(bars)).ConfigureIndicators(source, indicator).BuildAsync();
        var selected = run[source].ToArray();
        var projected = bars.Select((b, i) => new Bar(b.Time, selected[i], selected[i], selected[i], selected[i], b.Volume)).ToArray();
        var expected = BuiltInFormulaReferences.RoundedForecastOscillator(projected, 3);
        Assert.Equal(expected, run[indicator.Outputs[0]].ToArray());
        foreach (var chained in new[] { false, true })
        {
            var data = new StockData(bars.Select(b => b.Open), bars.Select(b => b.High), bars.Select(b => b.Low),
                bars.Select(b => b.Close), bars.Select(b => b.Volume), bars.Select(b => b.Time));
            if (chained) data.CustomValuesList = selected.ToList(); else data.InputValues = selected.ToList();
            using var context = new ComputeContext();
            using var buffer = IndicatorCompute.ComputeChandeForecastOscillatorFast(data, context, 3);
            Assert.Equal(expected, buffer.ToArray());
            Assert.Equal(expected, data.CalculateChandeForecastOscillator(3).OutputValues["Cfo"]);
        }
    }
}

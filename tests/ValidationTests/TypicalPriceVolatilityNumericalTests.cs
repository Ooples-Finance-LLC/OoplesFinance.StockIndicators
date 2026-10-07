using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Builder.Compute;
using OoplesFinance.StockIndicators.Builder.Specs;
using OoplesFinance.StockIndicators.Core;
using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Streaming;
using OoplesFinance.StockIndicators.Validation;

namespace OoplesFinance.StockIndicators.Tests.Unit.ValidationTests;

public sealed class TypicalPriceVolatilityNumericalTests
{
    [Fact]
    public void MeanAndDeviationPreserveExtremeAndSubnormalValues()
    {
        foreach (var magnitude in new[] { double.Epsilon, 1d, double.MaxValue })
        {
            using var window = new ExactTypicalVolatilityWindow(2);
            Assert.Equal(0, window.Next(magnitude, magnitude, magnitude, true));
            Assert.Equal(magnitude, window.Next(-magnitude, -magnitude, -magnitude, false));
            Assert.Equal(0, window.Next(magnitude, magnitude, magnitude, false));
            Assert.Equal(magnitude, window.Next(-magnitude, -magnitude, -magnitude, true));
            Assert.Equal(magnitude, window.Next(magnitude, magnitude, magnitude, true));
            Assert.Equal(0, window.Next(magnitude, magnitude, magnitude, true));
            window.Reset();
            Assert.Equal(0, window.Next(-magnitude, -magnitude, -magnitude, true));
        }
        using var mixed = new ExactTypicalVolatilityWindow(2);
        mixed.Next(double.MaxValue, -double.MaxValue, 3, true);
        Assert.Equal(1, mixed.Next(double.MaxValue, -double.MaxValue, -3, true));
    }

    public static IEnumerable<object[]> Cases => IndicatorValidationDiscovery.Discover(new[] { typeof(IIndicator).Assembly })
        .Where(c => c.IndicatorType == typeof(TypicalPriceVolatility))
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
    public void EveryRouteMatchesIndependentPopulationDeviation(IndicatorValidationCase testCase)
    {
        var indicator = testCase.Factory();
        var builtIn = (IBuiltInIndicator)indicator;
        var options = builtIn.CreateOptions();
        var length = (int)options.GetType().GetProperty("Length")!.GetValue(options)!;
        var key = "Tpv";
        var spec = new IndicatorSpec(builtIn.BatchName, options);
        foreach (var fixture in IndicatorAdversarialCases.Generate(32, 244))
        {
            var bars = fixture.Bars;
            var expected = BuiltInFormulaReferences.RoundedTypicalVolatility(bars, length);
            var prices = bars.Select(b => b.Close).ToArray();
            var actual = new double[bars.Count];
            OscillatorCore.TypicalPriceVolatility(bars.Select(b => b.High).ToArray(), bars.Select(b => b.Low).ToArray(), prices, actual, length);
            Assert.Equal(expected, actual);
            StockData Data() => new StockData(bars.Select(b => b.Open), bars.Select(b => b.High), bars.Select(b => b.Low),
                prices, bars.Select(b => b.Volume), bars.Select(b => b.Time));
            var legacy = Data().CalculateTypicalPriceVolatility(length);
            Assert.Equal(expected, legacy.OutputValues[key]);
            using (var context = new ComputeContext())
            {
                using var buffer = IndicatorCompute.ComputeTypicalPriceVolatilityFast(Data(), context, length);
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
        var bars = Enumerable.Range(0, 40).Select(i =>
            new Bar(DateTime.UnixEpoch.AddDays(i), 10, 20, 1, 5 + i % 7, 1)).ToArray();
        var source = new Sma(2);
        var indicator = new TypicalPriceVolatility(3).Of(source);
        using var run = await new StockIndicatorBuilder().ConfigureSource(Bars.From(bars)).ConfigureIndicators(source, indicator).BuildAsync();
        var selected = run[source].ToArray();
        // Typed Of() replaces close while retaining the original high/low.
        var typedBars = bars.Select((b, i) => new Bar(b.Time, b.Open, b.High, b.Low, selected[i], b.Volume)).ToArray();
        Assert.Equal(BuiltInFormulaReferences.RoundedTypicalVolatility(typedBars, 3), run[indicator.Outputs[0]].ToArray());
        // Legacy selected-input lists retain the documented custom-range projection.
        var projected = bars.Select((b, i) => new Bar(b.Time, selected[i],
            selected[i] >= 1 ? b.High : Math.Max(selected[Math.Max(0, i - 1)], selected[i]),
            selected[i] >= 1 ? b.Low : Math.Min(selected[Math.Max(0, i - 1)], selected[i]), selected[i], b.Volume)).ToArray();
        var expected = BuiltInFormulaReferences.RoundedTypicalVolatility(projected, 3);
        foreach (var chained in new[] { false, true })
        {
            var data = new StockData(bars.Select(b => b.Open), bars.Select(b => b.High), bars.Select(b => b.Low),
                bars.Select(b => b.Close), bars.Select(b => b.Volume), bars.Select(b => b.Time));
            if (chained) data.CustomValuesList = selected.ToList(); else data.InputValues = selected.ToList();
            using var context = new ComputeContext();
            using var buffer = IndicatorCompute.ComputeTypicalPriceVolatilityFast(data, context, 3);
            Assert.Equal(expected, buffer.ToArray());
            Assert.Equal(expected, data.CalculateTypicalPriceVolatility(3).OutputValues["Tpv"]);
        }
    }
}

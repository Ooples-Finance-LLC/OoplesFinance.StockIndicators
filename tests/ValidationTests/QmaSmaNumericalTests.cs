using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Builder.Compute;
using OoplesFinance.StockIndicators.Builder.Specs;
using OoplesFinance.StockIndicators.Core;
using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Streaming;
using OoplesFinance.StockIndicators.Validation;

namespace OoplesFinance.StockIndicators.Tests.Unit.ValidationTests;

public sealed class QmaSmaNumericalTests
{
    [Fact]
    public void RoundedComponentsPreserveWarmupExtremesAndExpiry()
    {
        foreach (var prices in new[] {
            new[] { double.MaxValue, double.MaxValue, 0d, 7, 7, 7 },
            new[] { -double.MaxValue, -double.MaxValue, 0d, 7, 7, 7 },
            new[] { double.Epsilon, 2 * double.Epsilon, 4 * double.Epsilon, 7, 7, 7 },
            new[] { 1d, 2, 4, 7, 7, 7 } })
        {
            var bars = prices.Select(v => new Bar(DateTime.UnixEpoch, v, v, v, v, 1)).ToArray();
            var expected = BuiltInFormulaReferences.RoundedQmaSmaDifference(bars, 2);
            var data = new StockData(prices, prices, prices, prices, prices.Select(_ => 1d), bars.Select(b => b.Time));
            Assert.Equal(expected, data.CalculateQmaSmaDifference(2).OutputValues["QmaSmaDiff"]);
            Assert.Equal(Math.Abs(prices[0]), expected[0]);
            Assert.Equal(0, expected[^1]);
            if (prices[0] == double.MaxValue) Assert.Equal(0, expected[1]);
            if (prices[0] == -double.MaxValue) Assert.Equal(double.PositiveInfinity, expected[1]);
        }
    }

    public static IEnumerable<object[]> Cases => IndicatorValidationDiscovery.Discover(new[] { typeof(IIndicator).Assembly })
        .Where(c => c.IndicatorType == typeof(QmaSmaDifference))
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
    public void EveryRouteMatchesIndependentRoundedComponents(IndicatorValidationCase testCase)
    {
        var indicator = testCase.Factory();
        var builtIn = (IBuiltInIndicator)indicator;
        var options = builtIn.CreateOptions();
        var length = (int)options.GetType().GetProperty("Length")!.GetValue(options)!;
        var key = "QmaSmaDiff";
        var spec = new IndicatorSpec(builtIn.BatchName, options);
        foreach (var fixture in IndicatorAdversarialCases.Generate(32, 244))
        {
            var bars = fixture.Bars;
            var expected = BuiltInFormulaReferences.RoundedQmaSmaDifference(bars, length);
            var prices = bars.Select(b => b.Close).ToArray();
            StockData Data() => new StockData(bars.Select(b => b.Open), bars.Select(b => b.High), bars.Select(b => b.Low),
                prices, bars.Select(b => b.Volume), bars.Select(b => b.Time));
            var legacy = Data().CalculateQmaSmaDifference(length);
            Assert.Equal(expected, legacy.OutputValues[key]);
            Assert.True(BuilderArmBinding.TryGetTarget(options.GetType(), out var target));
            Assert.Equal(expected, BuilderArmBinding.Compute(Data(), spec, target));
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
        var indicator = new QmaSmaDifference(3).Of(source);
        using var run = await new StockIndicatorBuilder().ConfigureSource(Bars.From(bars)).ConfigureIndicators(source, indicator).BuildAsync();
        var selected = run[source].ToArray();
        var projected = bars.Select((b, i) => new Bar(b.Time, selected[i], selected[i], selected[i], selected[i], b.Volume)).ToArray();
        var expected = BuiltInFormulaReferences.RoundedQmaSmaDifference(projected, 3);
        Assert.Equal(expected, run[indicator.Outputs[0]].ToArray());
        foreach (var chained in new[] { false, true })
        {
            var data = new StockData(bars.Select(b => b.Open), bars.Select(b => b.High), bars.Select(b => b.Low),
                bars.Select(b => b.Close), bars.Select(b => b.Volume), bars.Select(b => b.Time));
            if (chained) data.CustomValuesList = selected.ToList(); else data.InputValues = selected.ToList();
            Assert.Equal(expected, data.CalculateQmaSmaDifference(3).OutputValues["QmaSmaDiff"]);
        }
    }
}

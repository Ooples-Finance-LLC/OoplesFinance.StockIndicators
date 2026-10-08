using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Builder.Compute;
using OoplesFinance.StockIndicators.Builder.Specs;
using OoplesFinance.StockIndicators.Core;
using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Streaming;
using OoplesFinance.StockIndicators.Validation;

namespace OoplesFinance.StockIndicators.Tests.Unit.ValidationTests;

public sealed class DownsideDeviationNumericalTests
{
    [Theory]
    [InlineData(-0.5)]
    [InlineData(0)]
    [InlineData(0.25)]
    [InlineData(double.MaxValue)]
    public void TargetsExtremesAndExpiryMatchIndependentRationalFormula(double target)
    {
        var sequences = new[]
        {
            new[] { 2d, 1, 2, 2, 0, -1, 4, 3, 3 },
            new[] { 1d, -double.MaxValue, 1, -double.MaxValue, 1, 1, 1 },
            new[] { 0.5, -double.MaxValue, 1, 0, 1, 0, 1, 0, 1 },
            new[] { double.Epsilon, double.MaxValue, double.Epsilon, 0, 1, 1, 1 },
            new[] { double.MaxValue, Math.BitDecrement(double.MaxValue), double.MaxValue, 1, 1, 1 }
        };
        foreach (var prices in sequences)
        foreach (var length in new[] { 1, 3, 5 })
        {
            var bars = prices.Select((p, i) => new Bar(DateTime.UnixEpoch.AddDays(i), p, p, p, p, 1)).ToArray();
            var expected = BuiltInFormulaReferences.RoundedDownside(bars, length, target);
            var actual = new double[prices.Length];
            VolatilityCore.DownsideDeviation(prices, actual, length, target);
            Assert.Equal(expected, actual);
            var data = new StockData(prices, prices, prices, prices, prices.Select(_ => 1d), bars.Select(b => b.Time));
            Assert.Equal(expected, data.CalculateDownsideDeviation(length, target).OutputValues["Dd"]);
            using var state = new DownsideDeviationState(length, target);
            for (var replay = 0; replay < 2; replay++)
            {
                state.Reset();
                for (var i = 0; i < prices.Length; i++)
                {
                    var b = bars[i];
                    var native = new OhlcvBar("TEST", BarTimeframe.Minutes(1), b.Time, b.Time, b.Open, b.High, b.Low, b.Close, 1, true);
                    Assert.Equal(expected[i], state.Update(native, false, true).Value);
                    Assert.Equal(expected[i], state.Update(native, true, true).Value);
                }
            }
        }
    }

    [Fact]
    public void ConditionalDivisorPreservesFiniteDilutedOverflowAndTinyReturns()
    {
        var prices = new[] { 0.5, -double.MaxValue, 1, 0, 1, 0, 1, 0 };
        var actual = new double[prices.Length];
        VolatilityCore.DownsideDeviation(prices, actual, 7);
        Assert.Equal(double.MaxValue, actual[7]); // sqrt((2*Max)^2 / 4), despite the overflowing return.
        VolatilityCore.DownsideDeviation(new[] { 2d, 1, 2, 2 }, actual, 3);
        Assert.Equal(0.5, actual[3]); // one shortfall, not three observations.
        VolatilityCore.DownsideDeviation(new[] { double.MaxValue, Math.BitDecrement(double.MaxValue) }, actual, 1);
        Assert.True(actual[1] > 0);
    }

    public static IEnumerable<object[]> Cases => IndicatorValidationDiscovery.Discover(new[] { typeof(IIndicator).Assembly })
        .Where(c => c.IndicatorType == typeof(DownsideDeviation))
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
    public void EveryRouteMatchesIndependentConditionalRms(IndicatorValidationCase testCase)
    {
        var indicator = testCase.Factory();
        var builtIn = (IBuiltInIndicator)indicator;
        var options = builtIn.CreateOptions();
        var length = (int)options.GetType().GetProperty("Length")!.GetValue(options)!;
        var key = "Dd";
        var spec = new IndicatorSpec(builtIn.BatchName, options);
        foreach (var fixture in IndicatorAdversarialCases.Generate(32, 244))
        {
            var bars = fixture.Bars;
            var expected = BuiltInFormulaReferences.RoundedDownside(bars, length);
            var prices = bars.Select(b => b.Close).ToArray();
            var actual = new double[bars.Count];
            VolatilityCore.DownsideDeviation(prices, actual, length);
            Assert.Equal(expected, actual);
            StockData Data() => new StockData(bars.Select(b => b.Open), bars.Select(b => b.High), bars.Select(b => b.Low),
                prices, bars.Select(b => b.Volume), bars.Select(b => b.Time));
            var legacy = Data().CalculateDownsideDeviation(length);
            Assert.Equal(expected, legacy.OutputValues[key]);
            using (var context = new ComputeContext())
            {
                using var buffer = IndicatorCompute.ComputeDownsideDeviationFast(Data(), context, length);
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
        var indicator = new DownsideDeviation(3).Of(source);
        using var run = await new StockIndicatorBuilder().ConfigureSource(Bars.From(bars)).ConfigureIndicators(source, indicator).BuildAsync();
        var selected = run[source].ToArray();
        var projected = bars.Select((b, i) => new Bar(b.Time, selected[i], selected[i], selected[i], selected[i], b.Volume)).ToArray();
        var expected = BuiltInFormulaReferences.RoundedDownside(projected, 3);
        Assert.Equal(expected, run[indicator.Outputs[0]].ToArray());
        foreach (var chained in new[] { false, true })
        {
            var data = new StockData(bars.Select(b => b.Open), bars.Select(b => b.High), bars.Select(b => b.Low),
                bars.Select(b => b.Close), bars.Select(b => b.Volume), bars.Select(b => b.Time));
            if (chained) data.CustomValuesList = selected.ToList(); else data.InputValues = selected.ToList();
            using var context = new ComputeContext();
            using var buffer = IndicatorCompute.ComputeDownsideDeviationFast(data, context, 3);
            Assert.Equal(expected, buffer.ToArray());
            Assert.Equal(expected, data.CalculateDownsideDeviation(3).OutputValues["Dd"]);
        }
    }
}

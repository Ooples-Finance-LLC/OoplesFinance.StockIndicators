using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Builder.Compute;
using OoplesFinance.StockIndicators.Builder.Specs;
using OoplesFinance.StockIndicators.Core;
using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Streaming;
using OoplesFinance.StockIndicators.Validation;

namespace OoplesFinance.StockIndicators.Tests.Unit.ValidationTests;

public sealed class TrailingReverseNumericalTests
{
    [Fact]
    public void ThresholdEqualityReversesOnlyTheRisingLeg()
    {
        var prices = new[] { 100d, 98, 99.96, 100, 98, 97, 100 };
        var expected = new[] { 98d, 99.96, 99.96, 98, 99.96, 98.94, 98 };
        var data = new StockData(prices, prices, prices, prices, prices.Select(_ => 1d), prices.Select((_, i) => DateTime.UnixEpoch.AddDays(i)));
        Assert.Equal(expected, data.CalculateNickRypockTrailingReverse(2).OutputValues["Nrtr"]);
        var state = new NickRypockTrailingReverseState(2);
        for (var replay = 0; replay < 2; replay++)
        {
            state.Reset();
            for (var i = 0; i < prices.Length; i++)
            {
                var time = DateTime.UnixEpoch.AddDays(i);
                var bar = new OhlcvBar("NRTR", BarTimeframe.Minutes(1), time, time, prices[i], prices[i], prices[i], prices[i], 1, true);
                Assert.Equal(expected[i], state.Update(bar, false, true).Value);
                Assert.Equal(expected[i], state.Update(bar, true, true).Value);
            }
        }
    }

    [Theory]
    [InlineData(-2)]
    [InlineData(0)]
    [InlineData(100)]
    [InlineData(101)]
    [InlineData(int.MaxValue)]
    public void PercentageDomainPreservesExtremeThresholds(int percent)
    {
        var prices = new[] { double.MaxValue, -double.MaxValue, double.Epsilon, 0, 7, 8, 9 };
        var bars = prices.Select((v, i) => new Bar(DateTime.UnixEpoch.AddDays(i), v, v, v, v, 1)).ToArray();
        var expected = BuiltInFormulaReferences.RoundedTrailingReverse(bars, percent);
        var data = new StockData(prices, prices, prices, prices, prices.Select(_ => 1d), bars.Select(b => b.Time));
        Assert.Equal(expected, data.CalculateNickRypockTrailingReverse(percent).OutputValues["Nrtr"]);
        var state = new NickRypockTrailingReverseState(percent);
        for (var i = 0; i < bars.Length; i++)
        {
            var b = bars[i];
            var native = new OhlcvBar("NRTR", BarTimeframe.Minutes(1), b.Time, b.Time, b.Open, b.High, b.Low, b.Close, b.Volume, true);
            Assert.Equal(expected[i], state.Update(native, false, true).Value);
            Assert.Equal(expected[i], state.Update(native, true, true).Value);
        }
    }

    public static IEnumerable<object[]> Cases => IndicatorValidationDiscovery.Discover(new[] { typeof(IIndicator).Assembly })
        .Where(c => c.IndicatorType == typeof(NickRypockTrailingReverse))
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
    public void EveryRouteMatchesIndependentPercentageThresholds(IndicatorValidationCase testCase)
    {
        var indicator = testCase.Factory();
        var builtIn = (IBuiltInIndicator)indicator;
        var options = builtIn.CreateOptions();
        var length = (int)options.GetType().GetProperty("Length")!.GetValue(options)!;
        var key = "Nrtr";
        var spec = new IndicatorSpec(builtIn.BatchName, options);
        foreach (var fixture in IndicatorAdversarialCases.Generate(32, 244))
        {
            var bars = fixture.Bars;
            var expected = BuiltInFormulaReferences.RoundedTrailingReverse(bars, length);
            var prices = bars.Select(b => b.Close).ToArray();
            StockData Data() => new StockData(bars.Select(b => b.Open), bars.Select(b => b.High), bars.Select(b => b.Low),
                prices, bars.Select(b => b.Volume), bars.Select(b => b.Time));
            var legacy = Data().CalculateNickRypockTrailingReverse(length);
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
        var indicator = new NickRypockTrailingReverse(3).Of(source);
        using var run = await new StockIndicatorBuilder().ConfigureSource(Bars.From(bars)).ConfigureIndicators(source, indicator).BuildAsync();
        var selected = run[source].ToArray();
        var projected = bars.Select((b, i) => new Bar(b.Time, selected[i], selected[i], selected[i], selected[i], b.Volume)).ToArray();
        var expected = BuiltInFormulaReferences.RoundedTrailingReverse(projected, 3);
        Assert.Equal(expected, run[indicator.Outputs[0]].ToArray());
        foreach (var chained in new[] { false, true })
        {
            var data = new StockData(bars.Select(b => b.Open), bars.Select(b => b.High), bars.Select(b => b.Low),
                bars.Select(b => b.Close), bars.Select(b => b.Volume), bars.Select(b => b.Time));
            if (chained) data.CustomValuesList = selected.ToList(); else data.InputValues = selected.ToList();
            Assert.Equal(expected, data.CalculateNickRypockTrailingReverse(3).OutputValues["Nrtr"]);
        }
    }
}

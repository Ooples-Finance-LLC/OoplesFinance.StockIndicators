using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Builder.Compute;
using OoplesFinance.StockIndicators.Builder.Specs;
using OoplesFinance.StockIndicators.Core;
using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Streaming;
using OoplesFinance.StockIndicators.Validation;

namespace OoplesFinance.StockIndicators.Tests.Unit.ValidationTests;

public sealed class WilliamsNumericalTests
{
    public static IEnumerable<object[]> Cases => IndicatorValidationDiscovery.Discover(new[] { typeof(IIndicator).Assembly })
        .Where(c => c.IndicatorType == typeof(WilliamsR)).Select(c => new object[] { c });

    [Theory, MemberData(nameof(Cases))]
    public async Task EveryConfigurationReceivesEveryNumericalClass(IndicatorValidationCase testCase)
    {
        var report = await IndicatorValidation.ValidateAsync(testCase);
        report.ThrowIfInvalid();
        Assert.Equal(4, report.OutputOverflowRejectionsChecked);
        Assert.Contains(report.FixtureEvidence, f => f.OutputOverflowSign == 1 && f.OutputOverflowRejectionsChecked == 2);
        Assert.Contains(report.FixtureEvidence, f => f.OutputOverflowSign == -1 && f.OutputOverflowRejectionsChecked == 2);
        foreach (var fixture in IndicatorAdversarialCases.Generate(256, 244))
            Assert.Contains(report.FixtureEvidence, f => f.Name == fixture.Name && f.Completed && f.Passed);
    }

    [Fact]
    public void ExactRatioPreservesRangeOrientationWithoutClamping()
    {
        Assert.Equal(-50, WilliamsRangePosition.Percent(0, -double.MaxValue, double.MaxValue));
        Assert.Equal(-double.Epsilon, WilliamsRangePosition.Percent(-double.Epsilon, -100, 0));
        Assert.Equal(-100, WilliamsRangePosition.Percent(3, 1, 1));
        Assert.Equal(100, WilliamsRangePosition.Percent(2, 0, 1));
        Assert.Equal(-200, WilliamsRangePosition.Percent(2, 1, 0));
        Assert.Equal(double.PositiveInfinity, WilliamsRangePosition.Percent(double.MaxValue, 0, double.Epsilon));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(3)]
    [InlineData(14)]
    public async Task ExactOracleMatchesCoreLegacyFactoriesPreviewAndReset(int length)
    {
        foreach (var fixture in IndicatorAdversarialCases.Generate(32, 244))
        {
            var bars = fixture.Bars;
            var expected = BuiltInFormulaReferences.RoundedWilliams(bars, length);
            var indicator = new WilliamsR(length);
            if (expected.Any(double.IsInfinity))
                await Assert.ThrowsAsync<IndicatorOutputException>(() => new StockIndicatorBuilder()
                    .ConfigureSource(Bars.From(bars)).ConfigureIndicators(indicator).BuildAsync());
            else
            {
                using var run = await new StockIndicatorBuilder().ConfigureSource(Bars.From(bars)).ConfigureIndicators(indicator).BuildAsync();
                Assert.Equal(expected, run[indicator].ToArray());
            }
            var data = Data(bars);
            data.CalculateWilliamsR(length);
            Assert.Equal(expected, data.OutputValues["Williams%R"]);
            var core = new double[bars.Count];
            OscillatorCore.WilliamsR(bars.Select(b => b.High).ToArray(), bars.Select(b => b.Low).ToArray(),
                bars.Select(b => b.Close).ToArray(), core, length);
            Assert.Equal(expected, core);
            var builtIn = (IBuiltInIndicator)indicator;
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
                        var native = new OhlcvBar("WR", BarTimeframe.Minutes(1), b.Time, b.Time, b.Open, b.High, b.Low, b.Close, b.Volume, true);
                        Assert.Equal(expected[i], state.Update(native, commit, true).Value);
                    }
                }
            }
        }
    }

    [Fact]
    public async Task OutOfRangePriceRequiresProvenOverflowRejection()
    {
        var bars = new[] { new Bar(DateTime.UnixEpoch, 0, double.Epsilon, 0, double.MaxValue, 1) };
        var testCase = new IndicatorValidationCase(typeof(WilliamsR), "overflow", () => new WilliamsR(1));
        var report = await IndicatorValidation.ValidateAsync(testCase, new IndicatorValidationOptions {
            AdditionalFixtures = new[] { new IndicatorValidationFixture("williams-overflow", bars) }
        });
        report.ThrowIfInvalid();
        Assert.Equal(2, Assert.Single(report.FixtureEvidence, f => f.Name == "williams-overflow").OutputOverflowRejectionsChecked);
    }

    [Fact]
    public void LegacySelectedInputRetainsItsOwnRange()
    {
        var bars = new[] { 100d, 101d, 102d }.Select((v, i) =>
            new Bar(DateTime.UnixEpoch.AddMinutes(i), v, v + 1, v - 1, v, 1)).ToArray();
        var data = Data(bars);
        data.InputValues = new() { 1, 2, 3 };
        using var context = new ComputeContext();
        using var result = IndicatorCompute.ComputeWilliamsRFast(data, context, 2);
        Assert.Equal(new[] { -100d, 0, 0 }, result.ToArray());
        data.CalculateWilliamsR(2);
        Assert.Equal(result.ToArray(), data.OutputValues["Williams%R"]);
    }

    private static StockData Data(IReadOnlyList<Bar> bars) => new(bars.Select(b => b.Open), bars.Select(b => b.High),
        bars.Select(b => b.Low), bars.Select(b => b.Close), bars.Select(b => b.Volume), bars.Select(b => b.Time));
}

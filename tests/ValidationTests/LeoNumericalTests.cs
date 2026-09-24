using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Builder.Specs;
using OoplesFinance.StockIndicators.Core;
using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Streaming;
using OoplesFinance.StockIndicators.Validation;

namespace OoplesFinance.StockIndicators.Tests.Unit.ValidationTests;

public sealed class LeoNumericalTests
{
    public static IEnumerable<object[]> Cases => IndicatorValidationDiscovery.Discover(new[] { typeof(IIndicator).Assembly })
        .Where(c => c.IndicatorType == typeof(LeoMovingAverage)).Select(c => new object[] { c });

    [Theory, MemberData(nameof(Cases))]
    public async Task EveryConfigurationReceivesEveryNumericalClass(IndicatorValidationCase testCase)
    {
        var report = await IndicatorValidation.ValidateAsync(testCase);
        report.ThrowIfInvalid();
        foreach (var fixture in IndicatorAdversarialCases.Generate(256, 244))
            Assert.Contains(report.FixtureEvidence, f => f.Name == fixture.Name && f.Completed && f.Passed);
    }

    [Fact]
    public void CombinationPreservesFiniteCancellationAndReportsRealOverflow()
    {
        foreach (var value in new[] { double.MaxValue, -double.MaxValue, double.Epsilon, -double.Epsilon })
            Assert.Equal(value, LeoAverage.Combine(value, value));
        Assert.Equal(double.Epsilon, LeoAverage.Combine(double.Epsilon, double.Epsilon));
        Assert.Equal(double.PositiveInfinity, LeoAverage.Combine(double.MaxValue, -double.MaxValue));
        Assert.Equal(double.NegativeInfinity, LeoAverage.Combine(-double.MaxValue, double.MaxValue));
        MovingAverageCore.LeoMovingAverage(Array.Empty<double>(), Span<double>.Empty, 14);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(3)]
    [InlineData(14)]
    public async Task RoutesMatchRoundedComponentReferencesIncludingOverflow(int length)
    {
        foreach (var fixture in IndicatorAdversarialCases.Generate(40, 244))
        {
            var bars = fixture.Bars.ToArray();
            var expected = BuiltInFormulaReferences.RoundedLeoMean(bars, length);
            var actual = new double[bars.Length];
            MovingAverageCore.LeoMovingAverage(bars.Select(b => b.Close).ToArray(), actual, length);
            Assert.Equal(expected, actual);
            var data = new StockData(bars.Select(b => b.Open), bars.Select(b => b.High), bars.Select(b => b.Low),
                bars.Select(b => b.Close), bars.Select(b => b.Volume), bars.Select(b => b.Time));
            data.CalculateLeoMovingAverage(length);
            Assert.Equal(expected, data.OutputValues["Lma"]);
            var indicator = new LeoMovingAverage(length);
            if (expected.Any(double.IsInfinity))
                await Assert.ThrowsAsync<IndicatorOutputException>(() => new StockIndicatorBuilder().ConfigureSource(Bars.From(bars))
                    .ConfigureIndicators(indicator).BuildAsync());
            else
            {
                using var run = await new StockIndicatorBuilder().ConfigureSource(Bars.From(bars)).ConfigureIndicators(indicator).BuildAsync();
                Assert.Equal(expected, run[indicator].ToArray());
            }
            var spec = new IndicatorSpec(IndicatorName.LeoMovingAverage, new LeoMovingAverageSpecOptions(length));
            foreach (var state in new[] { StatefulIndicatorFactory.Create(spec), StreamingIndicatorFactory.CreateState(spec) })
            {
                Assert.NotNull(state);
                using var lifetime = state as IDisposable;
                for (var replay = 0; replay < 2; replay++)
                {
                    state.Reset();
                    for (var i = 0; i < bars.Length; i++)
                    {
                        var b = bars[i];
                        var native = new OhlcvBar("LEO", BarTimeframe.Minutes(1), b.Time, b.Time, b.Open, b.High, b.Low, b.Close, b.Volume, true);
                        foreach (var commit in new[] { false, true })
                            Assert.Equal(expected[i], state.Update(native, commit, true).Outputs!["Lma"]);
                    }
                }
            }
        }
    }

    [Fact]
    public async Task UnrepresentableExtrapolationIsRejectedAtTheOutputBoundary()
    {
        // WMA is zero padded while SMA is zero until its full window arrives.
        // The second startup value is therefore 5/3 * MaxValue.
        var values = new[] { double.MaxValue, double.MaxValue, double.MaxValue };
        var bars = values.Select((v, i) => new Bar(DateTime.UnixEpoch.AddMinutes(i), v, v, v, v, 1)).ToArray();
        var expected = BuiltInFormulaReferences.RoundedLeoMean(bars, 3);
        Assert.Contains(expected, double.IsInfinity);
        await Assert.ThrowsAsync<IndicatorOutputException>(() => new StockIndicatorBuilder().ConfigureSource(Bars.From(bars))
            .ConfigureIndicators(new LeoMovingAverage(3)).BuildAsync());
    }
}

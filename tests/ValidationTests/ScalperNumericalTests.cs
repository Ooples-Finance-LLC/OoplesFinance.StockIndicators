using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Streaming;
using OoplesFinance.StockIndicators.Validation;

namespace OoplesFinance.StockIndicators.Tests.Unit.ValidationTests;

public sealed class ScalperNumericalTests
{
    [Fact]
    public async Task ReceivesEveryNumericalClass()
    {
        var testCase = Assert.Single(IndicatorValidationDiscovery.Discover(new[] { typeof(IIndicator).Assembly })
            .Where(c => c.IndicatorType == typeof(TTMScalperIndicator)));
        var report = await IndicatorValidation.ValidateAsync(testCase);
        report.ThrowIfInvalid();
        foreach (var fixture in IndicatorAdversarialCases.Generate(256, 244))
            Assert.Contains(report.FixtureEvidence, f => f.Name == fixture.Name && f.Completed && f.Passed);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task PublishesOnlyConfirmedAlternatingEvents(bool mirrored)
    {
        foreach (var unit in new[] { double.Epsilon, 1d, double.MaxValue / 4 })
        {
            double[] steps = [0, 1, 2, 3, 2, 1, 0, 1, 2, 3, 3, 3];
            double[] expectedSteps = mirrored
                ? [0, 0, 0, 0, 0, 0, 0, 0, -3, -3, -3, -3]
                : [0, 0, 3, 3, 3, 0, 0, 0, 3, 3, 3, 3];
            var bars = steps.Select((s, i) =>
            {
                var close = (mirrored ? -s : s) * unit;
                return new Bar(DateTime.UnixEpoch.AddMinutes(i), close, close + unit, close - unit, close, 1);
            }).ToArray();
            var expected = expectedSteps.Select(s => s * unit).ToArray();
            var indicator = new TTMScalperIndicator();
            using var run = await new StockIndicatorBuilder().ConfigureSource(Bars.From(bars))
                .ConfigureIndicators(indicator).BuildAsync();
            Assert.Equal(expected, run[indicator.Outputs[0]].ToArray());
            var data = new StockData(bars.Select(b => b.Open), bars.Select(b => b.High), bars.Select(b => b.Low),
                bars.Select(b => b.Close), bars.Select(b => b.Volume), bars.Select(b => b.Time));
            data.CalculateTTMScalperIndicator();
            Assert.Equal(expected, data.OutputValues["Sbs"]);
            using var state = new TTMScalperIndicatorState();
            for (var replay = 0; replay < 2; replay++)
            {
                state.Reset();
                for (var i = 0; i < bars.Length; i++)
                {
                    var b = bars[i];
                    var native = new OhlcvBar("TTM", BarTimeframe.Minutes(1), b.Time, b.Time,
                        b.Open, b.High, b.Low, b.Close, b.Volume, true);
                    var preview = new OhlcvBar("TTM", BarTimeframe.Minutes(1), b.Time, b.Time, 0, 0, 0, 0, 1, true);
                    state.Update(preview, false, true);
                    foreach (var commit in new[] { false, true })
                        Assert.Equal(expected[i], state.Update(native, commit, true).Outputs!["Sbs"]);
                }
            }
        }
    }
}

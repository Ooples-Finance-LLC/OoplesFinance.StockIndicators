using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Streaming;
using OoplesFinance.StockIndicators.Validation;

namespace OoplesFinance.StockIndicators.Tests.Unit.ValidationTests;

public sealed class GannNumericalTests
{
    public static IEnumerable<object[]> Cases => IndicatorValidationDiscovery.Discover(new[] { typeof(IIndicator).Assembly })
        .Where(c => c.IndicatorType == typeof(GannSwingOscillator) || c.IndicatorType == typeof(GannTrendOscillator))
        .Select(c => new object[] { c });

    [Theory, MemberData(nameof(Cases))]
    public async Task EveryConfigurationReceivesEveryNumericalClass(IndicatorValidationCase testCase)
    {
        var report = await IndicatorValidation.ValidateAsync(testCase);
        report.ThrowIfInvalid();
        foreach (var fixture in IndicatorAdversarialCases.Generate(256, 244))
            Assert.Contains(report.FixtureEvidence, f => f.Name == fixture.Name && f.Completed && f.Passed);
    }

    [Fact]
    public async Task SimultaneousReversalsPreferHighAndExactTiesRetainState()
    {
        foreach (var magnitude in new[] { double.Epsilon, 1d, double.MaxValue })
        {
            var adjacent = Math.BitDecrement(magnitude);
            var highs = new[] { magnitude, adjacent, magnitude, magnitude, magnitude, magnitude };
            var lows = new[] { -magnitude, -adjacent, -magnitude, -adjacent, -magnitude, -magnitude };
            var bars = highs.Select((high, i) => new Bar(DateTime.UnixEpoch.AddMinutes(i), 0, high, lows[i], 0, 1)).ToArray();
            var expected = new[] { 0d, 0, 1, 1, -1, -1 };
            IIndicator[] indicators = { new GannSwingOscillator(1), new GannTrendOscillator(1) };
            using var run = await new StockIndicatorBuilder().ConfigureSource(Bars.From(bars)).ConfigureIndicators(indicators).BuildAsync();
            foreach (var indicator in indicators) Assert.Equal(expected, run[indicator.Outputs[0]].ToArray());
            foreach (var swing in new[] { true, false })
            {
                var data = new StockData(bars.Select(b => b.Open), bars.Select(b => b.High), bars.Select(b => b.Low),
                    bars.Select(b => b.Close), bars.Select(b => b.Volume), bars.Select(b => b.Time));
                if (swing) data.CalculateGannSwingOscillator(1); else data.CalculateGannTrendOscillator(1);
                var key = swing ? "Gso" : "Gto";
                Assert.Equal(expected, data.OutputValues[key]);
                IStreamingIndicatorState state = swing ? new GannSwingOscillatorState(1) : new GannTrendOscillatorState(1);
                using var lifetime = (IDisposable)state;
                for (var replay = 0; replay < 2; replay++)
                {
                    state.Reset();
                    for (var i = 0; i < bars.Length; i++)
                    {
                        var b = bars[i];
                        var native = new OhlcvBar("GANN", BarTimeframe.Minutes(1), b.Time, b.Time, b.Open, b.High, b.Low, b.Close, b.Volume, true);
                        var unrelatedPreview = new OhlcvBar("GANN", BarTimeframe.Minutes(1), b.Time, b.Time, 0, 0, 0, 0, 1, true);
                        state.Update(unrelatedPreview, false, true);
                        foreach (var commit in new[] { false, true })
                            Assert.Equal(expected[i], state.Update(native, commit, true).Outputs![key]);
                    }
                }
            }
        }
    }
}

using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Builder.Specs;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Streaming;
using OoplesFinance.StockIndicators.Validation;

namespace OoplesFinance.StockIndicators.Tests.Unit.ValidationTests;

public sealed class RangeIdentifierNumericalTests
{
    public static IEnumerable<object[]> Cases => IndicatorValidationDiscovery.Discover(new[] { typeof(IIndicator).Assembly })
        .Where(c => c.IndicatorType == typeof(RangeIdentifier)).Select(c => new object[] { c });

    [Theory, MemberData(nameof(Cases))]
    public async Task EveryConfigurationReceivesEveryNumericalClass(IndicatorValidationCase testCase)
    {
        var report = await IndicatorValidation.ValidateAsync(testCase);
        report.ThrowIfInvalid();
        foreach (var fixture in IndicatorAdversarialCases.Generate(256, 244))
            Assert.Contains(report.FixtureEvidence, f => f.Name == fixture.Name && f.Completed && f.Passed);
    }

    [Fact]
    public async Task BreakoutTiesReplaceTheAnchorAndFiniteBoundsKeepFiniteMidpoints()
    {
        // Inside closes retain the anchor even when that candle has a wider range.
        // Equality at either bound starts a new anchor; length is a legacy no-op.
        var inputs = new (double Low, double High, double Close, double Lower, double Upper, double Middle)[] {
            (0, double.Epsilon, double.Epsilon, 0, double.Epsilon, 0),
            (double.Epsilon, 2 * double.Epsilon, 2 * double.Epsilon, double.Epsilon, 2 * double.Epsilon, 2 * double.Epsilon),
            (2, 6, 4, 2, 6, 4), (1, 7, 5, 2, 6, 4), (4, 8, 6, 4, 8, 6),
            (0, 6, 4, 0, 6, 3),
            (double.MaxValue, double.MaxValue, double.MaxValue, double.MaxValue, double.MaxValue, double.MaxValue),
            (-double.MaxValue, -double.MaxValue, -double.MaxValue, -double.MaxValue, -double.MaxValue, -double.MaxValue),
            (-double.MaxValue, double.MaxValue, 0, -double.MaxValue, double.MaxValue, 0)
        };
        var bars = inputs.Select((x, i) => new Bar(DateTime.UnixEpoch.AddMinutes(i), x.Close, x.High, x.Low, x.Close, 1)).ToArray();
        var expected = new Dictionary<string, double[]> {
            ["UpperBand"] = inputs.Select(x => x.Upper).ToArray(),
            ["MiddleBand"] = inputs.Select(x => x.Middle).ToArray(),
            ["LowerBand"] = inputs.Select(x => x.Lower).ToArray()
        };
        foreach (var length in new[] { 1, 3, 34 })
        {
            IIndicator indicator = new RangeIdentifier(length);
            var builtIn = (IBuiltInIndicator)indicator;
            using var run = await new StockIndicatorBuilder().ConfigureSource(Bars.From(bars)).ConfigureIndicators(indicator).BuildAsync();
            var keys = indicator.Outputs.Count == 1 ? new[] { builtIn.BatchOutputKey ?? "MiddleBand" } : GeneratedIndicatorOutputs.KeysFor(builtIn.BatchName);
            for (var slot = 0; slot < keys.Count; slot++) Assert.Equal(expected[keys[slot]], run[indicator.Outputs[slot]].ToArray());
            var data = new StockData(bars.Select(b => b.Open), bars.Select(b => b.High), bars.Select(b => b.Low),
                bars.Select(b => b.Close), bars.Select(b => b.Volume), bars.Select(b => b.Time));
            data.CalculateRangeIdentifier(length);
            foreach (var (key, values) in expected) Assert.Equal(values, data.OutputValues[key]);
            var spec = IndicatorSpecs.Create(builtIn.BatchName, builtIn.CreateOptions());
            foreach (var state in new[] { StatefulIndicatorFactory.Create(spec), StreamingIndicatorFactory.CreateState(spec) })
            {
                Assert.NotNull(state);
                for (var replay = 0; replay < 2; replay++)
                {
                    state.Reset();
                    for (var i = 0; i < bars.Length; i++)
                    {
                        var b = bars[i];
                        var native = new OhlcvBar("RANGE", BarTimeframe.Minutes(1), b.Time, b.Time, b.Open, b.High, b.Low, b.Close, b.Volume, true);
                        // A provisional breakout must not replace the committed anchor.
                        var preview = new OhlcvBar("RANGE", BarTimeframe.Minutes(1), b.Time, b.Time, 100, 101, 99, 100, 1, true);
                        state.Update(preview, false, true);
                        foreach (var commit in new[] { false, true })
                        {
                            var actual = state.Update(native, commit, true);
                            foreach (var (key, values) in expected) Assert.Equal(values[i], actual.Outputs![key]);
                        }
                    }
                }
            }
        }
    }
}

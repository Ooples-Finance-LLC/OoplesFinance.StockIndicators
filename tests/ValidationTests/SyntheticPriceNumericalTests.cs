using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Builder.Compute;
using OoplesFinance.StockIndicators.Builder.Specs;
using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Streaming;
using OoplesFinance.StockIndicators.Validation;

namespace OoplesFinance.StockIndicators.Tests.Unit.ValidationTests;

public sealed class SyntheticPriceNumericalTests
{
    public static IEnumerable<object[]> Cases => IndicatorValidationDiscovery.Discover(new[] { typeof(IIndicator).Assembly })
        .Where(c => c.IndicatorType == typeof(DetrendedSyntheticPrice)).Select(c => new object[] { c });
    private static StockData Data(IReadOnlyList<Bar> bars) => new(bars.Select(b => b.Open), bars.Select(b => b.High),
        bars.Select(b => b.Low), bars.Select(b => b.Close), bars.Select(b => b.Volume), bars.Select(b => b.Time));
    private static OhlcvBar Native(Bar b) => new("SYNTHETIC", BarTimeframe.Minutes(1), b.Time, b.Time,
        b.Open, b.High, b.Low, b.Close, b.Volume, true);

    [Theory, MemberData(nameof(Cases))]
    public async Task EveryConfigurationReceivesEveryNumericalClass(IndicatorValidationCase testCase)
    {
        var report = await IndicatorValidation.ValidateAsync(testCase);
        report.ThrowIfInvalid();
        foreach (var fixture in IndicatorAdversarialCases.Generate(256, 244))
            Assert.Contains(report.FixtureEvidence, f => f.Name == fixture.Name && f.Completed && f.Passed);
    }

    [Theory, MemberData(nameof(Cases))]
    public void EveryRouteMatchesIndependentRoundedFilterStages(IndicatorValidationCase testCase)
    {
        Core.OscillatorCore.DetrendedSyntheticPrice([], [], [], int.MaxValue);
        var builtIn = (IBuiltInIndicator)testCase.Factory();
        var options = (DetrendedSyntheticPriceSpecOptions)builtIn.CreateOptions();
        Check(options);
        Check(new DetrendedSyntheticPriceSpecOptions(int.MaxValue));
        void Check(DetrendedSyntheticPriceSpecOptions settings)
        {
            var spec = new IndicatorSpec(builtIn.BatchName, settings);
            Assert.True(BuilderArmBinding.TryGetTarget(settings.GetType(), out var target));
            foreach (var fixture in IndicatorAdversarialCases.Generate(40, 244))
            {
                var bars = fixture.Bars;
                var expected = BuiltInFormulaReferences.RoundedSyntheticPriceReference(bars, settings.Length);
                Assert.Equal(expected, BuilderArmBinding.Compute(Data(bars), spec, target));
                using var context = new ComputeContext();
                using var buffer = IndicatorCompute.TryComputeFast(Data(bars), spec, context);
                Assert.NotNull(buffer);
                Assert.Equal(expected, buffer.Value.ToArray());
                var core = new double[bars.Count];
                Core.OscillatorCore.DetrendedSyntheticPrice(bars.Select(b => b.High).ToArray(), bars.Select(b => b.Low).ToArray(), core, settings.Length);
                Assert.Equal(expected, core);
                foreach (var state in new[] { StatefulIndicatorFactory.Create(spec), StreamingIndicatorFactory.CreateState(spec) })
                {
                    Assert.NotNull(state);
                    for (var replay = 0; replay < 2; replay++)
                    {
                        state.Reset();
                        for (var i = 0; i < bars.Count; i++)
                        {
                            state.Update(Native(new Bar(bars[i].Time, -1, 1, -1, -1, 1)), false, true);
                            Assert.Equal(expected[i], state.Update(Native(bars[i]), false, true).Value);
                            Assert.Equal(expected[i], state.Update(Native(bars[i]), true, true).Value);
                        }
                    }
                }
            }
        }
    }

    [Fact]
    public async Task HighLowInputPreservesPublicAndLegacyProjectionRules()
    {
        var bars = IndicatorAdversarialCases.Generate(40, 244).Single(f => f.Name.EndsWith("/negative")).Bars;
        var source = new Sma(2);
        var indicator = new DetrendedSyntheticPrice(3).Of(source);
        using var run = await new StockIndicatorBuilder().ConfigureSource(Bars.From(bars)).ConfigureIndicators(source, indicator).BuildAsync();
        Assert.Equal(BuiltInFormulaReferences.RoundedSyntheticPriceReference(bars, 3), run[indicator].ToArray());
        var selected = run[source].ToArray();
        var projected = bars.Select((b, i) =>
        {
            var value = selected[i];
            var within = value >= b.Low && value <= b.High;
            var previous = i == 0 ? value : selected[i - 1];
            return new Bar(b.Time, b.Open, within ? b.High : Math.Max(value, previous),
                within ? b.Low : Math.Min(value, previous), value, b.Volume);
        }).ToArray();
        var expected = BuiltInFormulaReferences.RoundedSyntheticPriceReference(projected, 3);
        var data = Data(bars);
        data.CustomValuesList = selected.ToList();
        using var context = new ComputeContext();
        var builtIn = (IBuiltInIndicator)indicator;
        using var buffer = IndicatorCompute.TryComputeFast(data, new IndicatorSpec(builtIn.BatchName, builtIn.CreateOptions()), context);
        Assert.NotNull(buffer);
        Assert.Equal(expected, buffer.Value.ToArray());
        Assert.Equal(expected, data.CalculateDetrendedSyntheticPrice(3).CustomValuesList);
        var simple = new[] { 1d, 2, 4 }.Select(v => new Bar(DateTime.UnixEpoch, v, v, v, v, 1)).ToArray();
        Assert.Equal(new[] { 0d, .25, .6875 }, Data(simple).CalculateDetrendedSyntheticPrice(3).CustomValuesList);
    }
}

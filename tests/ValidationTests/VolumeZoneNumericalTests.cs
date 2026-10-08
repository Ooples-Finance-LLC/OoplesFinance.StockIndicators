using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Builder.Compute;
using OoplesFinance.StockIndicators.Builder.Specs;
using OoplesFinance.StockIndicators.Core;
using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Streaming;
using OoplesFinance.StockIndicators.Validation;

namespace OoplesFinance.StockIndicators.Tests.Unit.ValidationTests;

public sealed class VolumeZoneNumericalTests
{
    public static IEnumerable<object[]> Cases => IndicatorValidationDiscovery.Discover(new[] { typeof(IIndicator).Assembly })
        .Where(c => c.IndicatorType == typeof(VolumeZoneOscillator))
        .Select(c => new object[] { c });

    [Theory, MemberData(nameof(Cases))]
    public async Task EveryPeriodReceivesNumericalAndSignedOverflowCases(IndicatorValidationCase testCase)
    {
        var report = await IndicatorValidation.ValidateAsync(testCase);
        report.ThrowIfInvalid();
        foreach (var fixture in IndicatorAdversarialCases.Generate(256, 244))
            Assert.Contains(report.FixtureEvidence, f => f.Name == fixture.Name && f.Completed && f.Passed);
        var prefix = "volume-zone-";
        if (((VolumeZoneOscillator)testCase.Factory()).Length > 1)
        foreach (var sign in new[] { "positive", "negative" })
            Assert.Equal(2, Assert.Single(report.FixtureEvidence, f => f.Name == prefix + sign + "-output-overflow").OutputOverflowRejectionsChecked);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(3)]
    [InlineData(12)]
    public async Task EveryRouteMatchesTheIndependentExactRatio(int length)
    {
        foreach (var fixture in IndicatorAdversarialCases.Generate(32, 244).Concat(new[] {
            new IndicatorValidationFixture("signed-volume-cancellation", new[] { double.MaxValue, -double.MaxValue, 3 * double.Epsilon }
                .Select((v, i) => new Bar(DateTime.UnixEpoch.AddMinutes(i), i, i, i, i, v))),
            new IndicatorValidationFixture("unchanged-price-volume", Enumerable.Range(0, 8)
                .Select(i => new Bar(DateTime.UnixEpoch.AddMinutes(i), 1, 1, 1, 1, i + 1))) }))
        {
            var bars = fixture.Bars;
            var expected = BuiltInFormulaReferences.RoundedVolumeZone(bars, length);
            var data = Data(bars);
            data.CalculateVolumeZoneOscillator(length);
            Assert.Equal(expected, data.OutputValues["Vzo"]);
            var core = new double[bars.Count];
            VolumeCore.VolumeZoneOscillator(bars.Select(b => b.Close).ToArray(), bars.Select(b => b.Volume).ToArray(), core, length);
            Assert.Equal(expected, core);
            foreach (IIndicator indicator in new IIndicator[] { new VolumeZoneOscillator(length) })
            {
                if (expected.Any(double.IsInfinity))
                    await Assert.ThrowsAsync<IndicatorOutputException>(() => new StockIndicatorBuilder()
                        .ConfigureSource(Bars.From(bars)).ConfigureIndicators(indicator).BuildAsync());
                else
                {
                    using var run = await new StockIndicatorBuilder().ConfigureSource(Bars.From(bars)).ConfigureIndicators(indicator).BuildAsync();
                    Assert.Equal(expected, run[indicator].ToArray());
                    if (bars.All(b => b.Volume >= 0))
                        Assert.All(run[indicator].ToArray(), value => Assert.InRange(value, -100, 100));
                }
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
                            var native = new OhlcvBar("ROC", BarTimeframe.Minutes(1), b.Time, b.Time, b.Open, b.High, b.Low, b.Close, b.Volume, true);
                            Assert.Equal(expected[i], state.Update(native, commit, true).Value);
                        }
                    }
                }
            }
        }
    }

    [Fact]
    public async Task TypedSelectionUsesTheSelectedPrices()
    {
        var bars = new[] { 100d, 120, 110, 150 }.Select((v, i) =>
            new Bar(DateTime.UnixEpoch.AddMinutes(i), v, v, v, v, 1)).ToArray();
        var source = new Sma(2);
        var compact = new VolumeZoneOscillator(1).Of(source);
        using var run = await new StockIndicatorBuilder().ConfigureSource(Bars.From(bars))
            .ConfigureIndicators(source, compact).BuildAsync();
        var projected = run[source].ToArray().Select((v, i) => new Bar(bars[i].Time, v, v, v, v, 1)).ToArray();
        var expected = BuiltInFormulaReferences.RoundedVolumeZone(projected, 1);
        Assert.Equal(expected, run[compact].ToArray());
        var data = Data(bars);
        _ = data.TickerDataList;
        data.InputValues = new() { 1, 1, 0, 2 };
        data.Volumes = new() { 3, 3, 3, 3 };
        using var context = new ComputeContext();
        using var result = IndicatorCompute.ComputeVolumeZoneOscillatorFast(data, context, 1);
        Assert.Equal(new[] { 0d, -100, -100, 100 }, result.ToArray());
    }

    private static StockData Data(IReadOnlyList<Bar> bars) => new(bars.Select(b => b.Open), bars.Select(b => b.High),
        bars.Select(b => b.Low), bars.Select(b => b.Close), bars.Select(b => b.Volume), bars.Select(b => b.Time));
}

using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Builder.Compute;
using OoplesFinance.StockIndicators.Builder.Specs;
using OoplesFinance.StockIndicators.Core;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Streaming;
using OoplesFinance.StockIndicators.Validation;

namespace OoplesFinance.StockIndicators.Tests.Unit.ValidationTests;

public sealed class VolumeNormalizationNumericalTests
{
    public static IEnumerable<object[]> Cases => IndicatorValidationDiscovery.Discover(new[] { typeof(IIndicator).Assembly })
        .Where(c => c.IndicatorType == typeof(NetVolume) || c.IndicatorType == typeof(NormalizedVolume))
        .Select(c => new object[] { c });

    [Theory, MemberData(nameof(Cases))]
    public async Task EveryConfigurationReceivesNumericalAndSignedOverflowCases(IndicatorValidationCase testCase)
    {
        var report = await IndicatorValidation.ValidateAsync(testCase);
        report.ThrowIfInvalid();
        foreach (var fixture in IndicatorAdversarialCases.Generate(256, 244))
            Assert.Contains(report.FixtureEvidence, f => f.Name == fixture.Name && f.Completed && f.Passed);
        if (testCase.Factory() is NormalizedVolume normalized && normalized.Length >= 3)
            foreach (var sign in new[] { "positive", "negative" })
                Assert.Equal(2, Assert.Single(report.FixtureEvidence,
                    f => f.Name == "normalized-volume-" + sign + "-output-overflow").OutputOverflowRejectionsChecked);
    }

    [Theory]
    [InlineData(1, false)]
    [InlineData(2, false)]
    [InlineData(20, false)]
    [InlineData(1, true)]
    [InlineData(3, true)]
    [InlineData(2, true)]
    [InlineData(20, true)]
    public async Task EveryRouteMatchesTheIndependentVolumeContract(int length, bool normalized)
    {
        foreach (var fixture in IndicatorAdversarialCases.Generate(32, 244).Concat(new[] {
            new IndicatorValidationFixture("signed-cancellation-and-eviction", Enumerable.Range(0, 2 * length + 4).Select(i => {
                var volume = i == 0 ? double.Epsilon : i == length - 2 ? -double.MaxValue : i == length - 1 ? double.MaxValue : 1;
                var price = i % 3 == 0 ? double.MaxValue : i % 3 == 1 ? -double.MaxValue : -double.MaxValue;
                return new Bar(DateTime.UnixEpoch.AddMinutes(i), price, price, price, price, volume);
            })) }))
        {
            var bars = fixture.Bars;
            var expected = normalized ? BuiltInFormulaReferences.RoundedNormalizedVolume(bars, length) : BuiltInFormulaReferences.RoundedNetVolume(bars);
            var data = Data(bars);
            if (normalized) data.CalculateNormalizedVolume(length); else data.CalculateNetVolume();
            Assert.Equal(expected, data.OutputValues[normalized ? "NormalizedVolume" : "NetVolume"]);
            var values = bars.Select(b => b.Volume).ToArray();
            var core = new double[bars.Count];
            if (normalized) VolumeCore.NormalizedVolume(values, core, length); else VolumeCore.NetVolume(bars.Select(b => b.Close).ToArray(), values, core);
            Assert.Equal(expected, core);
            IIndicator indicator = normalized ? new NormalizedVolume(length) : new NetVolume(length);
            if (expected.Any(double.IsInfinity))
                await Assert.ThrowsAsync<IndicatorOutputException>(() => new StockIndicatorBuilder()
                    .ConfigureSource(Bars.From(bars)).ConfigureIndicators(indicator).BuildAsync());
            else
            {
                using var run = await new StockIndicatorBuilder().ConfigureSource(Bars.From(bars)).ConfigureIndicators(indicator).BuildAsync();
                Assert.Equal(expected, run[indicator].ToArray());
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
                        var native = new OhlcvBar("DIFFERENCE", BarTimeframe.Minutes(1), b.Time, b.Time, b.Open, b.High, b.Low, b.Close, b.Volume, true);
                        Assert.Equal(expected[i], state.Update(native, commit, true).Value);
                    }
                }
            }
        }
    }

    [Fact]
    public async Task ChainingUsesSelectedPriceForDirectionAndKeepsVolumeForNormalization()
    {
        var bars = new[] { 100d, 120, 110, 150 }.Select((v, i) =>
            new Bar(DateTime.UnixEpoch.AddMinutes(i), v, v, v, v, Math.Pow(2, i))).ToArray();
        var source = new Sma(2);
        var net = new NetVolume(14).Of(source);
        var normalized = new NormalizedVolume(3).Of(source);
        using var run = await new StockIndicatorBuilder().ConfigureSource(Bars.From(bars))
            .ConfigureIndicators(source, net, normalized).BuildAsync();
        var projected = run[source].ToArray().Select((v, i) => new Bar(bars[i].Time, v, v, v, v, bars[i].Volume)).ToArray();
        Assert.Equal(BuiltInFormulaReferences.RoundedNetVolume(projected), run[net].ToArray());
        Assert.Equal(BuiltInFormulaReferences.RoundedNormalizedVolume(bars, 3), run[normalized].ToArray());
        var data = Data(bars);
        _ = data.TickerDataList;
        data.Volumes = new() { 3, 3, 3, 3 };
        data.InputValues = new() { 1, 1, 0, 2 };
        using var context = new ComputeContext();
        using var normalizedResult = IndicatorCompute.ComputeNormalizedVolumeFast(data, context, 3);
        using var netResult = IndicatorCompute.ComputeNetVolumeFast(data, context, 14);
        Assert.Equal(new[] { 0d, 0, 1, 1 }, normalizedResult.ToArray());
        Assert.Equal(new[] { 0d, 0, -3, 3 }, netResult.ToArray());
    }

    private static StockData Data(IReadOnlyList<Bar> bars) => new(bars.Select(b => b.Open), bars.Select(b => b.High),
        bars.Select(b => b.Low), bars.Select(b => b.Close), bars.Select(b => b.Volume), bars.Select(b => b.Time));
}

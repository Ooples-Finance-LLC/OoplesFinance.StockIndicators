using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Builder.Compute;
using OoplesFinance.StockIndicators.Builder.Specs;
using OoplesFinance.StockIndicators.Core;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Streaming;
using OoplesFinance.StockIndicators.Validation;

namespace OoplesFinance.StockIndicators.Tests.Unit.ValidationTests;

public sealed class LaggedDifferenceNumericalTests
{
    public static IEnumerable<object[]> Cases => IndicatorValidationDiscovery.Discover(new[] { typeof(IIndicator).Assembly })
        .Where(c => c.IndicatorType == typeof(PriceMomentum) || c.IndicatorType == typeof(VolumeMomentum))
        .Select(c => new object[] { c });

    [Theory, MemberData(nameof(Cases))]
    public async Task EveryConfigurationReceivesNumericalAndSignedOverflowCases(IndicatorValidationCase testCase)
    {
        var report = await IndicatorValidation.ValidateAsync(testCase);
        report.ThrowIfInvalid();
        foreach (var fixture in IndicatorAdversarialCases.Generate(256, 244))
            Assert.Contains(report.FixtureEvidence, f => f.Name == fixture.Name && f.Completed && f.Passed);
        var prefix = testCase.IndicatorType == typeof(PriceMomentum) ? "price-difference-" : "volume-difference-";
        foreach (var sign in new[] { "positive", "negative" })
            Assert.Equal(2, Assert.Single(report.FixtureEvidence, f => f.Name == prefix + sign + "-output-overflow").OutputOverflowRejectionsChecked);
    }

    [Theory]
    [InlineData(1, false)]
    [InlineData(3, false)]
    [InlineData(10, false)]
    [InlineData(1, true)]
    [InlineData(3, true)]
    [InlineData(10, true)]
    public async Task EveryRouteMatchesTheIndependentDifference(int length, bool volume)
    {
        foreach (var fixture in IndicatorAdversarialCases.Generate(32, 244))
        {
            var bars = fixture.Bars;
            var expected = BuiltInFormulaReferences.RoundedLaggedDifference(bars, length, volume);
            var data = Data(bars);
            if (volume) data.CalculateVolumeMomentum(length); else data.CalculatePriceMomentum(length);
            Assert.Equal(expected, data.OutputValues[volume ? "VolumeMomentum" : "Pm"]);
            var values = bars.Select(b => volume ? b.Volume : b.Close).ToArray();
            var core = new double[bars.Count];
            if (volume) VolumeCore.VolumeMomentum(values, core, length); else TrendCore.PriceMomentum(values, core, length);
            Assert.Equal(expected, core);
            IIndicator indicator = volume ? new VolumeMomentum(length) : new PriceMomentum(length);
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
    public async Task SelectedPricesDoNotChangeVolumeAndUpdatedColumnsAreUsed()
    {
        var bars = new[] { 100d, 120, 110, 150 }.Select((v, i) =>
            new Bar(DateTime.UnixEpoch.AddMinutes(i), v, v, v, v, Math.Pow(2, i))).ToArray();
        var source = new Sma(2);
        var price = new PriceMomentum(1).Of(source);
        var volume = new VolumeMomentum(1).Of(source);
        using var run = await new StockIndicatorBuilder().ConfigureSource(Bars.From(bars))
            .ConfigureIndicators(source, price, volume).BuildAsync();
        var projected = run[source].ToArray().Select((v, i) => new Bar(bars[i].Time, v, v, v, v, 1)).ToArray();
        Assert.Equal(BuiltInFormulaReferences.RoundedLaggedDifference(projected, 1, false), run[price].ToArray());
        Assert.Equal(new[] { 0d, 1, 2, 4 }, run[volume].ToArray());
        var data = Data(bars);
        _ = data.TickerDataList;
        data.Volumes = new() { 3, 3, 3, 3 };
        data.InputValues = new() { 1, 2, 4, 8 };
        using var context = new ComputeContext();
        using var volumeResult = IndicatorCompute.ComputeVolumeMomentumFast(data, context, 1);
        using var priceResult = IndicatorCompute.ComputePriceMomentumFast(data, context, 1);
        Assert.Equal(new double[4], volumeResult.ToArray());
        Assert.Equal(new[] { 0d, 1, 2, 4 }, priceResult.ToArray());
    }

    private static StockData Data(IReadOnlyList<Bar> bars) => new(bars.Select(b => b.Open), bars.Select(b => b.High),
        bars.Select(b => b.Low), bars.Select(b => b.Close), bars.Select(b => b.Volume), bars.Select(b => b.Time));
}

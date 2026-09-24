using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Builder.Specs;
using OoplesFinance.StockIndicators.Core;
using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Streaming;
using OoplesFinance.StockIndicators.Validation;

namespace OoplesFinance.StockIndicators.Tests.Unit.ValidationTests;

public sealed class MoneyFlowNumericalTests
{
    public static IEnumerable<object[]> Cases => IndicatorValidationDiscovery.Discover(new[] { typeof(IIndicator).Assembly })
        .Where(c => c.IndicatorType == typeof(Mfi) || c.IndicatorType == typeof(MfiCore)).Select(c => new object[] { c });

    [Theory, MemberData(nameof(Cases))]
    public async Task EveryConfigurationReceivesNumericalCases(IndicatorValidationCase testCase)
    {
        var report = await IndicatorValidation.ValidateAsync(testCase);
        report.ThrowIfInvalid();
        foreach (var fixture in IndicatorAdversarialCases.Generate(256, 244))
            Assert.Contains(report.FixtureEvidence, f => f.Name == fixture.Name && f.Completed && f.Passed);

    }

    [Theory]
    [InlineData(1)]
    [InlineData(3)]
    [InlineData(14)]
    public async Task EveryRouteMatchesTheIndependentFlowIncludingPreviewAndReset(int length)
    {
        foreach (var fixture in IndicatorAdversarialCases.Generate(32, 244).Concat(SpecialCases()))
        {
            var bars = fixture.Bars;
            var expected = BuiltInFormulaReferences.RoundedMoneyFlow(bars, length);
            var data = new StockData(bars.Select(b => b.Open), bars.Select(b => b.High), bars.Select(b => b.Low),
                bars.Select(b => b.Close), bars.Select(b => b.Volume), bars.Select(b => b.Time));
            data.CalculateMoneyFlowIndex(length);
            Assert.Equal(expected, data.OutputValues["Mfi"]);
            var core = new double[bars.Count];
            VolumeCore.MoneyFlowIndex(bars.Select(b => b.High).ToArray(), bars.Select(b => b.Low).ToArray(), bars.Select(b => b.Close).ToArray(), bars.Select(b => b.Volume).ToArray(), core, length);
            Assert.Equal(expected, core);
            OscillatorCore.MoneyFlowIndex(bars.Select(b => b.High).ToArray(), bars.Select(b => b.Low).ToArray(), bars.Select(b => b.Close).ToArray(), bars.Select(b => b.Volume).ToArray(), core, length);
            Assert.Equal(expected, core);
            foreach (IIndicator indicator in new IIndicator[] { new Mfi(length), new MfiCore(length) })
            {
                using var run = await new StockIndicatorBuilder().ConfigureSource(Bars.From(bars)).ConfigureIndicators(indicator).BuildAsync();
                Assert.Equal(expected, run[indicator].ToArray());
                Assert.All(run[indicator].ToArray(), value => Assert.InRange(value, 0, 100));
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
                            var native = new OhlcvBar("MFI", BarTimeframe.Minutes(1), b.Time, b.Time, b.Open, b.High, b.Low, b.Close, b.Volume, true);
                            Assert.Equal(expected[i], state.Update(native, commit, true).Value);
                        }
                    }
                }
            }
        }
    }

    private static IEnumerable<IndicatorValidationFixture> SpecialCases()
    {
        yield return Fixture("overflowing-products", new[] { double.MaxValue / 2, double.MaxValue, double.MaxValue / 2 }, new[] { double.MaxValue, double.MaxValue, double.MaxValue });
        yield return Fixture("underflowing-products", new[] { double.Epsilon, 2 * double.Epsilon, double.Epsilon }, new[] { double.Epsilon, double.Epsilon, double.Epsilon });
        yield return Fixture("zero-total", new[] { 1d, 2, 1 }, new[] { 1d, 1, -2 });
        yield return Fixture("ties-and-expiry", new[] { 1d, 2, 1, 1, 3, 3, 3, 3, 2 }, Enumerable.Repeat(1d, 9).ToArray());
    }

    private static IndicatorValidationFixture Fixture(string name, double[] prices, double[] volumes) => new(name,
        prices.Select((v, i) => new Bar(DateTime.UnixEpoch.AddMinutes(i), v, v, v, v, volumes[i])));

    [Fact]
    public void ExtremeProductsRetainFiniteRatioAndZeroFlowConventions()
    {
        foreach (var fixture in SpecialCases().Take(2))
        {
            using var state = new RollingMoneyFlowIndex(3);
            var actual = fixture.Bars.Select(b => state.Next(b.Close, b.Volume, true)).ToArray();
            Assert.Equal(new[] { 100d, 100, 200d / 3 }, actual);
        }
        using var zero = new RollingMoneyFlowIndex(3);
        Assert.Equal(100, zero.Next(1, 1, true));
        Assert.Equal(100, zero.Next(2, 1, true));
        Assert.Equal(0, zero.Next(1, -2, true));
    }

    [Fact]
    public async Task TypedChainingAndBuildersUseSelectedPricesAndCurrentColumns()
    {
        var bars = Fixture("selection", new[] { 100d, 120, 110, 150, 90 }, new[] { 1d, 2, 3, 4, 5 }).Bars;
        var source = new Sma(2);
        var first = new Mfi(3).Of(source);
        var second = new MfiCore(3).Of(source);
        using var run = await new StockIndicatorBuilder().ConfigureSource(Bars.From(bars)).ConfigureIndicators(source, first, second).BuildAsync();
        var projected = run[source].ToArray().Select((v, i) => new Bar(bars[i].Time, bars[i].Open, bars[i].High, bars[i].Low, v, bars[i].Volume)).ToArray();
        var expected = BuiltInFormulaReferences.RoundedMoneyFlow(projected, 3, selectedClose: true);
        Assert.Equal(expected, run[first].ToArray());
        Assert.Equal(expected, run[second].ToArray());
        using var fused = await new StockIndicatorBuilder().ConfigureSource(Bars.From(bars)).ConfigureIndicators(first).BuildAsync();
        Assert.Equal(expected, fused[first].ToArray());
        var feed = Bars.Live();
        using var live = await new StockIndicatorBuilder().ConfigureSource(feed).ConfigureIndicators(source, first, second).PublishBeforeWarmup().BuildAsync();
        foreach (var bar in bars) feed.Publish(bar);
        feed.Complete();
        await foreach (var snapshot in live) { }
        Assert.Equal(expected, live[first].ToArray());
        Assert.Equal(expected, live[second].ToArray());
        var data = new StockData(bars.Select(b => b.Open), bars.Select(b => b.High), bars.Select(b => b.Low),
            bars.Select(b => b.Close), bars.Select(b => b.Volume), bars.Select(b => b.Time));
        _ = data.TickerDataList;
        data.HighPrices = new() { 1, 2, 1, 3, 2 };
        data.LowPrices = new(data.HighPrices);
        data.ClosePrices = new(data.HighPrices);
        data.Volumes = new() { 2, 3, 4, 5, 6 };
        var current = data.ClosePrices.Select((v, i) => new Bar(bars[i].Time, 0, v, v, v, data.Volumes[i])).ToArray();
        using var context = new OoplesFinance.StockIndicators.Builder.Compute.ComputeContext();
        using var updated = OoplesFinance.StockIndicators.Builder.Compute.IndicatorCompute.ComputeMoneyFlowIndexFast(data, context, 3);
        Assert.Equal(BuiltInFormulaReferences.RoundedMoneyFlow(current, 3), updated.ToArray());
        data.SetInputSeries(projected.Select(b => b.Close).ToList());
        var selected = projected.Select((b, i) => new Bar(b.Time, b.Open, b.High, b.Low, b.Close, data.Volumes[i])).ToArray();
        expected = BuiltInFormulaReferences.RoundedMoneyFlow(selected, 3, selectedClose: true);
        using var chained = OoplesFinance.StockIndicators.Builder.Compute.IndicatorCompute.ComputeMoneyFlowIndexFast(data, context, 3);
        using var alias = OoplesFinance.StockIndicators.Builder.Compute.IndicatorCompute.ComputeMfiCoreFast(data, context, 3);
        Assert.Equal(expected, chained.ToArray());
        Assert.Equal(expected, alias.ToArray());
        data.CalculateMoneyFlowIndex(3);
        Assert.Equal(expected, data.OutputValues["Mfi"]);
        using var native = new MoneyFlowIndexState(3);
        ((ICustomInputConsumer)native).ReadCloseAsInput();
        for (var i = 0; i < selected.Length; i++)
        {
            var b = selected[i];
            var bar = new OhlcvBar("MFI", BarTimeframe.Minutes(1), b.Time, b.Time, b.Open, b.High, b.Low, b.Close, b.Volume, true);
            Assert.Equal(expected[i], native.Update(bar, false, true).Value);
            Assert.Equal(expected[i], native.Update(bar, true, true).Value);
        }
    }
}

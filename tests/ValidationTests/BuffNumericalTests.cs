using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Builder.Compute;
using OoplesFinance.StockIndicators.Builder.Specs;
using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Streaming;
using OoplesFinance.StockIndicators.Validation;
namespace OoplesFinance.StockIndicators.Tests.Unit.ValidationTests;
public sealed class BuffNumericalTests
{
    private static StockData Data(Bar[] bars) => new(bars.Select(b => b.Open), bars.Select(b => b.High), bars.Select(b => b.Low), bars.Select(b => b.Close), bars.Select(b => b.Volume), bars.Select(b => b.Time));
    private static OhlcvBar Native(Bar b) => new("BUFF", BarTimeframe.Minutes(1), b.Time, b.Time, b.Open, b.High, b.Low, b.Close, b.Volume, true);
    private static Bar[] Bars(double[] prices, double[] volumes) => prices.Select((v, i) => new Bar(DateTime.UnixEpoch.AddDays(i), v, v, v, v, volumes[i % volumes.Length])).ToArray();
    public static IEnumerable<object[]> Cases => IndicatorValidationDiscovery.Discover(new[] { typeof(IIndicator).Assembly }).Where(c => c.IndicatorType == typeof(BuffAverage)).Select(c => new object[] { c });
    public static IEnumerable<object[]> Routes => Cases.SelectMany(c => new[] { "batch", "fast", "arm", "native", "streaming" }.Select(route => new object[] { c[0], route }));
    [Theory, MemberData(nameof(Routes))]
    public void EveryRouteMatchesExactPartialWindowWeights(IndicatorValidationCase c, string route)
    {
        var options = (BuffAverageSpecOptions)((IBuiltInIndicator)c.Factory()).CreateOptions();
        new OrdinalFamilyNumericalTests().CheckRoutes(c, route, bars => BuiltInFormulaReferences.BuffOutputs(bars, options.Length), IndicatorErrorBudget.Exact);
    }
    [Theory, MemberData(nameof(Cases))]
    public Task SelectedSourcePreservesOriginalVolume(IndicatorValidationCase c) => new OrdinalFamilyNumericalTests().SelectedSourcePreservesTheFormulaAndOriginalCandleFields(c);
    [Theory, MemberData(nameof(Cases))]
    public void EveryOutputRejectsInjectedFaults(IndicatorValidationCase c) => new OrdinalFamilyNumericalTests().EveryPublishedOutputRejectsAnInjectedValueFault(c);
    [Theory, MemberData(nameof(Cases))]
    public Task NumericalClassesAreEnrolled(IndicatorValidationCase c) => new OrdinalFamilyNumericalTests().PublicConfigurationsPassEveryNumericalClass(c);
    private static void Check(Bar[] bars, int fast, int slow)
    {
        var expected = BuiltInFormulaReferences.BuffOutputs(bars, fast, slow);
        var batch = Data(bars).CalculateBuffAverage(fast, slow);
        foreach (var key in expected.Keys) Assert.Equal(expected[key], batch.OutputValues[key]);
        using var context = new ComputeContext(); using var raw = IndicatorCompute.ComputeBuffAverageFast(Data(bars), context, fast);
        Assert.Equal(expected["FastBuff"], raw.ToArray());
        using var state = new BuffAverageState(fast, slow);
        for (var replay = 0; replay < 2; replay++)
        {
            state.Reset();
            for (var i = 0; i < bars.Length; i++)
            {
                state.Update(Native(Bars(new[] { -double.MaxValue }, new[] { 17d })[0]), false, false);
                foreach (var final in new[] { false, false, true })
                {
                    var actual = state.Update(Native(bars[i]), final, true);
                    Assert.Equal(expected["FastBuff"][i], actual.Value);
                    foreach (var key in expected.Keys) Assert.Equal(expected[key][i], actual.Outputs![key]);
                }
            }
        }
    }
    [Fact]
    public void HandWeightsUsePartialWindowsAndDistinctPeriods()
    {
        var bars = Bars(new[] { 2d, 8, 4, 10 }, new[] { 1d, 3, 0, 2 });
        var expected = BuiltInFormulaReferences.BuffOutputs(bars, 2, 3);
        Assert.Equal(new[] { 2d, 6.5, 8, 10 }, expected["FastBuff"]);
        Assert.Equal(new[] { 2d, 6.5, 6.5, 8.8 }, expected["SlowBuff"]); Check(bars, 2, 3);
    }
    [Fact]
    public void ProductsAndTotalsRemainExactAcrossExtremePricesAndVolumes()
    {
        foreach (var length in new[] { 1, 2, 5, int.MaxValue })
        foreach (var volume in new[] { double.Epsilon, 1d, double.MaxValue })
        {
            Check(Bars(new[] { double.MaxValue, -double.MaxValue, double.MaxValue, 1d, 0, 0, 2, 3 }, new[] { volume }), length, 3);
            Check(Bars(new[] { double.Epsilon, 2 * double.Epsilon, 4 * double.Epsilon, 0d, 1, -1 }, new[] { volume }), length, int.MaxValue);
        }
        Check(Bars(new[] { 2d, 8, 4, 10, 3, 2 }, new[] { 0d, 0, 1 }), 2, 3);
        Check(Array.Empty<Bar>(), 5, 20);
    }
    [Fact]
    public void SelectedRawPricesKeepOriginalVolumes()
    {
        var bars = Bars(new[] { 100d, 200, 300, 400 }, new[] { 1d, 3, 0, 2 }); var selected = new[] { 2d, 8, 4, 10 };
        var expected = BuiltInFormulaReferences.BuffOutputs(Bars(selected, new[] { 1d, 3, 0, 2 }), 2)["FastBuff"];
        var data = Data(bars); data.SetCustomValues(selected.ToList()); using var context = new ComputeContext();
        using var raw = IndicatorCompute.ComputeBuffAverageFast(data, context, 2); Assert.Equal(expected, raw.ToArray());
        Assert.Equal(expected, data.CalculateBuffAverage(2).OutputValues["FastBuff"]);
    }
    [Fact]
    public void InvalidFieldsDoNotAdvanceEitherWindow()
    {
        foreach (var field in Enumerable.Range(0, 5)) foreach (var invalid in new[] { double.NaN, double.PositiveInfinity, double.NegativeInfinity }) foreach (var final in new[] { false, true })
        {
            using var state = new BuffAverageState(2, 3); using var control = new BuffAverageState(2, 3);
            var seed = Native(Bars(new[] { 2d }, new[] { 3d })[0]); state.Update(seed, true, true); control.Update(seed, true, true);
            var v = new[] { 1d, 3, 0, 2, 1 }; v[field] = invalid;
            Assert.Throws<ArgumentOutOfRangeException>(() => state.Update(Native(new Bar(DateTime.UnixEpoch, v[0], v[1], v[2], v[3], v[4])), final, true));
            foreach (var b in Bars(new[] { 7d, 4, 3, 0 }, new[] { 1d, 2 }))
            {
                var expected = control.Update(Native(b), true, true); var actual = state.Update(Native(b), true, true);
                foreach (var key in expected.Outputs!.Keys) Assert.Equal(expected.Outputs[key], actual.Outputs![key]);
            }
        }
    }
}

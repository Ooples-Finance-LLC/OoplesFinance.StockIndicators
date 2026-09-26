using ChannelBand = OoplesFinance.StockIndicators.Builder.Compute.IndicatorCompute.ChannelBand;
using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Builder.Compute;
using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Streaming;
using OoplesFinance.StockIndicators.Validation;
namespace OoplesFinance.StockIndicators.Tests.Unit.ValidationTests;
public sealed class AtrPercentBandNumericalTests
{
    private static StockData Data(Bar[] bars) => new(bars.Select(b => b.Open), bars.Select(b => b.High), bars.Select(b => b.Low), bars.Select(b => b.Close), bars.Select(b => b.Volume), bars.Select(b => b.Time));
    private static OhlcvBar Native(Bar b) => new("APB", BarTimeframe.Minutes(1), b.Time, b.Time, b.Open, b.High, b.Low, b.Close, b.Volume, true);
    [Fact]
    public void StartupZeroDenominatorsTiesAndExtremeArithmeticMatchIndependentFractions()
    {
        var ordinary = Enumerable.Range(0, 14).Select(i => new Bar(DateTime.UnixEpoch.AddMinutes(i), i % 4 - 2, i % 5 + 2, -(i % 3 + 1), i % 7 - 3, 1)).ToArray();
        foreach (var length in new[] { 1, 3, 14 }) foreach (var center in new[] { 1, 2, 7 })
        foreach (var kind in new[] { MovingAvgType.SimpleMovingAverage, MovingAvgType.WeightedMovingAverage, MovingAvgType.ExponentialMovingAverage, MovingAvgType.WildersSmoothingMethod })
        foreach (var multiplier in new[] { -2d, 0, .5, double.MaxValue }) foreach (var scale in new[] { 1d, double.Epsilon, double.MaxValue / 16 })
            Check(ordinary.Select(b => new Bar(b.Time, b.Open * scale, b.High * scale, b.Low * scale, b.Close * scale, 1)).ToArray(), length, center, kind, multiplier);
        Check(Array.Empty<Bar>(), 14, 20, MovingAvgType.SimpleMovingAverage, 2);
        var bars = new[] { new Bar(DateTime.UnixEpoch, 10, 11, 9, 10, 1), new Bar(DateTime.UnixEpoch.AddMinutes(1), 10, 11, 9, 10, 1) };
        var batch = Data(bars).CalculateBollingerBandsWithAtrPct(length: 1, bbLength: 1);
        Assert.Equal(new[] { 50d, 14 }, batch.OutputValues["UpperBand"]); Assert.Equal(new[] { -30d, 6 }, batch.OutputValues["LowerBand"]);
        var edges = new[] { new Bar(DateTime.UnixEpoch, 0, 1, -1, 1, 1), new Bar(DateTime.UnixEpoch.AddMinutes(1), 0, 1, -3, -1, 1), new Bar(DateTime.UnixEpoch.AddMinutes(2), 0, 3, -3, 1, 1), new Bar(DateTime.UnixEpoch.AddMinutes(3), 0, double.MaxValue, -double.MaxValue, 2, 1) };
        Check(edges, 3, 1, MovingAvgType.ExponentialMovingAverage, 2);
        Check(new[] { new Bar(DateTime.UnixEpoch, 2, 2, 2, 2, 1), new Bar(DateTime.UnixEpoch.AddMinutes(1), -1, -1, -1, -1, 1) }, 3, 1, MovingAvgType.ExponentialMovingAverage, 2);
    }
    private static void Check(Bar[] bars, int length, int center, MovingAvgType kind, double multiplier)
    {
        var code = kind == MovingAvgType.SimpleMovingAverage ? 1 : kind == MovingAvgType.WeightedMovingAverage ? 2 : kind == MovingAvgType.ExponentialMovingAverage ? 3 : 6;
        var expected = BuiltInFormulaReferences.AtrPercentBandOutputs(bars, length, center, code, multiplier);
        var batch = Data(bars).CalculateBollingerBandsWithAtrPct(kind, length, center, multiplier);
        using var context = new ComputeContext();
        foreach (var key in expected.Keys)
        {
            Assert.Equal(expected[key], batch.OutputValues[key]);
            using var raw = IndicatorCompute.ComputeBollingerBandsWithAtrPctFast(Data(bars), context, length, center, multiplier, kind, key == "UpperBand" ? ChannelBand.Upper : key == "LowerBand" ? ChannelBand.Lower : ChannelBand.Middle);
            Assert.Equal(expected[key], raw.Span.ToArray());
        }
        using var state = new BollingerBandsWithAtrPctState(kind, length, center, multiplier);
        for (var replay = 0; replay < 2; replay++)
        {
            state.Reset();
            for (var i = 0; i < bars.Length; i++)
            {
                state.Update(Native(new Bar(bars[i].Time, 3, 4, -2, -1, 7)), false, true);
                foreach (var final in new[] { false, false, true })
                { var result = state.Update(Native(bars[i]), final, true); foreach (var key in expected.Keys) Assert.Equal(expected[key][i], result.Outputs![key]); }
            }
        }
    }
    [Fact]
    public void SelectedInputAndCustomerBasisRetainPeriodAndCandleRange()
    {
        var bars = Enumerable.Range(0, 3).Select(i => new Bar(DateTime.UnixEpoch.AddMinutes(i), 10, 11, 9, 10, 1)).ToArray();
        var selected = new[] { 9d, 10, 11 }; using var context = new ComputeContext();
        foreach (var batch in new[] { false, true })
        {
            var data = Data(bars); data.SetCustomValues(selected.ToList());
            using var armed = ComponentAverage.Arm(new Func<IReadOnlyList<double>, int, IReadOnlyList<double>>[] {
                (values, period) => { Assert.Equal(3, period); Assert.Equal(selected, values); return new[] { 10d, 10, 10 }; } });
            if (batch) Assert.Equal(new[] { 50d, 14, 14 }, data.CalculateBollingerBandsWithAtrPct(length: 1, bbLength: 3).OutputValues["UpperBand"]);
            else { using var raw = IndicatorCompute.ComputeBollingerBandsWithAtrPctFast(data, context, 1, 3, band: ChannelBand.Upper); Assert.Equal(new[] { 50d, 14, 14 }, raw.Span.ToArray()); }
            Assert.Equal(1, ComponentAverage.Substitutions);
        }
        var expected = BuiltInFormulaReferences.AtrPercentBandOutputs(bars.Select((b,i) => new Bar(b.Time,b.Open,b.High,b.Low,selected[i],b.Volume)).ToArray(), 1, 1, 1, 2);
        var selectedData = Data(bars); selectedData.SetCustomValues(selected.ToList()); using var actual = IndicatorCompute.ComputeBollingerBandsWithAtrPctFast(selectedData, context, 1, 1, band: ChannelBand.Upper); Assert.Equal(expected["UpperBand"], actual.Span.ToArray());
    }
    [Fact]
    public void InvalidInputsDoNotMutateNativeState()
    {
        foreach (var field in Enumerable.Range(0, 5)) foreach (var invalid in new[] { double.NaN, double.PositiveInfinity, double.NegativeInfinity }) foreach (var final in new[] { false, true })
        {
            using var state = new BollingerBandsWithAtrPctState(length: 2, bbLength: 2); using var control = new BollingerBandsWithAtrPctState(length: 2, bbLength: 2);
            var first = Native(new Bar(DateTime.UnixEpoch, 2, 3, 1, 2, 1)); state.Update(first,true,true); control.Update(first,true,true);
            var values = new[] { 2d,4,1,2,1 }; values[field] = invalid;
            var bad = new OhlcvBar("APB",BarTimeframe.Minutes(1),DateTime.UnixEpoch,DateTime.UnixEpoch,values[0],values[1],values[2],values[3],values[4],true);
            Assert.Throws<ArgumentOutOfRangeException>(() => state.Update(bad,final,true));
            var next = Native(new Bar(DateTime.UnixEpoch.AddMinutes(1),4,5,3,4,1)); Assert.Equal(control.Update(next,true,true).Outputs!,state.Update(next,true,true).Outputs!);
        }
    }
    [Fact]
    public void BatchOnlyAveragesStillUseTheirSelectedBasis()
    {
        var bars = Enumerable.Range(0, 12).Select(i => new Bar(DateTime.UnixEpoch.AddMinutes(i), 10, 11, 9, 10+i%2,1)).ToArray();
        var basis = CalculationsHelper.GetMovingAverageList(Data(bars),MovingAvgType.HullMovingAverage,3,bars.Select(b=>b.Close).ToList());
        var data = Data(bars).CalculateBollingerBandsWithAtrPct(MovingAvgType.HullMovingAverage,1,3);
        using var context = new ComputeContext(); using var raw = IndicatorCompute.ComputeBollingerBandsWithAtrPctFast(Data(bars),context,1,3,maType:MovingAvgType.HullMovingAverage,band:ChannelBand.Upper);
        Assert.Equal(basis,data.OutputValues["MiddleBand"]); Assert.Equal(data.OutputValues["UpperBand"],raw.Span.ToArray());
    }
    public static IEnumerable<object[]> Cases => IndicatorValidationDiscovery.Discover(new[] { typeof(IIndicator).Assembly }).Where(c => c.IndicatorType == typeof(BollingerBandsWithAtrPct)).Select(c => new object[] { c });
    public static IEnumerable<object[]> Routes => Cases.SelectMany(c => new[] { "batch", "fast", "arm", "native", "streaming" }.Select(route => new object[] { c[0], route }));
    [Theory, MemberData(nameof(Routes))]
    public void AllRoutesMatchIndependentFormulasIncludingPreviewAndReset(IndicatorValidationCase testCase, string route) => new OrdinalFamilyNumericalTests().CheckRoutes(testCase, route, bars => BuiltInFormulaReferences.AtrPercentBandOutputs(bars, (IBuiltInIndicator)testCase.Factory()), IndicatorErrorBudget.Exact);
    [Theory, MemberData(nameof(Cases))]
    public Task SelectedSourcePreservesTheFormulaAndOriginalCandleFields(IndicatorValidationCase testCase) => new OrdinalFamilyNumericalTests().SelectedSourcePreservesTheFormulaAndOriginalCandleFields(testCase);
    [Theory, MemberData(nameof(Cases))]
    public void EveryPublishedOutputRejectsAnInjectedValueFault(IndicatorValidationCase testCase) => new OrdinalFamilyNumericalTests().EveryPublishedOutputRejectsAnInjectedValueFault(testCase);
    [Theory, MemberData(nameof(Cases))]
    public Task PublicConfigurationsPassEveryNumericalClass(IndicatorValidationCase testCase) => new OrdinalFamilyNumericalTests().PublicConfigurationsPassEveryNumericalClass(testCase);
}

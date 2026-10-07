using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Builder.Compute;
using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Streaming;
using OoplesFinance.StockIndicators.Validation;
using Band = OoplesFinance.StockIndicators.Builder.Compute.IndicatorCompute.SupportResistanceBand;
namespace OoplesFinance.StockIndicators.Tests.Unit.ValidationTests;
public sealed class DynamicSupportNumericalTests
{
    private static StockData Data(Bar[] bars) => new(bars.Select(b => b.Open), bars.Select(b => b.High), bars.Select(b => b.Low), bars.Select(b => b.Close), bars.Select(b => b.Volume), bars.Select(b => b.Time));
    private static OhlcvBar Native(Bar b) => new("DSR", BarTimeframe.Minutes(1), b.Time, b.Time, b.Open, b.High, b.Low, b.Close, b.Volume, true);
    [Fact]
    public void RollingExtremaAndAtrOffsetsPreserveTheExactMidpoint()
    {
        foreach (var length in new[] { 1, 2, 4, 7 })
        foreach (var kind in new[] { MovingAvgType.SimpleMovingAverage, MovingAvgType.WeightedMovingAverage, MovingAvgType.ExponentialMovingAverage, MovingAvgType.WildersSmoothingMethod })
        foreach (var scale in new[] { 1d, double.Epsilon, double.MaxValue / 16 })
            Check(Enumerable.Range(0, 20).Select(i => new Bar(DateTime.UnixEpoch.AddMinutes(i), (i % 7 - 3) * scale, (i % 5 + 2) * scale, -(i % 3 + 1) * scale, (i % 9 - 4) * scale, 1)).ToArray(), length, kind);
        Check(Array.Empty<Bar>(), 25, MovingAvgType.WildersSmoothingMethod);
        var bars = Enumerable.Range(0, 8).Select(i => new Bar(DateTime.UnixEpoch.AddMinutes(i), 0, double.MaxValue, -double.MaxValue, i % 2 == 0 ? double.MaxValue : -double.MaxValue, 1)).ToArray();
        Check(bars, 4, MovingAvgType.ExponentialMovingAverage);
        var batch = Data(bars).CalculateDynamicSupportAndResistance(MovingAvgType.ExponentialMovingAverage, 4);
        Assert.All(batch.OutputValues["MiddleBand"], v => Assert.Equal(0, v)); Assert.All(batch.OutputValues["Support"], v => Assert.Equal(double.NegativeInfinity, v));
        var simple = new[] { new Bar(DateTime.UnixEpoch,10,12,8,10,1) };
        batch = Data(simple).CalculateDynamicSupportAndResistance(length:4);
        Assert.Equal(10,batch.OutputValues["Support"][0]); Assert.Equal(10,batch.OutputValues["Resistance"][0]); Assert.Equal(10,batch.OutputValues["MiddleBand"][0]);
    }
    private static void Check(Bar[] bars, int length, MovingAvgType kind)
    {
        var code = kind == MovingAvgType.SimpleMovingAverage ? 1 : kind == MovingAvgType.WeightedMovingAverage ? 2 : kind == MovingAvgType.ExponentialMovingAverage ? 3 : 6;
        var expected = BuiltInFormulaReferences.DynamicSupportOutputs(bars, length, code); var batch = Data(bars).CalculateDynamicSupportAndResistance(kind,length);
        using var context = new ComputeContext();
        foreach (var key in expected.Keys)
        {
            Assert.Equal(expected[key], batch.OutputValues[key]);
            using var raw = IndicatorCompute.ComputeDynamicSupportAndResistanceFast(Data(bars), context, length, kind, key == "Support" ? Band.Support : key == "Resistance" ? Band.Resistance : Band.Middle); Assert.Equal(expected[key],raw.Span.ToArray());
        }
        using var state = new DynamicSupportAndResistanceState(kind,length);
        for (var replay = 0; replay < 2; replay++)
        {
            state.Reset();
            for (var i = 0; i < bars.Length; i++)
            {
                state.Update(Native(new Bar(bars[i].Time,0,100,-90,20,1)),false,true);
                foreach (var final in new[] { false,false,true }) { var actual = state.Update(Native(bars[i]),final,true); foreach (var key in expected.Keys) Assert.Equal(expected[key][i], actual.Outputs![key]); }
            }
        }
    }
    [Fact]
    public void SelectedClosesAndCustomerRangesPreserveExtremaAndPeriod()
    {
        var bars = new[] { new Bar(DateTime.UnixEpoch,4,10,-2,4,1),new Bar(DateTime.UnixEpoch.AddMinutes(1),4,5,-2,4,1),new Bar(DateTime.UnixEpoch.AddMinutes(2),4,7,0,4,1) }; var prices = new[] { 9d,-1,6 };
        using var context = new ComputeContext();
        foreach (var batch in new[] { false,true })
        {
            var data = Data(bars); data.SetCustomValues(prices.ToList());
            using var armed = ComponentAverage.Arm(new Func<IReadOnlyList<double>,int,IReadOnlyList<double>>[] { (values,period) => { Assert.Equal(4,period); Assert.Equal(new[] { 12d,11,8 },values); return new[] { 2d,2,2 }; } });
            if (batch) Assert.Equal(new[] { 6d,6,6 },data.CalculateDynamicSupportAndResistance(length:4).OutputValues["Support"]);
            else { using var raw = IndicatorCompute.ComputeDynamicSupportAndResistanceFast(data,context,4,band:Band.Support); Assert.Equal(new[] { 6d,6,6 },raw.Span.ToArray()); }
            Assert.Equal(1,ComponentAverage.Substitutions);
        }
        var expected = BuiltInFormulaReferences.DynamicSupportOutputs(bars.Select((b,i)=>new Bar(b.Time,b.Open,b.High,b.Low,prices[i],b.Volume)).ToArray(),2,6);
        var selected=Data(bars); selected.SetCustomValues(prices.ToList()); using var result = IndicatorCompute.ComputeDynamicSupportAndResistanceFast(selected,context,2,band:Band.Support); Assert.Equal(expected["Support"],result.Span.ToArray());
    }
    [Fact]
    public void InvalidInputsNeverAdvanceNativeState()
    {
        foreach (var field in Enumerable.Range(0,5)) foreach (var invalid in new[] { double.NaN,double.PositiveInfinity,double.NegativeInfinity }) foreach (var final in new[] { false,true })
        {
            using var state = new DynamicSupportAndResistanceState(length:2); using var control = new DynamicSupportAndResistanceState(length:2);
            var first=Native(new Bar(DateTime.UnixEpoch,2,3,1,2,1)); state.Update(first,true,true); control.Update(first,true,true);
            var values=new[] { 2d,4,1,2,1 }; values[field]=invalid; var bad=new OhlcvBar("DSR",BarTimeframe.Minutes(1),DateTime.UnixEpoch,DateTime.UnixEpoch,values[0],values[1],values[2],values[3],values[4],true);
            Assert.Throws<ArgumentOutOfRangeException>(()=>state.Update(bad,final,true));
            var next=Native(new Bar(DateTime.UnixEpoch.AddMinutes(1),4,5,3,4,1)); Assert.Equal(control.Update(next,true,true).Outputs!,state.Update(next,true,true).Outputs!);
        }
    }
    [Fact]
    public void LegacyBatchOnlyAveragesRetainTheirRangeSmoother()
    {
        var bars=Enumerable.Range(0,12).Select(i=>new Bar(DateTime.UnixEpoch.AddMinutes(i),10,12,8,9+i%3,1)).ToArray();
        var ranges=Enumerable.Repeat(4d,bars.Length).ToList(); var atr=CalculationsHelper.GetMovingAverageList(Data(bars),MovingAvgType.HullMovingAverage,4,ranges);
        var expected=atr.Select(v=>12-2*v).ToArray(); var batch=Data(bars).CalculateDynamicSupportAndResistance(MovingAvgType.HullMovingAverage,4);
        using var context=new ComputeContext(); using var raw=IndicatorCompute.ComputeDynamicSupportAndResistanceFast(Data(bars),context,4,MovingAvgType.HullMovingAverage,Band.Support);
        Assert.Equal(expected,batch.OutputValues["Support"]); Assert.Equal(expected,raw.Span.ToArray());
    }
    public static IEnumerable<object[]> Cases => IndicatorValidationDiscovery.Discover(new[] { typeof(IIndicator).Assembly }).Where(c=>c.IndicatorType==typeof(DynamicSupportAndResistance)).Select(c=>new object[] { c });
    public static IEnumerable<object[]> Routes => Cases.SelectMany(c=>new[] { "batch","fast","arm","native","streaming" }.Select(route=>new object[] { c[0],route }));
    [Theory,MemberData(nameof(Routes))]
    public void AllRoutesMatchIndependentFormulasIncludingPreviewAndReset(IndicatorValidationCase testCase,string route)=>new OrdinalFamilyNumericalTests().CheckRoutes(testCase,route,bars=>BuiltInFormulaReferences.DynamicSupportOutputs(bars,(IBuiltInIndicator)testCase.Factory()),IndicatorErrorBudget.Exact);
    [Theory,MemberData(nameof(Cases))]
    public Task SelectedSourcePreservesTheFormulaAndOriginalCandleFields(IndicatorValidationCase testCase)=>new OrdinalFamilyNumericalTests().SelectedSourcePreservesTheFormulaAndOriginalCandleFields(testCase);
    [Theory,MemberData(nameof(Cases))]
    public void EveryPublishedOutputRejectsAnInjectedValueFault(IndicatorValidationCase testCase)=>new OrdinalFamilyNumericalTests().EveryPublishedOutputRejectsAnInjectedValueFault(testCase);
    [Theory,MemberData(nameof(Cases))]
    public Task PublicConfigurationsPassEveryNumericalClass(IndicatorValidationCase testCase)=>new OrdinalFamilyNumericalTests().PublicConfigurationsPassEveryNumericalClass(testCase);
}

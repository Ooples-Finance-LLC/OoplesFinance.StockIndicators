using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Builder.Compute;
using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Streaming;
using OoplesFinance.StockIndicators.Validation;
namespace OoplesFinance.StockIndicators.Tests.Unit.ValidationTests;
public sealed class RexNumericalTests
{
    private static StockData Data(Bar[] bars) => new(bars.Select(b => b.Open), bars.Select(b => b.High), bars.Select(b => b.Low), bars.Select(b => b.Close), bars.Select(b => b.Volume), bars.Select(b => b.Time));
    private static OhlcvBar Native(Bar b) => new("REX", BarTimeframe.Minutes(1), b.Time, b.Time, b.Open, b.High, b.Low, b.Close, b.Volume, true);
    private static int Kind(MovingAvgType kind) => kind == MovingAvgType.SimpleMovingAverage ? 1 : kind == MovingAvgType.WeightedMovingAverage ? 2 : kind == MovingAvgType.ExponentialMovingAverage ? 3 : 6;
    [Fact]
    public void WeightedCandleAndBothAveragesMatchIndependentRoundedStages()
    {
        foreach (var length in new[] { 0, 1, 2, 7, 14 })
        foreach (var kind in new[] { MovingAvgType.SimpleMovingAverage, MovingAvgType.WeightedMovingAverage, MovingAvgType.ExponentialMovingAverage, MovingAvgType.WildersSmoothingMethod })
        foreach (var scale in new[] { 1d, double.Epsilon, double.MaxValue / 8 })
            Check(Enumerable.Range(0, 24).Select(i => new Bar(DateTime.UnixEpoch.AddMinutes(i), (i % 3 - 1) * scale, (i % 5 + 2) * scale, -(i % 3 + 2) * scale, (i % 5 - 2) * scale, 1)).ToArray(), length, kind);
        Check(Array.Empty<Bar>(), 14, MovingAvgType.ExponentialMovingAverage);
        var flat = Enumerable.Range(0, 8).Select(i => new Bar(DateTime.UnixEpoch.AddMinutes(i), double.MaxValue, double.MaxValue, double.MaxValue, double.MaxValue, 1)).ToArray();
        Check(flat, 2, MovingAvgType.SimpleMovingAverage);
        Assert.All(BuiltInFormulaReferences.RexOutputs(flat, 2, 1)["Ro"], v => Assert.Equal(0d, v));
        var hand = new[] { 1d, 2, 3, 4 }.Select((c, i) => new Bar(DateTime.UnixEpoch.AddMinutes(i), 0, 4, -2, c, 1)).ToArray();
        Check(hand, 2, MovingAvgType.SimpleMovingAverage);
        var expected = BuiltInFormulaReferences.RexOutputs(hand, 2, 1);
        Assert.Equal(new[] { 0d, 2.5, 5.5, 8.5 }, expected["Ro"]);
        Assert.Equal(new[] { 0d, 1.25, 4, 7 }, expected["Signal"]);
    }
    private static void Check(Bar[] bars, int length, MovingAvgType kind)
    {
        var expected = BuiltInFormulaReferences.RexOutputs(bars, length, Kind(kind));
        var batch = Data(bars).CalculateRexOscillator(kind, length);
        foreach (var key in expected.Keys) Assert.Equal(expected[key], batch.ChainedOutputs[key]);
        using var context = new ComputeContext();
        using var raw = IndicatorCompute.ComputeRexOscillatorFast(Data(bars), context, length, kind);
        using var signal = IndicatorCompute.ComputeRexOscillatorFast(Data(bars), context, length, kind, true);
        Assert.Equal(expected["Ro"], raw.Span.ToArray()); Assert.Equal(expected["Signal"], signal.Span.ToArray());
        if (kind == MovingAvgType.ExponentialMovingAverage)
        {
            var core = new double[bars.Length];
            OoplesFinance.StockIndicators.Core.OscillatorCore.RexOscillator(bars.Select(b=>b.Open).ToArray(), bars.Select(b=>b.High).ToArray(), bars.Select(b=>b.Low).ToArray(), bars.Select(b=>b.Close).ToArray(), core, length);
            Assert.Equal(expected["Ro"], core);
        }
        using var state = new RexOscillatorState(kind, length);
        for (var replay = 0; replay < 2; replay++)
        {
            state.Reset();
            for (var i = 0; i < bars.Length; i++)
            {
                state.Update(Native(new Bar(bars[i].Time, 3, 4, -2, -1, 7)), false, true);
                foreach (var final in new[] { false, false, true })
                {
                    var result = state.Update(Native(bars[i]), final, true);
                    Assert.Equal(expected["Ro"][i], result.Value);
                    foreach (var key in expected.Keys) Assert.Equal(expected[key][i], result.Outputs![key]);
                }
            }
        }
    }
    [Fact]
    public void ExtendedCandleValuesRecoverBeforeEitherAverageIsPublished()
    {
        var bars = new[] { double.MaxValue, -double.MaxValue, double.MaxValue, -double.MaxValue }.Select((c,i) => new Bar(DateTime.UnixEpoch.AddMinutes(i), 0, double.MaxValue, -double.MaxValue, c, 1)).ToArray();
        Check(bars, 2, MovingAvgType.SimpleMovingAverage);
        var expected = BuiltInFormulaReferences.RexOutputs(bars, 2, 1);
        Assert.All(expected["Ro"], v => Assert.Equal(0d, v));
        Assert.All(expected["Signal"], v => Assert.Equal(0d, v));
        var cancelling = new[] {
            new Bar(DateTime.UnixEpoch,-double.MaxValue,double.MaxValue,-double.MaxValue,double.MaxValue,1),
            new Bar(DateTime.UnixEpoch.AddMinutes(1),0,0,0,0,1),
            new Bar(DateTime.UnixEpoch.AddMinutes(2),double.MaxValue,double.MaxValue,-double.MaxValue,-double.MaxValue,1) };
        Check(cancelling,2,MovingAvgType.SimpleMovingAverage);
        var recovered=BuiltInFormulaReferences.RexOutputs(cancelling,2,1);
        Assert.Equal(double.PositiveInfinity,recovered["Ro"][1]);
        Assert.Equal(double.NegativeInfinity,recovered["Ro"][2]);
        Assert.Equal(0d,recovered["Signal"][2]);
    }
    [Fact]
    public void SelectedCloseAndTwoCustomerStagesPreserveOriginalCandleFields()
    {
        var bars = Enumerable.Range(0,4).Select(i => new Bar(DateTime.UnixEpoch.AddMinutes(i), 1, 4, -2, 0, 1)).ToArray();
        var selected = new[] { 99d, -99, 3, 4 };
        var selectedBars = bars.Select((b,i) => new Bar(b.Time,b.Open,b.High,b.Low,selected[i],b.Volume)).ToArray();
        var supplied = new[] { new[] { 2d, 8, 0, -4 }, new[] { 7d, 4, 3, 2 } };
        using var context = new ComputeContext();
        foreach (var route in new[] { "batch", "raw", "signal" }) foreach (var external in new[] { false, true })
        {
            var expected = BuiltInFormulaReferences.RexOutputs(selectedBars,2,1,external ? supplied : null);
            var data = Data(bars); data.SetCustomValues(selected.ToList());
            using var armed = external ? ComponentAverage.Arm(new Func<IReadOnlyList<double>,int,IReadOnlyList<double>>[] {
                (v,p) => { Assert.Equal(2,p); Assert.Equal(new[] {294d,-300,6,9},v); return supplied[0]; },
                (v,p) => { Assert.Equal(2,p); Assert.Equal(supplied[0],v); return supplied[1]; } }) : null;
            if (route == "batch") { var output=data.CalculateRexOscillator(MovingAvgType.SimpleMovingAverage,2).ChainedOutputs; foreach(var key in expected.Keys) Assert.Equal(expected[key],output[key]); }
            else { using var result=IndicatorCompute.ComputeRexOscillatorFast(data,context,2,MovingAvgType.SimpleMovingAverage,route=="signal"); Assert.Equal(expected[route=="raw"?"Ro":"Signal"],result.Span.ToArray()); }
            if(external) Assert.Equal(route=="raw"?1:2,ComponentAverage.Substitutions);
        }
    }
    [Fact]
    public void InvalidFieldsCannotAdvanceEitherSmoothingStage()
    {
        foreach(var field in Enumerable.Range(0,5)) foreach(var invalid in new[] {double.NaN,double.PositiveInfinity,double.NegativeInfinity}) foreach(var final in new[] {false,true})
        {
            using var state=new RexOscillatorState(length:2); using var control=new RexOscillatorState(length:2);
            var first=Native(new Bar(DateTime.UnixEpoch,0,3,-2,1,1));state.Update(first,true,true);control.Update(first,true,true);
            var v=new[] {0d,4,-2,1,1};v[field]=invalid;
            var bad=new OhlcvBar("REX",BarTimeframe.Minutes(1),DateTime.UnixEpoch,DateTime.UnixEpoch,v[0],v[1],v[2],v[3],v[4],true);
            Assert.Throws<ArgumentOutOfRangeException>(()=>state.Update(bad,final,true));
            foreach(var close in new[] {4d,7,3,9,2,5}) {var next=Native(new Bar(DateTime.UnixEpoch.AddMinutes(1),close-2,close+2,close-1,close,1)); Assert.Equal(control.Update(next,true,true).Outputs!,state.Update(next,true,true).Outputs!);}
        }
    }
    public static IEnumerable<object[]> Cases=>IndicatorValidationDiscovery.Discover(new[] {typeof(IIndicator).Assembly}).Where(c=>c.IndicatorType==typeof(RexOscillator)).Select(c=>new object[] {c});
    public static IEnumerable<object[]> Routes=>Cases.SelectMany(c=>new[] {"batch","fast","arm","native","streaming"}.Select(route=>new object[] {c[0],route}));
    [Theory,MemberData(nameof(Routes))]
    public void AllRoutesMatchIndependentFormulasIncludingPreviewAndReset(IndicatorValidationCase testCase,string route)=>new OrdinalFamilyNumericalTests().CheckRoutes(testCase,route,bars=>BuiltInFormulaReferences.RexOutputs(bars,(IBuiltInIndicator)testCase.Factory()),IndicatorErrorBudget.Exact);
    [Theory,MemberData(nameof(Cases))]
    public Task SelectedSourcePreservesTheFormulaAndOriginalCandleFields(IndicatorValidationCase testCase)=>new OrdinalFamilyNumericalTests().SelectedSourcePreservesTheFormulaAndOriginalCandleFields(testCase);
    [Theory,MemberData(nameof(Cases))]
    public void EveryPublishedOutputRejectsAnInjectedValueFault(IndicatorValidationCase testCase)=>new OrdinalFamilyNumericalTests().EveryPublishedOutputRejectsAnInjectedValueFault(testCase);
    [Theory,MemberData(nameof(Cases))]
    public Task PublicConfigurationsPassEveryNumericalClass(IndicatorValidationCase testCase)=>new OrdinalFamilyNumericalTests().PublicConfigurationsPassEveryNumericalClass(testCase);
}

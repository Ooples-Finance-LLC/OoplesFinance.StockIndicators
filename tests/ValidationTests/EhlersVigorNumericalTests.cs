using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Builder.Compute;
using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Streaming;
using OoplesFinance.StockIndicators.Validation;
namespace OoplesFinance.StockIndicators.Tests.Unit.ValidationTests;
public sealed class EhlersVigorNumericalTests
{
    private static StockData Data(Bar[] bars) => new(bars.Select(b => b.Open), bars.Select(b => b.High), bars.Select(b => b.Low), bars.Select(b => b.Close), bars.Select(b => b.Volume), bars.Select(b => b.Time));
    private static OhlcvBar Native(Bar b) => new("EVIGOR", BarTimeframe.Minutes(1), b.Time, b.Time, b.Open, b.High, b.Low, b.Close, b.Volume, true);
    private static int Kind(MovingAvgType kind) => kind == MovingAvgType.SimpleMovingAverage ? 1 : kind == MovingAvgType.WeightedMovingAverage ? 2 : kind == MovingAvgType.ExponentialMovingAverage ? 3 : 6;
    [Fact]
    public void CandleRatiosAndUnequalAveragesMatchIndependentRoundedStages()
    {
        foreach (var length in new[] { 0, 1, 2, 7, 14 })
        foreach (var signalLength in new[] { 0, 1, 3, 8 })
        foreach (var kind in new[] { MovingAvgType.SimpleMovingAverage, MovingAvgType.WeightedMovingAverage, MovingAvgType.ExponentialMovingAverage, MovingAvgType.WildersSmoothingMethod })
        foreach (var scale in new[] { 1d, double.Epsilon, double.MaxValue / 8 })
            Check(Enumerable.Range(0, 24).Select(i => new Bar(DateTime.UnixEpoch.AddMinutes(i), (i % 3 - 1) * scale, (i % 5 + 2) * scale, -(i % 3 + 2) * scale, (i % 5 - 2) * scale, 1)).ToArray(), length, kind, signalLength);
        Check(Array.Empty<Bar>(), 14, MovingAvgType.ExponentialMovingAverage);
        var flat = Enumerable.Range(0, 8).Select(i => new Bar(DateTime.UnixEpoch.AddMinutes(i), double.MaxValue, double.MaxValue, double.MaxValue, double.MaxValue, 1)).ToArray();
        Check(flat, 2, MovingAvgType.SimpleMovingAverage);
        Assert.All(BuiltInFormulaReferences.EhlersVigorOutputs(flat, 2, 1, 3)["Ervi"], v => Assert.Equal(0d, v));
        var hand = new[] { 1d, 2, 3, 4 }.Select((c, i) => new Bar(DateTime.UnixEpoch.AddMinutes(i), 0, 1, 0, c, 1)).ToArray();
        Check(hand, 2, MovingAvgType.SimpleMovingAverage);
        var expected = BuiltInFormulaReferences.EhlersVigorOutputs(hand, 2, 1, 3);
        Assert.Equal(new[] { 0d, 1.5, 2.5, 3.5 }, expected["Ervi"]);
        Assert.Equal(new[] { 0d, 0, 4d/3, 2.5 }, expected["Signal"]);
    }
    private static void Check(Bar[] bars, int length, MovingAvgType kind, int signalLength = 3)
    {
        var expected = BuiltInFormulaReferences.EhlersVigorOutputs(bars, length, Kind(kind), signalLength);
        var batch = Data(bars).CalculateEhlersRelativeVigorIndex(kind, length, signalLength);
        foreach (var key in expected.Keys) Assert.Equal(expected[key], batch.ChainedOutputs[key]);
        using var context = new ComputeContext();
        using var raw = IndicatorCompute.ComputeEhlersRelativeVigorIndexFast(Data(bars), context, length, kind, signalLength);
        using var signal = IndicatorCompute.ComputeEhlersRelativeVigorIndexFast(Data(bars), context, length, kind, signalLength, "Signal");
        Assert.Equal(expected["Ervi"], raw.Span.ToArray()); Assert.Equal(expected["Signal"], signal.Span.ToArray());
        if (length == 1)
        {
            var core = new double[bars.Length];
            OoplesFinance.StockIndicators.Core.OscillatorCore.EhlersRelativeVigorIndex(bars.Select(b=>b.Open).ToArray(), bars.Select(b=>b.High).ToArray(), bars.Select(b=>b.Low).ToArray(), bars.Select(b=>b.Close).ToArray(), core);
            Assert.Equal(expected["Ervi"], core);
        }
        using var state = new EhlersRelativeVigorIndexState(kind, length, signalLength);
        for (var replay = 0; replay < 2; replay++)
        {
            state.Reset();
            for (var i = 0; i < bars.Length; i++)
            {
                state.Update(Native(new Bar(bars[i].Time, 3, 4, -2, -1, 7)), false, true);
                foreach (var final in new[] { false, false, true })
                {
                    var result = state.Update(Native(bars[i]), final, true);
                    Assert.Equal(expected["Ervi"][i], result.Value);
                    foreach (var key in expected.Keys) Assert.Equal(expected[key][i], result.Outputs![key]);
                }
            }
        }
    }
    [Fact]
    public void ExtremeRangesAndRatiosRemainUsableBeforePublication()
    {
        var extreme = Enumerable.Range(0,6).Select(i=>new Bar(DateTime.UnixEpoch.AddMinutes(i),-double.MaxValue,double.MaxValue,-double.MaxValue,double.MaxValue,1)).ToArray();
        Check(extreme,1,MovingAvgType.SimpleMovingAverage,1);
        Assert.All(BuiltInFormulaReferences.EhlersVigorOutputs(extreme,1,1,1)["Ervi"],v=>Assert.Equal(1d,v));
        var cancelling = new[] {double.MaxValue,-double.MaxValue,0d}.Select((c,i)=>new Bar(DateTime.UnixEpoch.AddMinutes(i),0,double.Epsilon,0,c,1)).ToArray();
        Check(cancelling,1,MovingAvgType.SimpleMovingAverage,2);
        var expected=BuiltInFormulaReferences.EhlersVigorOutputs(cancelling,1,1,2);
        Assert.Equal(double.PositiveInfinity,expected["Ervi"][0]);
        Assert.Equal(double.NegativeInfinity,expected["Ervi"][1]);
        Assert.Equal(0d,expected["Signal"][1]);
        Check(cancelling,2,MovingAvgType.SimpleMovingAverage,1);
        Assert.Equal(0d,BuiltInFormulaReferences.EhlersVigorOutputs(cancelling,2,1,1)["Ervi"][1]);
    }
    [Fact]
    public void SelectedCloseAndTwoCustomerStagesPreserveOriginalCandleFields()
    {
        var bars = Enumerable.Range(0,4).Select(i => new Bar(DateTime.UnixEpoch.AddMinutes(i), 1, 3, 1, 0, 1)).ToArray();
        var selected = new[] { 99d, -99, 3, 4 };
        var selectedBars = bars.Select((b,i) => new Bar(b.Time,b.Open,b.High,b.Low,selected[i],b.Volume)).ToArray();
        var supplied = new[] { new[] { 2d, 8, 0, -4 }, new[] { 7d, 4, 3, 2 } };
        using var context = new ComputeContext();
        foreach (var route in new[] { "batch", "raw", "signal" }) foreach (var external in new[] { false, true })
        {
            var expected = BuiltInFormulaReferences.EhlersVigorOutputs(selectedBars,2,1,3,external ? supplied : null);
            var data = Data(bars); data.SetCustomValues(selected.ToList());
            using var armed = external ? ComponentAverage.Arm(new Func<IReadOnlyList<double>,int,IReadOnlyList<double>>[] {
                (v,p) => { Assert.Equal(2,p); Assert.Equal(new[] {49d,-50,1,1.5},v); return supplied[0]; },
                (v,p) => { Assert.Equal(3,p); Assert.Equal(supplied[0],v); return supplied[1]; } }) : null;
            if (route == "batch") { var output=data.CalculateEhlersRelativeVigorIndex(MovingAvgType.SimpleMovingAverage,2,3).ChainedOutputs; foreach(var key in expected.Keys) Assert.Equal(expected[key],output[key]); }
            else { using var result=IndicatorCompute.ComputeEhlersRelativeVigorIndexFast(data,context,2,MovingAvgType.SimpleMovingAverage,3,route=="signal"?"Signal":null); Assert.Equal(expected[route=="raw"?"Ervi":"Signal"],result.Span.ToArray()); }
            if(external) Assert.Equal(route=="raw"?1:2,ComponentAverage.Substitutions);
        }
    }
    [Fact]
    public void InvalidFieldsCannotAdvanceEitherSmoothingStage()
    {
        foreach(var field in Enumerable.Range(0,5)) foreach(var invalid in new[] {double.NaN,double.PositiveInfinity,double.NegativeInfinity}) foreach(var final in new[] {false,true})
        {
            using var state=new EhlersRelativeVigorIndexState(length:2); using var control=new EhlersRelativeVigorIndexState(length:2);
            var first=Native(new Bar(DateTime.UnixEpoch,0,3,-2,1,1));state.Update(first,true,true);control.Update(first,true,true);
            var v=new[] {0d,4,-2,1,1};v[field]=invalid;
            var bad=new OhlcvBar("EVIGOR",BarTimeframe.Minutes(1),DateTime.UnixEpoch,DateTime.UnixEpoch,v[0],v[1],v[2],v[3],v[4],true);
            Assert.Throws<ArgumentOutOfRangeException>(()=>state.Update(bad,final,true));
            foreach(var close in new[] {4d,7,3,9,2,5}) {var next=Native(new Bar(DateTime.UnixEpoch.AddMinutes(1),close-2,close+2,close-1,close,1)); Assert.Equal(control.Update(next,true,true).Outputs!,state.Update(next,true,true).Outputs!);}
        }
    }
    public static IEnumerable<object[]> Cases=>IndicatorValidationDiscovery.Discover(new[] {typeof(IIndicator).Assembly}).Where(c=>c.IndicatorType==typeof(EhlersRelativeVigorIndex)).Select(c=>new object[] {c});
    public static IEnumerable<object[]> Routes=>Cases.SelectMany(c=>new[] {"batch","fast","arm","native","streaming"}.Select(route=>new object[] {c[0],route}));
    [Theory,MemberData(nameof(Routes))]
    public void AllRoutesMatchIndependentFormulasIncludingPreviewAndReset(IndicatorValidationCase testCase,string route)=>new OrdinalFamilyNumericalTests().CheckRoutes(testCase,route,bars=>BuiltInFormulaReferences.EhlersVigorOutputs(bars,(IBuiltInIndicator)testCase.Factory()),IndicatorErrorBudget.Exact);
    [Theory,MemberData(nameof(Cases))]
    public Task SelectedSourcePreservesTheFormulaAndOriginalCandleFields(IndicatorValidationCase testCase)=>new OrdinalFamilyNumericalTests().SelectedSourcePreservesTheFormulaAndOriginalCandleFields(testCase);
    [Theory,MemberData(nameof(Cases))]
    public void EveryPublishedOutputRejectsAnInjectedValueFault(IndicatorValidationCase testCase)=>new OrdinalFamilyNumericalTests().EveryPublishedOutputRejectsAnInjectedValueFault(testCase);
    [Theory,MemberData(nameof(Cases))]
    public Task PublicConfigurationsPassEveryNumericalClass(IndicatorValidationCase testCase)=>new OrdinalFamilyNumericalTests().PublicConfigurationsPassEveryNumericalClass(testCase);
}

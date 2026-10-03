using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Builder.Compute;
using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Streaming;
using OoplesFinance.StockIndicators.Validation;
namespace OoplesFinance.StockIndicators.Tests.Unit.ValidationTests;
public sealed class MassIndexNumericalTests
{
    private static StockData Data(Bar[] bars)=>new(bars.Select(b=>b.Open),bars.Select(b=>b.High),bars.Select(b=>b.Low),bars.Select(b=>b.Close),bars.Select(b=>b.Volume),bars.Select(b=>b.Time));
    private static OhlcvBar Native(Bar b)=>new("MASS",BarTimeframe.Minutes(1),b.Time,b.Time,b.Open,b.High,b.Low,b.Close,b.Volume,true);
    private static Bar Candle(int i,double h,double l,double c,double v)=>new(DateTime.UnixEpoch.AddMinutes(i),c,h,l,c,v);
    private static int Kind(MovingAvgType kind)=>kind==MovingAvgType.SimpleMovingAverage?1:kind==MovingAvgType.WeightedMovingAverage?2:kind==MovingAvgType.ExponentialMovingAverage?3:6;
    [Fact]
    public void DoubleSmoothingRollingRatiosAndSignalMatchIndependentFractions()
    {
        foreach(var periods in new[]{(0,0,0,0),(1,1,1,1),(2,3,4,2),(4,2,7,3),(2,2,4,3),(21,21,25,9)})
        foreach(var kind in new[]{MovingAvgType.SimpleMovingAverage,MovingAvgType.WeightedMovingAverage,MovingAvgType.ExponentialMovingAverage,MovingAvgType.WildersSmoothingMethod})
        {
            foreach(var scale in new[]{1d,double.Epsilon,double.MaxValue/8})Check(Enumerable.Range(0,28).Select(i=>Candle(i,(i%6+1)*scale,-scale,0,1)).ToArray(),periods.Item1,periods.Item2,periods.Item3,periods.Item4,kind);
            Check(new[]{Candle(0,double.MaxValue,-double.MaxValue,0,1),Candle(1,double.MaxValue,-double.MaxValue,0,1),Candle(2,0,0,0,1),Candle(3,0,0,0,1)},periods.Item1,periods.Item2,periods.Item3,periods.Item4,kind);
        }
        Check(Array.Empty<Bar>(),21,21,25,9,MovingAvgType.ExponentialMovingAverage);
        var bars=Enumerable.Range(0,10).Select(i=>Candle(i,i+2,-1,0,1)).ToArray();Check(bars,int.MaxValue,3,int.MaxValue,5,MovingAvgType.ExponentialMovingAverage);Check(bars,2,int.MaxValue,int.MaxValue,int.MaxValue,MovingAvgType.ExponentialMovingAverage);
        var flat=Enumerable.Range(0,5).Select(i=>Candle(i,1,1,1,1)).ToArray();Check(flat,2,2,3,2,MovingAvgType.ExponentialMovingAverage);Assert.All(BuiltInFormulaReferences.MassIndexOutputs(flat,2,2,3,2,3)["Mi"],v=>Assert.Equal(0d,v));
        var hand=Enumerable.Range(0,4).Select(i=>Candle(i,2,0,1,1)).ToArray();Check(hand,1,1,3,2,MovingAvgType.ExponentialMovingAverage);Assert.Equal(new[]{1d,2,3,3},BuiltInFormulaReferences.MassIndexOutputs(hand,1,1,3,2,3)["Mi"]);
    }
    private static void Check(Bar[] bars,int first,int second,int length,int signal,MovingAvgType kind)
    {
        var expected=BuiltInFormulaReferences.MassIndexOutputs(bars,first,second,length,signal,Kind(kind));var batch=Data(bars).CalculateMassIndex(kind,first,second,length,signal);
        using var context=new ComputeContext();foreach(var key in expected.Keys){Assert.Equal(expected[key],batch.OutputValues[key]);using var raw=IndicatorCompute.ComputeMassIndexFast(Data(bars),context,first,second,length,kind,key,signal);Assert.Equal(expected[key],raw.Span.ToArray());}
        if(kind==MovingAvgType.ExponentialMovingAverage && first==second)
        {
            var high=bars.Select(b=>b.High).ToArray();var low=bars.Select(b=>b.Low).ToArray();var core=new double[bars.Length];OoplesFinance.StockIndicators.Core.OscillatorCore.MassIndex(high,low,core,first,length);Assert.Equal(expected["Mi"],core);OoplesFinance.StockIndicators.Core.VolatilityCore.MassIndex(high,low,core,length,first);Assert.Equal(expected["Mi"],core);
        }
        using var state=new MassIndexState(kind,first,second,length,signal);
        for(var replay=0;replay<2;replay++){state.Reset();for(var i=0;i<bars.Length;i++){state.Update(Native(Candle(i,40,-30,20,7)),false,true);foreach(var final in new[]{false,false,true}){var result=state.Update(Native(bars[i]),final,true);Assert.Equal(expected["Mi"][i],result.Value);foreach(var key in expected.Keys)Assert.Equal(expected[key][i],result.Outputs![key]);}}}
    }
    [Fact]
    public void SelectedPricesKeepOriginalRangesAndThreeCustomerStagesStayOrdered()
    {
        var bars=Enumerable.Range(0,8).Select(i=>Candle(i,i+2,-2,1,1)).ToArray();var selected=new[]{100d,99,1,4,2,0,-9,1};var supplied=new[]{new[]{1d,3,5,7,2,0,9,1},new[]{2d,4,1,2,0,1,3,1},new[]{9d,2,8,1,7,3,6,4}};
        foreach(var batch in new[]{false,true})foreach(var external in new[]{false,true})foreach(var key in new[]{"Mi","Signal"})
        {
            var expected=BuiltInFormulaReferences.MassIndexOutputs(bars,2,3,4,5,3,external?supplied:null);var data=Data(bars);data.SetCustomValues(selected.ToList());
            using var armed=external?ComponentAverage.Arm(new Func<IReadOnlyList<double>,int,IReadOnlyList<double>>[]{(v,p)=>{Assert.Equal(2,p);Assert.Equal(bars.Select(b=>b.High-b.Low),v);return supplied[0];},(v,p)=>{Assert.Equal(3,p);Assert.Equal(supplied[0],v);return supplied[1];},(v,p)=>{Assert.Equal(5,p);Assert.Equal(expected["Mi"],v);return supplied[2];}}):null;
            if(batch)Assert.Equal(expected[key],data.CalculateMassIndex(MovingAvgType.ExponentialMovingAverage,2,3,4,5).OutputValues[key]);
            else{using var context=new ComputeContext();using var raw=IndicatorCompute.ComputeMassIndexFast(data,context,2,3,4,MovingAvgType.ExponentialMovingAverage,key,5);Assert.Equal(expected[key],raw.Span.ToArray());}
            if(external)Assert.Equal(3,ComponentAverage.Substitutions);
        }
    }
    [Fact]
    public void ExtendedCustomerRatiosCanCancelInTheRollingSum()
    {
        var bars=Enumerable.Range(0,3).Select(i=>Candle(i,1,0,0,1)).ToArray();var supplied=new[]{new[]{double.MaxValue,-double.MaxValue,0},new[]{double.Epsilon,double.Epsilon,1},new[]{0d,0,0}};
        foreach(var batch in new[]{false,true})
        {
            var expected=BuiltInFormulaReferences.MassIndexOutputs(bars,1,1,2,1,3,supplied)["Mi"];Assert.Equal(0d,expected[1]);
            using var armed=ComponentAverage.Arm(supplied.Select<double[],Func<IReadOnlyList<double>,int,IReadOnlyList<double>>>(a=>(v,p)=>a).ToArray());
            if(batch)Assert.Equal(expected,Data(bars).CalculateMassIndex(MovingAvgType.ExponentialMovingAverage,1,1,2,1).ChainedValues);
            else{using var context=new ComputeContext();using var raw=IndicatorCompute.ComputeMassIndexFast(Data(bars),context,1,1,2,MovingAvgType.ExponentialMovingAverage,signalLength:1);Assert.Equal(expected,raw.Span.ToArray());}
            Assert.Equal(3,ComponentAverage.Substitutions);
        }
    }
    [Fact]
    public void InvalidFieldsCannotAdvanceAnySmoothingStage()
    {
        foreach(var field in Enumerable.Range(0,5))foreach(var invalid in new[] {double.NaN,double.PositiveInfinity,double.NegativeInfinity})foreach(var final in new[] {false,true})
        {
            using var state=new MassIndexState(length1:2,length2:3,length3:4,signalLength:2);using var control=new MassIndexState(length1:2,length2:3,length3:4,signalLength:2);var first=Native(Candle(0,3,0,1,2));state.Update(first,true,true);control.Update(first,true,true);var v=new[] {0d,4,-2,1,1};v[field]=invalid;var bad=new OhlcvBar("MASS",BarTimeframe.Minutes(1),DateTime.UnixEpoch,DateTime.UnixEpoch,v[0],v[1],v[2],v[3],v[4],true);Assert.Throws<ArgumentOutOfRangeException>(()=>state.Update(bad,final,true));foreach(var i in Enumerable.Range(1,6)){var bar=Native(Candle(i,i+1,-2,i%3,i+1));Assert.Equal(control.Update(bar,true,true).Outputs!,state.Update(bar,true,true).Outputs!);}
        }
    }
    public static IEnumerable<object[]> Cases=>IndicatorValidationDiscovery.Discover(new[] {typeof(IIndicator).Assembly}).Where(c=>c.IndicatorType==typeof(MassIndex)||c.IndicatorType==typeof(MassIndexCore)).Select(c=>new object[] {c});
    public static IEnumerable<object[]> Routes=>Cases.SelectMany(c=>new[] {"batch","fast","arm","native","streaming"}.Select(route=>new object[] {c[0],route}));
    [Theory,MemberData(nameof(Routes))]
    public void AllRoutesMatchIndependentFormulasIncludingPreviewAndReset(IndicatorValidationCase testCase,string route)=>new OrdinalFamilyNumericalTests().CheckRoutes(testCase,route,bars=>BuiltInFormulaReferences.MassIndexOutputs(bars,(IBuiltInIndicator)testCase.Factory()),IndicatorErrorBudget.Exact);
    [Theory,MemberData(nameof(Cases))]
    public Task SelectedSourcePreservesTheFormulaAndOriginalCandleFields(IndicatorValidationCase testCase)=>new OrdinalFamilyNumericalTests().SelectedSourcePreservesTheFormulaAndOriginalCandleFields(testCase);
    [Theory,MemberData(nameof(Cases))]
    public void EveryPublishedOutputRejectsAnInjectedValueFault(IndicatorValidationCase testCase)=>new OrdinalFamilyNumericalTests().EveryPublishedOutputRejectsAnInjectedValueFault(testCase);
    [Theory,MemberData(nameof(Cases))]
    public Task PublicConfigurationsPassEveryNumericalClass(IndicatorValidationCase testCase)=>new OrdinalFamilyNumericalTests().PublicConfigurationsPassEveryNumericalClass(testCase);
}

using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Builder.Compute;
using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Streaming;
using OoplesFinance.StockIndicators.Validation;
namespace OoplesFinance.StockIndicators.Tests.Unit.ValidationTests;
public sealed class PseudoPolynomialNumericalTests
{
    private static StockData Data(Bar[] bars)=>new(bars.Select(b=>b.Open),bars.Select(b=>b.High),bars.Select(b=>b.Low),bars.Select(b=>b.Close),bars.Select(b=>b.Volume),bars.Select(b=>b.Time));
    private static Bar[] Bars(double[] prices)=>prices.Select((v,i)=>new Bar(DateTime.UnixEpoch.AddMinutes(i),v,v,v,v,1)).ToArray();
    private static OhlcvBar Native(Bar b)=>new("PPC",BarTimeframe.Minutes(1),b.Time,b.Time,b.Open,b.High,b.Low,b.Close,b.Volume,true);
    [Fact]
    public void TwoLagMorphingAndCumulativeErrorPreserveRoundedBandMidpoints()
    {
        foreach(var length in new[] {0,1,2,3,14})foreach(var kind in new[] {MovingAvgType.SimpleMovingAverage,MovingAvgType.WeightedMovingAverage,MovingAvgType.ExponentialMovingAverage,MovingAvgType.WildersSmoothingMethod})foreach(var morph in new[] {-1d,0,.5,.9,2})foreach(var scale in new[] {1d,double.Epsilon,double.MaxValue/8})
            Check(Bars(Enumerable.Range(0,12).Select(i=>(i%11-5)*scale).ToArray()),length,kind,morph);
        Check(Array.Empty<Bar>(),14,MovingAvgType.SimpleMovingAverage,.9);
        var hand=Bars(new[] {2d,4,6,8,10});Check(hand,1,MovingAvgType.SimpleMovingAverage,.5);var reference=BuiltInFormulaReferences.PseudoPolynomialOutputs(hand,1,1,.5);Assert.Equal(new[] {0d,0,3,7,10.5},reference["Raw"]);var batch=Data(hand).CalculatePseudoPolynomialChannel(length:1,morph:.5);Assert.Equal(7.5,batch.OutputValues["UpperBand"][2]);Assert.Equal(-1.5,batch.OutputValues["LowerBand"][2]);
        Check(Bars(new[] {double.MaxValue,-double.MaxValue,double.MaxValue,-double.MaxValue,0d}),1,MovingAvgType.SimpleMovingAverage,.5);
        Check(Bars(new[] {1d,2,3,4}),1,MovingAvgType.SimpleMovingAverage,double.MaxValue);
    }
    private static void Check(Bar[] bars,int length,MovingAvgType kind,double morph)
    {
        var code=kind==MovingAvgType.SimpleMovingAverage?1:kind==MovingAvgType.WeightedMovingAverage?2:kind==MovingAvgType.ExponentialMovingAverage?3:6;
        var expected=BuiltInFormulaReferences.PseudoPolynomialOutputs(bars,length,code,morph);var batch=Data(bars).CalculatePseudoPolynomialChannel(kind,length,morph);using var context=new ComputeContext();
        foreach(var key in new[] {"UpperBand","MiddleBand","LowerBand"}) {Assert.Equal(expected[key],batch.OutputValues[key]);using var raw=IndicatorCompute.ComputePseudoPolynomialChannelFast(Data(bars),context,length,morph,kind,key);Assert.Equal(expected[key],raw.Span.ToArray());}
        using var state=new PseudoPolynomialChannelState(kind,length,morph);for(var replay=0;replay<2;replay++) {state.Reset();for(var i=0;i<bars.Length;i++) {state.Update(Native(new Bar(bars[i].Time,3,4,-2,-1,7)),false,true);foreach(var final in new[] {false,false,true}) {var actual=state.Update(Native(bars[i]),final,true);foreach(var key in new[] {"UpperBand","MiddleBand","LowerBand"})Assert.Equal(expected[key][i],actual.Outputs![key]);}}}
    }
    [Fact]
    public void SelectedPricesAndCustomerSmoothingFeedThePublishedMidpoint()
    {
        var prices=new[] {2d,4,6,8,10};using var context=new ComputeContext();foreach(var external in new[] {false,true})foreach(var batch in new[] {false,true})
        {
            var data=Data(Bars(new[] {4d,4,4,4,4}));data.SetCustomValues(prices.ToList());var means=new[] {1d,2,3,4,5};var expected=BuiltInFormulaReferences.PseudoPolynomialOutputs(Bars(prices),1,1,.5,external?means:null);
            using var armed=external?ComponentAverage.Arm(new Func<IReadOnlyList<double>,int,IReadOnlyList<double>>[] {(values,period)=>{Assert.Equal(1,period);Assert.Equal(new[] {0d,0,3,7,10.5},values);return means;}}):null;
            if(batch) {var output=data.CalculatePseudoPolynomialChannel(length:1,morph:.5);foreach(var key in new[] {"UpperBand","MiddleBand","LowerBand"})Assert.Equal(expected[key],output.OutputValues[key]);}else {using var raw=IndicatorCompute.ComputePseudoPolynomialChannelFast(data,context,1,.5,outputKey:"MiddleBand");Assert.Equal(expected["MiddleBand"],raw.Span.ToArray());}if(external)Assert.Equal(1,ComponentAverage.Substitutions);
        }
        foreach(var batch in new[] {false,true})
        {
            using var armed=ComponentAverage.Arm(new Func<IReadOnlyList<double>,int,IReadOnlyList<double>>[] {(_,_)=>new[] {1d,1}});var data=Data(Bars(new[] {1e16,1e16}));if(batch)Assert.Equal(new[] {1d,0},data.CalculatePseudoPolynomialChannel(length:1).OutputValues["MiddleBand"]);else {using var raw=IndicatorCompute.ComputePseudoPolynomialChannelFast(data,context,1,outputKey:"MiddleBand");Assert.Equal(new[] {1d,0},raw.Span.ToArray());}
        }
    }
    [Fact]
    public void InvalidFieldsAndMorphNeverAdvanceNativeHistory()
    {
        foreach(var field in Enumerable.Range(0,5))foreach(var invalid in new[] {double.NaN,double.PositiveInfinity,double.NegativeInfinity})foreach(var final in new[] {false,true})
        {
            using var state=new PseudoPolynomialChannelState(length:2);using var control=new PseudoPolynomialChannelState(length:2);var first=Native(new Bar(DateTime.UnixEpoch,2,3,1,2,1));state.Update(first,true,true);control.Update(first,true,true);
            var values=new[] {2d,4,1,2,1};values[field]=invalid;var bad=new OhlcvBar("PPC",BarTimeframe.Minutes(1),DateTime.UnixEpoch,DateTime.UnixEpoch,values[0],values[1],values[2],values[3],values[4],true);Assert.Throws<ArgumentOutOfRangeException>(()=>state.Update(bad,final,true));var next=Native(new Bar(DateTime.UnixEpoch.AddMinutes(1),4,5,3,4,1));Assert.Equal(control.Update(next,true,true).Outputs!,state.Update(next,true,true).Outputs!);
        }
        foreach(var invalid in new[] {double.NaN,double.PositiveInfinity,double.NegativeInfinity}) {Assert.Throws<ArgumentOutOfRangeException>(()=>new PseudoPolynomialChannelState(morph:invalid));Assert.Throws<ArgumentOutOfRangeException>(()=>Data(Array.Empty<Bar>()).CalculatePseudoPolynomialChannel(morph:invalid));using var context=new ComputeContext();Assert.Throws<ArgumentOutOfRangeException>(()=>IndicatorCompute.ComputePseudoPolynomialChannelFast(Data(Array.Empty<Bar>()),context,morph:invalid));}
    }
    [Fact]
    public void LegacyBatchAverageSmoothsTheSameMorphedSequence()
    {
        var bars=Bars(new[] {2d,7,4,9,3,5,8,10});var kind=MovingAvgType.HullMovingAverage;var rawValues=BuiltInFormulaReferences.PseudoPolynomialOutputs(bars,2,1,.5)["Raw"];var mean=CalculationsHelper.GetMovingAverageList(Data(bars),kind,2,rawValues.ToList()).ToArray();var expected=BuiltInFormulaReferences.PseudoPolynomialOutputs(bars,2,1,.5,mean);var batch=Data(bars).CalculatePseudoPolynomialChannel(kind,2,.5);Assert.Equal(expected["UpperBand"],batch.OutputValues["UpperBand"]);using var context=new ComputeContext();using var raw=IndicatorCompute.ComputePseudoPolynomialChannelFast(Data(bars),context,2,.5,kind,"LowerBand");Assert.Equal(expected["LowerBand"],raw.Span.ToArray());
    }
    public static IEnumerable<object[]> Cases=>IndicatorValidationDiscovery.Discover(new[] {typeof(IIndicator).Assembly}).Where(c=>c.IndicatorType==typeof(PseudoPolynomialChannel)).Select(c=>new object[] {c});
    public static IEnumerable<object[]> Routes=>Cases.SelectMany(c=>new[] {"batch","fast","arm","native","streaming"}.Select(route=>new object[] {c[0],route}));
    [Theory,MemberData(nameof(Routes))]
    public void AllRoutesMatchIndependentFormulasIncludingPreviewAndReset(IndicatorValidationCase testCase,string route)=>new OrdinalFamilyNumericalTests().CheckRoutes(testCase,route,bars=>BuiltInFormulaReferences.PseudoPolynomialOutputs(bars,(IBuiltInIndicator)testCase.Factory()),IndicatorErrorBudget.Exact);
    [Theory,MemberData(nameof(Cases))]
    public Task SelectedSourcePreservesTheFormulaAndOriginalCandleFields(IndicatorValidationCase testCase)=>new OrdinalFamilyNumericalTests().SelectedSourcePreservesTheFormulaAndOriginalCandleFields(testCase);
    [Theory,MemberData(nameof(Cases))]
    public void EveryPublishedOutputRejectsAnInjectedValueFault(IndicatorValidationCase testCase)=>new OrdinalFamilyNumericalTests().EveryPublishedOutputRejectsAnInjectedValueFault(testCase);
    [Theory,MemberData(nameof(Cases))]
    public Task PublicConfigurationsPassEveryNumericalClass(IndicatorValidationCase testCase)=>new OrdinalFamilyNumericalTests().PublicConfigurationsPassEveryNumericalClass(testCase);
}

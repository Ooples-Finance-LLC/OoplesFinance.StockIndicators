using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Builder.Compute;
using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Streaming;
using OoplesFinance.StockIndicators.Validation;
namespace OoplesFinance.StockIndicators.Tests.Unit.ValidationTests;
public sealed class KirshenbaumNumericalTests
{
    private static StockData Data(Bar[] bars)=>new(bars.Select(b=>b.Open),bars.Select(b=>b.High),bars.Select(b=>b.Low),bars.Select(b=>b.Close),bars.Select(b=>b.Volume),bars.Select(b=>b.Time));
    private static Bar[] Bars(double[] prices)=>prices.Select((v,i)=>new Bar(DateTime.UnixEpoch.AddMinutes(i),v,v,v,v,1)).ToArray();
    private static OhlcvBar Native(Bar b)=>new("KB",BarTimeframe.Minutes(1),b.Time,b.Time,b.Open,b.High,b.Low,b.Close,b.Volume,true);
    [Fact]
    public void RoundedMovingFitResidualsPreserveSubnormalAndExtendedEndpoints()
    {
        foreach(var lengths in new[] {(0,1),(1,3),(3,2),(3,5),(14,9)})foreach(var kind in new[] {MovingAvgType.SimpleMovingAverage,MovingAvgType.WeightedMovingAverage,MovingAvgType.ExponentialMovingAverage,MovingAvgType.WildersSmoothingMethod})foreach(var factor in new[] {-1d,0,.5,1,double.MaxValue})foreach(var scale in new[] {1d,double.Epsilon,double.MaxValue/8})
            Check(Bars(Enumerable.Range(0,12).Select(i=>(i%11-5)*scale).ToArray()),lengths.Item1,lengths.Item2,kind,factor);
        Check(Array.Empty<Bar>(),30,20,MovingAvgType.ExponentialMovingAverage,1);
        var impulse=Bars(new[] {0d,0,9,0,0,0,0,0,0,0,0,0});Check(impulse,1,3,MovingAvgType.SimpleMovingAverage,1);var expired=Data(impulse).CalculateKirshenbaumBands(MovingAvgType.SimpleMovingAverage,1,3);Assert.True(expired.OutputValues["UpperBand"][2]>9);Assert.Equal(0d,expired.OutputValues["UpperBand"][7]);
        var line=Bars(new[] {2d,4,6,8});Check(line,1,3,MovingAvgType.SimpleMovingAverage,1);var hand=Data(line).CalculateKirshenbaumBands(MovingAvgType.SimpleMovingAverage,1,3);Assert.Equal(new[] {2d,4,6,8},hand.OutputValues["UpperBand"]);Assert.Equal(hand.OutputValues["UpperBand"],hand.OutputValues["LowerBand"]);
        var tiny=Bars(new[] {0d,0,4*double.Epsilon});Check(tiny,1,3,MovingAvgType.SimpleMovingAverage,1);var small=Data(tiny).CalculateKirshenbaumBands(MovingAvgType.SimpleMovingAverage,1,3);Assert.Equal(5*double.Epsilon,small.OutputValues["UpperBand"][2]);Assert.Equal(3*double.Epsilon,small.OutputValues["LowerBand"][2]);
        var extreme=Bars(new[] {-double.MaxValue,double.MaxValue,double.MaxValue});Check(extreme,3,3,MovingAvgType.SimpleMovingAverage,1);var big=Data(extreme).CalculateKirshenbaumBands(MovingAvgType.SimpleMovingAverage,3,3);Assert.All(big.OutputValues["UpperBand"],v=>Assert.True(double.IsFinite(v)));Assert.All(big.OutputValues["LowerBand"],v=>Assert.True(double.IsFinite(v)));Assert.True(big.OutputValues["UpperBand"][2]>big.OutputValues["MiddleBand"][2]);
    }
    [Fact]
    public void UnpublishableResidualRmsStillProducesFiniteHalfWidthBands()
    {
        var bars=Bars(Enumerable.Range(0,24).Select(i=>(i/4)%2==0?-double.MaxValue:double.MaxValue).ToArray());Check(bars,8,10,MovingAvgType.SimpleMovingAverage,.5);var bands=Data(bars).CalculateKirshenbaumBands(MovingAvgType.SimpleMovingAverage,8,10,.5);Assert.All(bands.OutputValues["UpperBand"],v=>Assert.True(double.IsFinite(v)));Assert.All(bands.OutputValues["LowerBand"],v=>Assert.True(double.IsFinite(v)));Assert.True(bands.OutputValues["UpperBand"][17]>double.MaxValue*.5);
    }
    private static void Check(Bar[] bars,int meanLength,int errorLength,MovingAvgType kind,double factor)
    {
        var code=kind==MovingAvgType.SimpleMovingAverage?1:kind==MovingAvgType.WeightedMovingAverage?2:kind==MovingAvgType.ExponentialMovingAverage?3:6;
        var expected=BuiltInFormulaReferences.KirshenbaumOutputs(bars,meanLength,errorLength,code,factor);var batch=Data(bars).CalculateKirshenbaumBands(kind,meanLength,errorLength,factor);using var context=new ComputeContext();
        foreach(var key in new[] {"UpperBand","MiddleBand","LowerBand"}) {Assert.Equal(expected[key],batch.OutputValues[key]);using var raw=IndicatorCompute.ComputeKirshenbaumBandsFast(Data(bars),context,meanLength,errorLength,factor,kind,key=="UpperBand"?IndicatorCompute.ChannelBand.Upper:key=="LowerBand"?IndicatorCompute.ChannelBand.Lower:IndicatorCompute.ChannelBand.Middle);Assert.Equal(expected[key],raw.Span.ToArray());}
        using var state=new KirshenbaumBandsState(kind,meanLength,errorLength,factor);for(var replay=0;replay<2;replay++) {state.Reset();for(var i=0;i<bars.Length;i++) {state.Update(Native(new Bar(bars[i].Time,3,4,-2,-1,7)),false,true);foreach(var final in new[] {false,false,true}) {var actual=state.Update(Native(bars[i]),final,true);foreach(var key in new[] {"UpperBand","MiddleBand","LowerBand"})Assert.Equal(expected[key][i],actual.Outputs![key]);}}}
    }
    [Fact]
    public void SelectedPricesFeedFitWhileCustomerAverageControlsCenter()
    {
        var prices=new[] {2d,7,3,8,4};using var context=new ComputeContext();foreach(var external in new[] {false,true})foreach(var batch in new[] {false,true})
        {
            var data=Data(Bars(new[] {4d,4,4,4,4}));data.SetCustomValues(prices.ToList());var means=new[] {1d,2,3,4,5};var expected=BuiltInFormulaReferences.KirshenbaumOutputs(Bars(prices),2,3,3,2,external?means:null);
            using var armed=external?ComponentAverage.Arm(new Func<IReadOnlyList<double>,int,IReadOnlyList<double>>[] {(values,period)=>{Assert.Equal(2,period);Assert.Equal(prices,values);return means;}}):null;
            if(batch) {var output=data.CalculateKirshenbaumBands(length1:2,length2:3,stdDevFactor:2);foreach(var key in new[] {"UpperBand","MiddleBand","LowerBand"})Assert.Equal(expected[key],output.OutputValues[key]);}else {using var raw=IndicatorCompute.ComputeKirshenbaumBandsFast(data,context,2,3,2,band:IndicatorCompute.ChannelBand.Upper);Assert.Equal(expected["UpperBand"],raw.Span.ToArray());}if(external)Assert.Equal(1,ComponentAverage.Substitutions);
        }
    }
    [Fact]
    public void InvalidFieldsAndFactorsNeverAdvanceNativeState()
    {
        foreach(var field in Enumerable.Range(0,5))foreach(var invalid in new[] {double.NaN,double.PositiveInfinity,double.NegativeInfinity})foreach(var final in new[] {false,true})
        {
            using var state=new KirshenbaumBandsState(length1:2,length2:3);using var control=new KirshenbaumBandsState(length1:2,length2:3);var first=Native(new Bar(DateTime.UnixEpoch,2,3,1,2,1));state.Update(first,true,true);control.Update(first,true,true);
            var values=new[] {2d,4,1,2,1};values[field]=invalid;var bad=new OhlcvBar("KB",BarTimeframe.Minutes(1),DateTime.UnixEpoch,DateTime.UnixEpoch,values[0],values[1],values[2],values[3],values[4],true);Assert.Throws<ArgumentOutOfRangeException>(()=>state.Update(bad,final,true));var next=Native(new Bar(DateTime.UnixEpoch.AddMinutes(1),4,5,3,4,1));Assert.Equal(control.Update(next,true,true).Outputs!,state.Update(next,true,true).Outputs!);
        }
        foreach(var invalid in new[] {double.NaN,double.PositiveInfinity,double.NegativeInfinity}) {Assert.Throws<ArgumentOutOfRangeException>(()=>new KirshenbaumBandsState(stdDevFactor:invalid));Assert.Throws<ArgumentOutOfRangeException>(()=>Data(Array.Empty<Bar>()).CalculateKirshenbaumBands(stdDevFactor:invalid));using var context=new ComputeContext();Assert.Throws<ArgumentOutOfRangeException>(()=>IndicatorCompute.ComputeKirshenbaumBandsFast(Data(Array.Empty<Bar>()),context,stdDevFactor:invalid));}
    }
    [Fact]
    public void LegacyBatchAverageLeavesTheRegressionResidualsIndependent()
    {
        var bars=Bars(new[] {2d,7,4,9,3,5});var kind=MovingAvgType.HullMovingAverage;var mean=CalculationsHelper.GetMovingAverageList(Data(bars),kind,2,bars.Select(b=>b.Close).ToList()).ToArray();var expected=BuiltInFormulaReferences.KirshenbaumOutputs(bars,2,3,1,1,mean);var batch=Data(bars).CalculateKirshenbaumBands(kind,2,3);Assert.Equal(expected["UpperBand"],batch.OutputValues["UpperBand"]);using var context=new ComputeContext();using var raw=IndicatorCompute.ComputeKirshenbaumBandsFast(Data(bars),context,2,3,1,kind,IndicatorCompute.ChannelBand.Lower);Assert.Equal(expected["LowerBand"],raw.Span.ToArray());
    }
    public static IEnumerable<object[]> Cases=>IndicatorValidationDiscovery.Discover(new[] {typeof(IIndicator).Assembly}).Where(c=>c.IndicatorType==typeof(KirshenbaumBands)).Select(c=>new object[] {c});
    public static IEnumerable<object[]> Routes=>Cases.SelectMany(c=>new[] {"batch","fast","arm","native","streaming"}.Select(route=>new object[] {c[0],route}));
    [Theory,MemberData(nameof(Routes))]
    public void AllRoutesMatchIndependentFormulasIncludingPreviewAndReset(IndicatorValidationCase testCase,string route)=>new OrdinalFamilyNumericalTests().CheckRoutes(testCase,route,bars=>BuiltInFormulaReferences.KirshenbaumOutputs(bars,(IBuiltInIndicator)testCase.Factory()),IndicatorErrorBudget.Exact);
    [Theory,MemberData(nameof(Cases))]
    public Task SelectedSourcePreservesTheFormulaAndOriginalCandleFields(IndicatorValidationCase testCase)=>new OrdinalFamilyNumericalTests().SelectedSourcePreservesTheFormulaAndOriginalCandleFields(testCase);
    [Theory,MemberData(nameof(Cases))]
    public void EveryPublishedOutputRejectsAnInjectedValueFault(IndicatorValidationCase testCase)=>new OrdinalFamilyNumericalTests().EveryPublishedOutputRejectsAnInjectedValueFault(testCase);
    [Theory,MemberData(nameof(Cases))]
    public Task PublicConfigurationsPassEveryNumericalClass(IndicatorValidationCase testCase)=>new OrdinalFamilyNumericalTests().PublicConfigurationsPassEveryNumericalClass(testCase);
}

using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Builder.Compute;
using OoplesFinance.StockIndicators.Builder.Specs;
using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Streaming;
using OoplesFinance.StockIndicators.Validation;
namespace OoplesFinance.StockIndicators.Tests.Unit.ValidationTests;
public sealed class PivotAverageNumericalTests
{
    private static StockData Data(Bar[] bars)=>new(bars.Select(b=>b.Open),bars.Select(b=>b.High),bars.Select(b=>b.Low),bars.Select(b=>b.Close),bars.Select(b=>b.Volume),bars.Select(b=>b.Time));
    private static OhlcvBar Native(Bar b)=>new("PPA",BarTimeframe.Minutes(1),b.Time,b.Time,b.Open,b.High,b.Low,b.Close,b.Volume,true);
    private static MovingAvgType Kind(int kind)=>kind switch{2=>MovingAvgType.WeightedMovingAverage,3=>MovingAvgType.ExponentialMovingAverage,6=>MovingAvgType.WildersSmoothingMethod,_=>MovingAvgType.SimpleMovingAverage};
    private static void Check(Bar[] bars,int kind,int length,InputLength period)
    {
        var expected=BuiltInFormulaReferences.PivotAverageOutputs(bars,length,kind,period);var actual=Data(bars).CalculatePivotPointAverage(Kind(kind),length,period);
        var options=new PivotPointAverageSpecOptions(length,period,Kind(kind));
        foreach(var key in expected.Keys)
        {
            Assert.Equal(expected[key],actual.OutputValues[key]);using var context=new ComputeContext();using var raw=IndicatorCompute.TryComputeFast(Data(bars),new IndicatorSpec(IndicatorName.PivotPointAverage,options,key),context);Assert.NotNull(raw);Assert.Equal(expected[key],raw.Value.ToArray());
        }
        using var state=new PivotPointAverageState(Kind(kind),length,period);
        for(var replay=0;replay<2;replay++)
        {
            state.Reset();
            for(var i=0;i<bars.Length;i++)foreach(var final in new[]{false,false,true})
            {
                var values=state.Update(Native(bars[i]),final,true).Outputs!;
                foreach(var key in expected.Keys)Assert.Equal(expected[key][i],values[key]);
            }
        }
    }
    [Theory]
    [InlineData(1)] [InlineData(2)] [InlineData(3)] [InlineData(6)]
    public void AllCalendarPeriodsPreserveRoundedMeansAndSignals(int kind)
    {
        var start=new DateTime(2024,1,1);
        foreach(var period in new[]{InputLength.Minute,InputLength.Hour,InputLength.Day,InputLength.Week,InputLength.Month,InputLength.Year})
        {
            DateTime Time(int i)=>period switch{InputLength.Minute=>start.AddMinutes(i/3).AddSeconds(i%3),InputLength.Hour=>start.AddHours(i/3).AddMinutes(i%3),InputLength.Day=>start.AddDays(i/3).AddHours(i%3),InputLength.Week=>start.AddDays(7*(i/3)+i%3),InputLength.Month=>start.AddMonths(i/3).AddDays(i%3),_=>start.AddYears(i/3).AddMonths(i%3)};
            foreach(var scale in new[]{1d,double.Epsilon,double.MaxValue/4})
                Check(Enumerable.Range(0,12).Select(i=>new Bar(Time(i),(i%3-1)*scale,4*scale,-3*scale,(i%5-2)*scale,1)).ToArray(),kind,2,period);
            Check(Enumerable.Range(0,9).Select(i=>new Bar(Time(i),double.MaxValue,double.MaxValue,double.MaxValue,double.MaxValue,1)).ToArray(),kind,2,period);
            // 0, 0.1, 0.2 distinguish one rounded exact mean from a rounded running sum.
            Check(new[]{0.1,0.2,0.3}.Select((v,i)=>new Bar(Time(3*i),v,v,v,v,1)).ToArray(),kind,3,period);
            Check(Array.Empty<Bar>(),kind,2,period);
        }
    }
    [Fact]
    public void CurrentOpenDefinesFirstPeriodQuarterAndThirdMeans()
    {
        var bars=new[]{new Bar(DateTime.UnixEpoch,12,15,9,13,1),new Bar(DateTime.UnixEpoch.AddHours(1),99,16,8,14,1)};
        var values=BuiltInFormulaReferences.PivotAverageOutputs(bars,1,1,InputLength.Day);
        Assert.Equal(new[]{0d,0},values["Pivot1"]);Assert.Equal(new[]{3d,3},values["Pivot2"]);Assert.Equal(new[]{4d,4},values["Pivot3"]);Check(bars,1,1,InputLength.Day);
    }
    [Fact]
    public void SelectedClosesKeepOriginalSessionOpensAndExtrema()
    {
        var bars=Enumerable.Range(0,12).Select(i=>new Bar(DateTime.UnixEpoch.AddDays(i/3).AddHours(i%3),i%3+1,8,-2,1,1)).ToArray();var selected=Enumerable.Range(0,12).Select(i=>i%2==0?100d:-50d).ToArray();var projected=bars.Select((b,i)=>new Bar(b.Time,b.Open,b.High,b.Low,selected[i],b.Volume)).ToArray();
        var expected=BuiltInFormulaReferences.PivotAverageOutputs(projected,2,2,InputLength.Day);
        foreach(var key in expected.Keys)
        {
            var data=Data(bars);data.SetCustomValues(selected.ToList());Assert.Equal(expected[key],data.CalculatePivotPointAverage(MovingAvgType.WeightedMovingAverage,2).OutputValues[key]);
            data=Data(bars);data.SetCustomValues(selected.ToList());using var context=new ComputeContext();using var raw=IndicatorCompute.TryComputeFast(data,new IndicatorSpec(IndicatorName.PivotPointAverage,new PivotPointAverageSpecOptions(2,InputLength.Day,MovingAvgType.WeightedMovingAverage),key),context);Assert.NotNull(raw);Assert.Equal(expected[key],raw.Value.ToArray());
        }
    }
    [Fact]
    public void ThreeCustomerSignalStagesKeepPeriodInputsAndOrder()
    {
        var bars=new[]{new Bar(DateTime.UnixEpoch,1,6,0,3,1),new Bar(DateTime.UnixEpoch.AddDays(1),8,9,1,6,1),new Bar(DateTime.UnixEpoch.AddDays(2),-3,10,4,9,1)};
        var expected=BuiltInFormulaReferences.PivotAverageOutputs(bars,2,1,InputLength.Day);
        foreach(var batch in new[]{false,true})foreach(var slot in Enumerable.Range(0,3))
        {
            var callbacks=Enumerable.Range(0,3).Select(index=>new Func<IReadOnlyList<double>,int,IReadOnlyList<double>>((values,period)=>{Assert.Equal(2,period);Assert.Equal(expected["Pivot"+(index+1)],values);return Enumerable.Repeat(10d*(index+1),bars.Length).ToArray();})).ToArray();
            using var armed=ComponentAverage.Arm(callbacks);
            if(batch)Assert.All(Data(bars).CalculatePivotPointAverage(length:2).OutputValues["Signal"+(slot+1)],v=>Assert.Equal(10d*(slot+1),v));
            else{using var context=new ComputeContext();using var raw=IndicatorCompute.ComputePivotPointAverageFast(Data(bars),context,2,series:(IndicatorCompute.PivotPointAverageSeries)(2*slot+1));Assert.All(raw.Span.ToArray(),v=>Assert.Equal(10d*(slot+1),v));}
            Assert.Equal(3,ComponentAverage.Substitutions);
        }
    }
    [Fact]
    public void InvalidFieldsNeverAdvanceAPeriodBoundary()
    {
        foreach(var field in Enumerable.Range(0,5))foreach(var invalid in new[]{double.NaN,double.PositiveInfinity,double.NegativeInfinity})foreach(var final in new[]{false,true})
        {
            using var state=new PivotPointAverageState(length:2);using var control=new PivotPointAverageState(length:2);var first=Native(new Bar(DateTime.UnixEpoch,1,3,0,2,1));state.Update(first,true,true);control.Update(first,true,true);
            var v=new[]{1d,3,0,2,1};v[field]=invalid;var bad=Native(new Bar(DateTime.UnixEpoch.AddDays(1),v[0],v[1],v[2],v[3],v[4]));Assert.Throws<ArgumentOutOfRangeException>(()=>state.Update(bad,final,true));
            for(var i=1;i<9;i++){var bar=Native(new Bar(DateTime.UnixEpoch.AddDays(i/3),1,i+3,-2,i,1));Assert.Equal(control.Update(bar,true,true).Outputs!,state.Update(bar,true,true).Outputs!);}
        }
    }
    public static IEnumerable<object[]> Cases=>IndicatorValidationDiscovery.Discover(new[]{typeof(IIndicator).Assembly}).Where(c=>c.IndicatorType==typeof(PivotPointAverage)).Select(c=>new object[]{c});
    public static IEnumerable<object[]> Routes=>Cases.SelectMany(c=>new[]{"batch","fast","arm","native","streaming"}.Select(route=>new object[]{c[0],route}));
    [Theory,MemberData(nameof(Routes))]
    public void AllRoutesMatchIndependentStages(IndicatorValidationCase c,string route)=>new OrdinalFamilyNumericalTests().CheckRoutes(c,route,bars=>BuiltInFormulaReferences.PivotAverageOutputs(bars,(IBuiltInIndicator)c.Factory()),IndicatorErrorBudget.Exact);
    [Theory,MemberData(nameof(Cases))]
    public Task SelectedSourceKeepsOriginalCandles(IndicatorValidationCase c)=>new OrdinalFamilyNumericalTests().SelectedSourcePreservesTheFormulaAndOriginalCandleFields(c);
    [Theory,MemberData(nameof(Cases))]
    public void EveryOutputRejectsInjectedFaults(IndicatorValidationCase c)=>new OrdinalFamilyNumericalTests().EveryPublishedOutputRejectsAnInjectedValueFault(c);
    [Theory,MemberData(nameof(Cases))]
    public Task EveryConfigurationReceivesNumericalFixtures(IndicatorValidationCase c)=>new OrdinalFamilyNumericalTests().PublicConfigurationsPassEveryNumericalClass(c);
}

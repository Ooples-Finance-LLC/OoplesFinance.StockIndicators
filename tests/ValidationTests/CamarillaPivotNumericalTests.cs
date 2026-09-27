using OoplesFinance.StockIndicators.Builder.Specs;
using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Builder.Compute;
using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Streaming;
using OoplesFinance.StockIndicators.Validation;
namespace OoplesFinance.StockIndicators.Tests.Unit.ValidationTests;
public sealed class CamarillaPivotNumericalTests
{
    private static StockData Data(Bar[] bars)=>new(bars.Select(b=>b.Open),bars.Select(b=>b.High),bars.Select(b=>b.Low),bars.Select(b=>b.Close),bars.Select(b=>b.Volume),bars.Select(b=>b.Time));
    private static OhlcvBar Native(Bar b)=>new("ROCHLA",BarTimeframe.Minutes(1),b.Time,b.Time,b.Open,b.High,b.Low,b.Close,b.Volume,true);
    private static Bar Candle(int i,double h,double l,double c,double o)=>new(DateTime.UnixEpoch.AddDays(i/3).AddHours(i%3),o,h,l,c,1);
    private static IBuiltInIndicator Indicator(bool standard)=>new CamarillaPivotPoint();
    [Fact]
    public void CompletedSessionLevelsAndMidpointsMatchIndependentFractions()
    {
        foreach(var standard in new[]{true})
        {
            foreach(var scale in new[]{1d,double.Epsilon,double.MaxValue/8})Check(Enumerable.Range(0,18).Select(i=>Candle(i,4*scale,-3*scale,(i%5-2)*scale,(i%3-1)*scale)).ToArray(),standard);
            Check(Array.Empty<Bar>(),standard);
            var flat=Enumerable.Range(0,6).Select(i=>Candle(i,double.MaxValue,double.MaxValue,double.MaxValue,double.MaxValue)).ToArray();Check(flat,standard);
            foreach(var values in BuiltInFormulaReferences.CamarillaPivotOutputs(flat).Values)Assert.All(values.Skip(3),v=>Assert.Equal(double.MaxValue,v));
            Check(Enumerable.Range(0,12).Select(i=>Candle(i,double.MaxValue,-double.MaxValue,i%2==0?double.MaxValue:-double.MaxValue,0)).ToArray(),standard);
        }
        var hand=new[]{Candle(0,4,0,3,1),Candle(1,6,2,5,3),Candle(2,5,1,4,2),Candle(3,10,1,5,2)};
        Assert.Equal(10d/3,BuiltInFormulaReferences.CamarillaPivotOutputs(hand)["Pivot"][3]);Check(hand,true);Check(hand,false);
    }
    private static void Check(Bar[] bars,bool standard)
    {
        var expected=BuiltInFormulaReferences.CamarillaPivotOutputs(bars);var batch=Data(bars).CalculateCamarillaPivotPoints();
        var indicator=Indicator(standard);foreach(var key in expected.Keys)
        {
            Assert.Equal(expected[key],batch.OutputValues[key]);using var context=new ComputeContext();using var result=IndicatorCompute.TryComputeFast(Data(bars),new IndicatorSpec(indicator.BatchName,indicator.CreateOptions(),key),context);Assert.NotNull(result);Assert.Equal(expected[key],result.Value.ToArray());
        }
        IStreamingIndicatorState state=new CamarillaPivotPointsState();
        for(var replay=0;replay<2;replay++){state.Reset();for(var i=0;i<bars.Length;i++)
        {
            state.Update(Native(new Bar(bars[i].Time.AddDays(20),0,40,-30,20,7)),false,true);
            foreach(var final in new[]{false,false,true}){var result=state.Update(Native(bars[i]),final,true);Assert.Equal(expected["Pivot"][i],result.Value);foreach(var key in expected.Keys)Assert.Equal(expected[key][i],result.Outputs![key]);}
        }}
    }
    [Fact]
    public void SelectedPricesPreserveSessionOpenAndExtremes()
    {
        var bars=Enumerable.Range(0,12).Select(i=>Candle(i,8,-2,1,i%3)).ToArray();var selected=Enumerable.Range(0,12).Select(i=>i%2==0?100d:-50d).ToArray();var selectedBars=bars.Select((b,i)=>new Bar(b.Time,b.Open,b.High,b.Low,selected[i],b.Volume)).ToArray();
        foreach(var standard in new[]{true})foreach(var route in new[]{"batch","fast","arm"})
        {
            var expected=BuiltInFormulaReferences.CamarillaPivotOutputs(selectedBars);var indicator=Indicator(standard);
            foreach(var key in expected.Keys){var data=Data(bars);data.SetCustomValues(selected.ToList());
                if(route!="batch"){using var context=new ComputeContext();var spec=new IndicatorSpec(indicator.BatchName,indicator.CreateOptions(),key);using var result=route=="arm"?IndicatorCompute.ComputeArm(data,spec,context):IndicatorCompute.TryComputeFast(data,spec,context);Assert.NotNull(result);Assert.Equal(expected[key],result.Value.ToArray());}
                else {var result=data.CalculateCamarillaPivotPoints();Assert.Equal(expected[key],result.OutputValues[key]);}
            }
        }
    }
    [Fact]
    public void LegacyCalendarPeriodsKeepTheirOwnCompletedPeriod()
    {
        var start=new DateTime(2024,1,1);var bars=Enumerable.Range(0,9).Select(i=>Candle(i,i+3,i-2,i+1,i)).ToArray();
        foreach(var period in new[]{InputLength.Minute,InputLength.Hour,InputLength.Day,InputLength.Week,InputLength.Month,InputLength.Year})foreach(var standard in new[]{true})
        {
            var remapped=bars.Select((b,i)=>new Bar(period switch{InputLength.Minute=>start.AddMinutes(i/3).AddSeconds(i%3),InputLength.Hour=>start.AddHours(i/3).AddMinutes(i%3),InputLength.Day=>start.AddDays(i/3).AddHours(i%3),InputLength.Week=>start.AddDays(7*(i/3)+i%3),InputLength.Month=>start.AddMonths(i/3).AddDays(i%3),_=>start.AddYears(i/3).AddMonths(i%3)},b.Open,b.High,b.Low,b.Close,b.Volume)).ToArray();
            var expected=BuiltInFormulaReferences.CamarillaPivotOutputs(bars);var actual=Data(remapped).CalculateCamarillaPivotPoints(period);foreach(var key in expected.Keys)Assert.Equal(expected[key],actual.OutputValues[key]);
        }
    }
    [Fact]
    public void InvalidFieldsCannotCommitASessionBoundary()
    {
        foreach(var standard in new[]{true})foreach(var field in Enumerable.Range(0,5))foreach(var invalid in new[]{double.NaN,double.PositiveInfinity,double.NegativeInfinity})foreach(var final in new[]{true})
        {
            IStreamingIndicatorState Create()=>new CamarillaPivotPointsState();var state=Create();var control=Create();var first=Native(Candle(0,3,0,1,2));state.Update(first,true,true);control.Update(first,true,true);var v=new[]{0d,4,-2,1,1};v[field]=invalid;var bad=new OhlcvBar("PIVOT",BarTimeframe.Minutes(1),DateTime.UnixEpoch.AddDays(1),DateTime.UnixEpoch.AddDays(1),v[0],v[1],v[2],v[3],v[4],true);Assert.Throws<ArgumentOutOfRangeException>(()=>state.Update(bad,final,true));foreach(var i in Enumerable.Range(1,9)){var bar=Native(Candle(i,i+1,-2,i%3,1));Assert.Equal(control.Update(bar,true,true).Outputs!,state.Update(bar,true,true).Outputs!);}
        }
    }
    [Fact]
    public void ResetStartsFreshEvenWhenTheNextBarUsesTheSameSessionDate()
    {
        foreach(var standard in new[]{true})
        {
            IStreamingIndicatorState Create()=>new CamarillaPivotPointsState();var state=Create();var control=Create();state.Update(Native(Candle(0,10,5,8,6)),true,true);state.Reset();
            foreach(var bar in new[]{Candle(0,-2,-4,-3,-3),Candle(3,2,0,1,1)})Assert.Equal(control.Update(Native(bar),true,true).Outputs!,state.Update(Native(bar),true,true).Outputs!);
        }
    }
    [Fact]
    public void FifthLevelsPreserveExtendedRatiosAndTheZeroLowConvention()
    {
        var maximum=Enumerable.Repeat(double.MaxValue,3).ToArray();var core=new double[3];
        OoplesFinance.StockIndicators.Core.TrendCore.CamarillaPivotPoint(maximum,maximum,maximum,core);Assert.Equal(new[]{0d,double.MaxValue,double.MaxValue},core);
        foreach(var low in new[]{0d,double.Epsilon,-double.Epsilon,1d})
        {
            var bars=new[]{new Bar(DateTime.UnixEpoch,0,double.MaxValue,low,double.Epsilon,1),new Bar(DateTime.UnixEpoch.AddDays(1),0,0,0,0,1)};Check(bars,true);
            var expected=BuiltInFormulaReferences.CamarillaPivotOutputs(bars);var output=new double[2];
            OoplesFinance.StockIndicators.Core.TrendCore.CamarillaPivotPoint(bars.Select(b=>b.High).ToArray(),bars.Select(b=>b.Low).ToArray(),bars.Select(b=>b.Close).ToArray(),output);Assert.Equal(expected["Pivot"],output);
            if(low==0)Assert.Equal(0,expected["R5"][1]);if(low==double.Epsilon)Assert.Equal(double.MaxValue,expected["R5"][1]);
        }
    }
    public static IEnumerable<object[]> Cases=>IndicatorValidationDiscovery.Discover(new[] {typeof(IIndicator).Assembly}).Where(c=>c.IndicatorType==typeof(CamarillaPivotPoint)).Select(c=>new object[] {c});
    public static IEnumerable<object[]> Routes=>Cases.SelectMany(c=>new[] {"batch","fast","arm","native","streaming"}.Select(route=>new object[] {c[0],route}));
    [Theory,MemberData(nameof(Routes))]
    public void AllRoutesMatchIndependentFormulasIncludingPreviewAndReset(IndicatorValidationCase testCase,string route)=>new OrdinalFamilyNumericalTests().CheckRoutes(testCase,route,bars=>BuiltInFormulaReferences.CamarillaPivotOutputs(bars),IndicatorErrorBudget.Exact);
    [Theory,MemberData(nameof(Cases))]
    public Task SelectedSourcePreservesTheFormulaAndOriginalCandleFields(IndicatorValidationCase testCase)=>new OrdinalFamilyNumericalTests().SelectedSourcePreservesTheFormulaAndOriginalCandleFields(testCase);
    [Theory,MemberData(nameof(Cases))]
    public void EveryPublishedOutputRejectsAnInjectedValueFault(IndicatorValidationCase testCase)=>new OrdinalFamilyNumericalTests().EveryPublishedOutputRejectsAnInjectedValueFault(testCase);
    [Theory,MemberData(nameof(Cases))]
    public Task PublicConfigurationsPassEveryNumericalClass(IndicatorValidationCase testCase)=>new OrdinalFamilyNumericalTests().PublicConfigurationsPassEveryNumericalClass(testCase);
}

using OoplesFinance.StockIndicators.Builder.Compute;
using OoplesFinance.StockIndicators.Core;
using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Streaming;
using OoplesFinance.StockIndicators.Validation;
using ChannelBand = OoplesFinance.StockIndicators.Builder.Compute.IndicatorCompute.ChannelBand;
using F = OoplesFinance.StockIndicators.Helpers.UltimatePowerWeights.Fraction;
using N = OoplesFinance.StockIndicators.Helpers.MacZWindow.Number;

namespace OoplesFinance.StockIndicators.Tests.Unit.ValidationTests;

public sealed class VariableAverageNumericalTests
{
    public static IEnumerable<object[]> Cases=>IndicatorValidationDiscovery.Discover(new[] { typeof(IIndicator).Assembly })
        .Where(c=>c.IndicatorType==typeof(Vma) || c.IndicatorType==typeof(VariableMovingAverageBands)).Select(c=>new object[] { c });
    public static IEnumerable<object[]> Routes=>Cases.SelectMany(c=>new[] { "batch","fast","native","streaming" }.Select(r=>new object[] { c[0],r }));
    private static StockData Data(Bar[] bars)=>new(bars.Select(b=>b.Open),bars.Select(b=>b.High),bars.Select(b=>b.Low),bars.Select(b=>b.Close),bars.Select(b=>b.Volume),bars.Select(b=>b.Time));
    private static OhlcvBar Native(Bar b)=>new("VMA",BarTimeframe.Minutes(1),b.Time,b.Time,b.Open,b.High,b.Low,b.Close,b.Volume,true);
    private static Bar[] Bars(params double[] prices)=>prices.Select((p,i)=>new Bar(DateTime.UnixEpoch.AddMinutes(i),p,p,p,p,1)).ToArray();
    [Theory,MemberData(nameof(Routes))]
    public void RoutesMatchIndependentVectorReference(IndicatorValidationCase c,string route)
    {
        var builtin=(IBuiltInIndicator)c.Factory();var o=builtin.CreateOptions();
        var length=(int)o.GetType().GetProperty("Length")!.GetValue(o)!;
        IReadOnlyDictionary<string,double[]> Reference(IReadOnlyList<Bar> bars)=>builtin.BatchName==IndicatorName.VariableMovingAverage
            ? new Dictionary<string,double[]> { ["Vma"]=BuiltInFormulaReferences.CertifiedVariableReference(bars.Select(b=>b.Close).ToArray(),length) }
            : BuiltInFormulaReferences.CertifiedVariableBandsReference(bars,length,(MovingAvgType)o.GetType().GetProperty("MaType")!.GetValue(o)!, (double)o.GetType().GetProperty("Mult")!.GetValue(o)!);
        new OrdinalFamilyNumericalTests().CheckRoutes(c,route,Reference,IndicatorErrorBudget.Exact);
    }
    [Theory,MemberData(nameof(Cases))]
    public Task SelectedSourcesPreserveOriginalCandles(IndicatorValidationCase c)=>new OrdinalFamilyNumericalTests().SelectedSourcePreservesTheFormulaAndOriginalCandleFields(c);
    [Theory,MemberData(nameof(Cases))]
    public void EveryOutputRejectsFaults(IndicatorValidationCase c)=>new OrdinalFamilyNumericalTests().EveryPublishedOutputRejectsAnInjectedValueFault(c);
    [Theory,MemberData(nameof(Cases))]
    public Task EveryNumericalClassIsEnrolled(IndicatorValidationCase c)=>new OrdinalFamilyNumericalTests().PublicConfigurationsPassEveryNumericalClass(c);
    [Fact]
    public void CoreAndAveragingDispatchRetainExactNarrowIndexBehavior()
    {
        var prices=Enumerable.Range(0,1000).Select(i=>(double)i).ToArray();
        var expected=new double[prices.Length];using(var engine=new ExactVariableMovingAverageEngine(6))
            for(var i=0;i<prices.Length;i++) expected[i]=engine.Next(prices[i],true);
        var core=new double[prices.Length];MovingAverageCore.VariableMovingAverage(prices,core,6);Assert.Equal(expected,core);
        var alias=prices.ToArray();MovingAverageCore.VariableMovingAverage(alias,alias,6);Assert.Equal(expected,alias);
        var data=Data(Bars(prices));var dispatched=CalculationsHelper.GetMovingAverageList(data,MovingAvgType.VariableMovingAverage,6,prices.ToList());
        Assert.Equal(expected,dispatched);
        Assert.True(core[^1]>prices[^1]-6);
    }
    [Fact]
    public void BandsKeepWideTrueRangesUntilFinalRounding()
    {
        var bar=new Bar(DateTime.UnixEpoch,0,double.MaxValue,-double.MaxValue,0,1);
        foreach(var kind in new[] { MovingAvgType.VariableMovingAverage,MovingAvgType.WeightedMovingAverage })
        {
            var expected=kind==MovingAvgType.VariableMovingAverage ? double.MaxValue/2 : double.MaxValue/3;
            using var state=new VariableMovingAverageBandsState(kind,2,.25);
            var actual=state.Update(Native(bar),true,true).Outputs!;
            Assert.Equal(expected,actual["UpperBand"]);Assert.Equal(-expected,actual["LowerBand"]);Assert.Equal(0,actual["MiddleBand"]);
            var batch=Data([bar]).CalculateVariableMovingAverageBands(kind,2,.25);
            foreach(var key in actual.Keys) Assert.Equal(actual[key],batch.OutputValues[key][0]);
            using var context=new ComputeContext();
            using var fast=IndicatorCompute.ComputeVariableMovingAverageBandsFast(Data([bar]),context,2,.25,kind,ChannelBand.Upper);
            Assert.Equal(expected,fast.Span[0]);
        }
        Assert.All(Data([bar]).CalculateVariableMovingAverageBands(mult:0).OutputValues.Values,values=>Assert.Equal(0,values[0]));
    }
    [Fact]
    public void NumberConversionPreservesValuesBeyondBinary64()
    {
        foreach(var value in new[] { -double.MaxValue,-double.Epsilon,0d,double.Epsilon,double.MaxValue })
        {
            var exact=F.Of(value)*3/7;var converted=N.Of(value).Times(3).Divide(7).ToFraction();
            Assert.Equal(0,exact.CompareTo(converted));
        }
    }
    [Fact]
    public void CustomerBandsReceivePriceThenTrueRangeAndRestoreInputs()
    {
        var data=Data(Bars(2,4,1));var selected=new[] { 2d,5,0 };data.SetCustomValues(selected.ToList());
        using(var armed=ComponentAverage.Arm(new Func<IReadOnlyList<double>,int,IReadOnlyList<double>>[] {
            (values,period)=> { Assert.Equal(selected,values);Assert.Equal(3,period);return new[] { 7d,8,9 }; },
            (values,period)=> { Assert.Equal(new[] { 0d,3,5 },values);Assert.Equal(selected,data.ChainedValues);return new[] { 2d,3,4 }; } }))
        {
            var result=data.CalculateVariableMovingAverageBands(length:3,mult:2);
            Assert.Equal(new[] { 11d,14,17 },result.OutputValues["UpperBand"]);
            Assert.Equal(new[] { 3d,2,1 },result.OutputValues["LowerBand"]);
            Assert.Equal(2,ComponentAverage.Substitutions);
        }
        var throwing=Data(Bars(2,4,1));throwing.SetCustomValues(selected.ToList());
        using var failure=ComponentAverage.Arm((_,_)=> { throwing.SetCustomValues(new List<double> { 91,92,93 });throw new InvalidOperationException("customer failure"); });
        Assert.Throws<InvalidOperationException>(()=>throwing.CalculateVariableMovingAverageBands());
        Assert.Equal(selected,throwing.ChainedValues);
    }
    [Fact]
    public void InvalidBandInputsDoNotAdvanceEitherAverage()
    {
        using var actual=new VariableMovingAverageBandsState(length:2);using var control=new VariableMovingAverageBandsState(length:2);
        actual.Update(Native(Bars(1)[0]),true,false);control.Update(Native(Bars(1)[0]),true,false);
        var invalid=new Bar(DateTime.UnixEpoch,2,double.NaN,1,2,1);
        Assert.Throws<ArgumentOutOfRangeException>(()=>actual.Update(Native(invalid),true,true));
        foreach(var bar in Bars(3,-1,4))
        {
            var expected=control.Update(Native(bar),true,true).Outputs!;var observed=actual.Update(Native(bar),true,true).Outputs!;
            foreach(var key in expected.Keys) Assert.Equal(expected[key],observed[key]);
        }
        foreach(var mult in new[] { double.NaN,double.PositiveInfinity,double.NegativeInfinity })
            Assert.Throws<ArgumentOutOfRangeException>(()=>new VariableMovingAverageBandsState(mult:mult));
    }
    [Fact]
    public void FastBandsHonorBothCustomerAveragesAndPreserveTheCaller()
    {
        foreach(var band in new[] { ChannelBand.Upper,ChannelBand.Middle,ChannelBand.Lower })
        {
            var data=Data(Bars(2,4,1));var selected=new[] { 2d,5,0 };data.SetCustomValues(selected.ToList());
            using var armed=ComponentAverage.Arm(new Func<IReadOnlyList<double>,int,IReadOnlyList<double>>[] {
                (values,_)=> { Assert.Equal(selected,values);return new[] { 7d,8,9 }; },
                (values,_)=> { Assert.Equal(new[] { 0d,3,5 },values);return new[] { 2d,3,4 }; } });
            using var context=new ComputeContext();using var result=IndicatorCompute.ComputeVariableMovingAverageBandsFast(data,context,3,2,band:band);
            var expected=band==ChannelBand.Upper ? new[] { 11d,14,17 } : band==ChannelBand.Lower ? new[] { 3d,2,1 } : new[] { 7d,8,9 };
            Assert.Equal(expected,result.ToArray());Assert.Equal(2,ComponentAverage.Substitutions);Assert.Equal(selected,data.ChainedValues);
        }
    }
    [Fact]
    public void AffineRangeHalfwayBandHasAnIndependentConstantHand()
    {
        var tiny=Math.Pow(2,-52);
        var bars=new[] { 11d,13,9,8,14,14,12,7,15,11,6,12 }
            .Select((p,i)=>new Bar(DateTime.UnixEpoch.AddMinutes(i),p,2*p+2,-tiny,p,1)).ToArray();
        // Every true range is 2*price + 2 + 2^-52. VMA is affine-equivariant,
        // so multiplier -1/2 makes the upper band exactly -1 - 2^-53.
        // The even binary64 neighbor at that midpoint is -1.
        var reference=BuiltInFormulaReferences.CertifiedVariableBandsReference(bars,3,MovingAvgType.VariableMovingAverage,-.5);
        Assert.All(reference["UpperBand"],value=>Assert.Equal(-1d,value));
        var batch=Data(bars).CalculateVariableMovingAverageBands(length:3,mult:-.5);
        Assert.All(batch.OutputValues["UpperBand"],value=>Assert.Equal(-1d,value));
    }
}

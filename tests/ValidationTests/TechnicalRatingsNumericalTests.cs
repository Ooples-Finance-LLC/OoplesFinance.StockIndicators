using System.Numerics;
using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Builder.Compute;
using OoplesFinance.StockIndicators.Builder.Specs;
using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Streaming;
using OoplesFinance.StockIndicators.Validation;

namespace OoplesFinance.StockIndicators.Tests.Unit.ValidationTests;

public sealed class TechnicalRatingsNumericalTests
{
    public static IEnumerable<object[]> Cases => IndicatorValidationDiscovery.Discover(new[] { typeof(IIndicator).Assembly })
        .Where(c => c.IndicatorType == typeof(TechnicalRatings)).Select(c => new object[] { c });
    public static IEnumerable<object[]> Routes => Cases.SelectMany(c => new[] { "batch", "fast", "arm", "native", "streaming" }.Select(route => new object[] { c[0], route }));
    [Theory, MemberData(nameof(Routes))]
    public void EveryRouteMatchesIndependentComponentVotes(IndicatorValidationCase c, string route) =>
        new OrdinalFamilyNumericalTests().CheckRoutes(c, route, bars => BuiltInFormulaReferences.TechnicalRatingValues(bars, (IBuiltInIndicator)c.Factory()), IndicatorErrorBudget.Exact);
    [Theory, MemberData(nameof(Cases))]
    public Task SelectedSourcePreservesComponentRanges(IndicatorValidationCase c) => new OrdinalFamilyNumericalTests().SelectedSourcePreservesTheFormulaAndOriginalCandleFields(c);
    [Theory, MemberData(nameof(Cases))]
    public void EveryOutputRejectsFaults(IndicatorValidationCase c) => new OrdinalFamilyNumericalTests().EveryPublishedOutputRejectsAnInjectedValueFault(c);
    [Theory, MemberData(nameof(Cases))]
    public Task EveryConfigurationPassesNumericalFixtures(IndicatorValidationCase c) => new OrdinalFamilyNumericalTests().PublicConfigurationsPassEveryNumericalClass(c);
    private static Bar B(double value, double volume = 1) => new(DateTime.UnixEpoch,value,value,value,value,volume);
    private static OhlcvBar Native(Bar b) => new("RATING",BarTimeframe.Minutes(1),b.Time,b.Time,b.Open,b.High,b.Low,b.Close,b.Volume,true);
    private static StockData Data(Bar[] bars) => new(bars.Select(b=>b.Open),bars.Select(b=>b.High),bars.Select(b=>b.Low),bars.Select(b=>b.Close),bars.Select(b=>b.Volume),bars.Select(b=>b.Time));
    [Theory]
    [InlineData(double.Epsilon)]
    [InlineData(1e-100)]
    [InlineData(1)]
    [InlineData(double.MaxValue)]
    public void ExtremeComponentsPreservePreviewAndReset(double scale)
    {
        var bars=Enumerable.Range(0,80).Select(i=>B((i%5==0?-1:i%3==0?0:1)*scale,i%3==0?double.MaxValue:double.Epsilon)).ToArray();
        var expected=BuiltInFormulaReferences.TechnicalRatingValues(bars,(IBuiltInIndicator)new TechnicalRatings());
        var batch=Data(bars).CalculateTechnicalRatings();
        foreach(var p in expected)Assert.Equal(p.Value,batch.OutputValues[p.Key]);
        using var state=new TechnicalRatingsState();
        for(var pass=0;pass<2;pass++)
        {
            state.Update(Native(B(123)),true,false);state.Reset();
            for(var i=0;i<bars.Length;i++)
            {
                state.Update(Native(B(-double.MaxValue)),false,false);
                foreach(var final in new[]{false,false,true})
                {
                    var point=state.Update(Native(bars[i]),final,true);
                    foreach(var p in expected)Assert.Equal(p.Value[i],point.Outputs![p.Key]);
                }
                var total=expected["Tr"][i];Assert.Equal(total>0.1?Signal.Buy:total< -0.1?Signal.Sell:Signal.None,batch.SignalsList[i]);
            }
        }
    }
    [Fact]
    public void TinyAndOverflowingValuesCastOrderedVotes()
    {
        Assert.Equal(-1,TechnicalRatingComparison.Compare(0,double.Epsilon));
        Assert.Equal(1,TechnicalRatingComparison.Compare(double.MaxValue,-double.MaxValue));
        var large=(TechnicalRatingValue)double.MaxValue+(TechnicalRatingValue)double.MaxValue;
        Assert.Equal(1,TechnicalRatingComparison.Compare(large,double.MaxValue));
        Assert.Equal(-1,TechnicalRatingComparison.Compare(large,large+large));
        Assert.Equal(0,TechnicalRatingComparison.Compare(large,large));
    }
    [Fact]
    public void ExactVolumeProductsSurviveOverflowUnderflowAndEviction()
    {
        foreach(var volume in new[]{double.Epsilon,double.MaxValue})
        {
            using var mean=new TechnicalRatingVolume(2);
            Assert.Equal(0,mean.Next(double.MaxValue,volume,true).Publish());
            Assert.Equal(0,mean.Next(-double.MaxValue,volume,false).Publish());
            Assert.Equal(0,mean.Next(-double.MaxValue,volume,true).Publish());
            Assert.Equal(-double.MaxValue,mean.Next(-double.MaxValue,volume,true).Publish());
            mean.Reset();Assert.Equal(0,mean.Next(7,volume,true).Publish());Assert.Equal(9,mean.Next(11,volume,true).Publish());
        }
        using var cancellation=new TechnicalRatingVolume(2);
        cancellation.Next(double.MaxValue,1,true);
        Assert.Equal(0,cancellation.Next(-double.MaxValue,-1,false).Publish());
        var nearlyCancelled=cancellation.Next(-double.MaxValue,-0.5,false);
        Assert.Equal(1,TechnicalRatingComparison.Compare(nearlyCancelled,double.MaxValue));
    }
    [Fact]
    public void MomentumRetainsOrderBeyondPublishedExponent()
    {
        using var momentum=new TechnicalRatingMomentum(2);
        momentum.Next(double.Epsilon,true);momentum.Next(double.Epsilon,true);
        var large=momentum.Next(double.MaxValue,false);var smaller=momentum.Next(double.MaxValue/2,false);
        Assert.Equal(1,TechnicalRatingComparison.Compare(large,smaller));
        Assert.Equal(-1,TechnicalRatingComparison.Compare(momentum.Next(-double.MaxValue,false),smaller));
        Assert.Equal(0,TechnicalRatingComparison.Compare(large,momentum.Next(double.MaxValue,false)));
        momentum.Reset();Assert.Equal(0,momentum.Next(7,true).Publish());
        Assert.Equal(0,momentum.Next(2,true).Publish());Assert.Equal(300,momentum.Next(21,false).Publish());
    }
    [Fact]
    public void InvalidFieldsDoNotCommitComponents()
    {
        foreach(var field in Enumerable.Range(0,5))foreach(var invalid in new[]{double.NaN,double.PositiveInfinity,double.NegativeInfinity})foreach(var final in new[]{false,true})
        {
            using var state=new TechnicalRatingsState();using var control=new TechnicalRatingsState();
            foreach(var bar in Enumerable.Range(0,24).Select(i=>Native(B(i%7)))){state.Update(bar,true,false);control.Update(bar,true,false);}
            var values=new[]{1d,2,0,1,1};values[field]=invalid;
            Assert.Throws<ArgumentOutOfRangeException>(()=>state.Update(Native(new Bar(DateTime.UnixEpoch,values[0],values[1],values[2],values[3],values[4])),final,true));
            foreach(var bar in Enumerable.Range(0,10).Select(i=>Native(B(i%3))))Assert.Equal(control.Update(bar,true,true).Outputs,state.Update(bar,true,true).Outputs);
        }
    }
    [Fact]
    public void CallbackRoutesStillRequestTheirComponents()
    {
        var bars=Enumerable.Range(0,64).Select(i=>B(10+i%7)).ToArray();
        Func<IReadOnlyList<double>,int,IReadOnlyList<double>> callback=(values,_)=>values.Select(_=>3d).ToArray();
        foreach(var fast in new[]{false,true})
        {
            using var scope=ComponentAverage.Arm(Enumerable.Repeat(callback,100).ToArray());
            if(fast){using var context=new ComputeContext();using var result=IndicatorCompute.ComputeTechnicalRatingsFast(Data(bars),context);Assert.Equal(bars.Length,result.Length);}
            else Assert.Equal(bars.Length,Data(bars).CalculateTechnicalRatings().CustomValuesList.Count);
            Assert.True(ComponentAverage.Requests>0);Assert.True(ComponentAverage.Substitutions>0);
        }
    }
    [Fact]
    public void AdditionalPublicPeriodsReachEveryRoute()
    {
        var bars=Enumerable.Range(0,96).Select(i=>new Bar(DateTime.UnixEpoch,10+i%11,25+i%17,-5-i%13,Math.Sin(i)*20,i%5)).ToArray();
        var constructor=typeof(TechnicalRatingsState).GetConstructors().Single();
        var batchMethod=typeof(Calculations).GetMethod(nameof(Calculations.CalculateTechnicalRatings))!;
        var fastMethod=typeof(IndicatorCompute).GetMethod("ComputeTechnicalRatingsFast",System.Reflection.BindingFlags.Static|System.Reflection.BindingFlags.NonPublic)!;
        foreach(var parameter in constructor.GetParameters().Where(p=>p.ParameterType==typeof(int)))
        {
            object? Argument(System.Reflection.ParameterInfo p)=>p.Name==parameter.Name?3:p.DefaultValue;
            using var state=(TechnicalRatingsState)constructor.Invoke(constructor.GetParameters().Select(Argument).ToArray());
            var data=Data(bars);
            batchMethod.Invoke(null,batchMethod.GetParameters().Select(p=>p.Name=="stockData"?data:Argument(p)).ToArray());
            foreach(var series in Enum.GetValues<IndicatorCompute.TechnicalRatingSeries>())
            {
                using var context=new ComputeContext();
                using var result=(ComputeBuffer)fastMethod.Invoke(null,fastMethod.GetParameters().Select(p=>p.Name=="data"?Data(bars):p.Name=="context"?context:p.Name=="series"?series:Argument(p)).ToArray())!;
                var key=series==IndicatorCompute.TechnicalRatingSeries.MovingAverages?"Mr":series==IndicatorCompute.TechnicalRatingSeries.Oscillator?"Or":"Tr";
                Assert.Equal(data.OutputValues[key],result.ToArray());
            }
            for(var i=0;i<bars.Length;i++)foreach(var point in state.Update(Native(bars[i]),true,true).Outputs!)Assert.Equal(data.OutputValues[point.Key][i],point.Value);
        }
    }
    [Fact]
    public void UnclampedWilliamsRetainsItsThresholdScale()
    {
        using var range=new TechnicalRatingWilliams(2);
        Assert.Equal(-50,range.Next(10,0,5,true).Publish());
        Assert.Equal(-25,range.Next(20,10,15,false).Publish());
        Assert.Equal(-50,range.Next(10,0,5,false).Publish());
        range.Reset();Assert.Equal(100,range.Next(10,0,20,true).Publish());
        range.Reset();Assert.Equal(-100,range.Next(3,3,3,true).Publish());
    }
    [Fact]
    public void RoundedComponentMeansDoNotInventCrossovers()
    {
        var c=Cases.Select(v=>(IndicatorValidationCase)v[0]).Single(v=>v.ToString().EndsWith("/shorter-periods",StringComparison.Ordinal));
        var indicator=(IBuiltInIndicator)c.Factory();
        Assert.True(BuilderArmBinding.TryGetTarget(indicator.CreateOptions().GetType(),out var target));
        var fixtures=IndicatorAdversarialCases.Generate(256,244).Where(f=>f.Name.EndsWith("/tiny",StringComparison.Ordinal)||f.Name.EndsWith("/evicted-spike",StringComparison.Ordinal)).ToArray();
        Assert.Equal(2,fixtures.Length);
        foreach(var fixture in fixtures)
        {
            var expected=BuiltInFormulaReferences.TechnicalRatingValues(fixture.Bars,indicator);
            foreach(var p in expected)
                Assert.Equal(p.Value,BuilderArmBinding.Compute(Data(fixture.Bars.ToArray()),new IndicatorSpec(indicator.BatchName,indicator.CreateOptions(),p.Key),target).ToArray());
        }
    }
    [Fact]
    public void ExtendedAveragesRetainTheirUpperExponent()
    {
        var units=ExactVarianceWindow.Units(double.MaxValue);
        var twice=TechnicalRatingValue.Ratio(2*units,BigInteger.One);
        foreach(var kind in new[]{MovingAvgType.SimpleMovingAverage,MovingAvgType.WeightedMovingAverage,MovingAvgType.ExponentialMovingAverage,MovingAvgType.WildersSmoothingMethod})
        {
            using var identity=new TechnicalRatingAverage(kind,1);
            Assert.Equal(0,TechnicalRatingComparison.Compare(twice,identity.Next(twice,false)));
            Assert.Equal(0,TechnicalRatingComparison.Compare(twice,identity.Next(twice,true)));
            identity.Reset();Assert.Equal(0,identity.Next(0,true).Publish());
        }
        using var ema=new TechnicalRatingAverage(MovingAvgType.ExponentialMovingAverage,2);
        Assert.Equal(0,TechnicalRatingComparison.Compare(twice,ema.Next(twice,true)));
        var four=TechnicalRatingValue.Ratio(4*units,BigInteger.One);
        Assert.Equal(0,TechnicalRatingComparison.Compare(TechnicalRatingValue.Ratio(3*units,BigInteger.One),ema.Next(four,false)));
        Assert.Equal(double.MaxValue,ema.Next(0,true).Publish());
        Assert.Equal(0,TechnicalRatingComparison.Compare(TechnicalRatingValue.Ratio(units,3),ema.Next(0,true)));
    }
    [Fact]
    public void AsymmetricCandlesKeepBullAndBearVotesSeparate()
    {
        var bars=Enumerable.Range(0,4).Select(i=>new Bar(DateTime.UnixEpoch,100-10*i,150-10*i,50-10*i,100-10*i,1)).ToArray();
        var result=Data(bars).CalculateTechnicalRatings();
        // At the second bar price is below its mean and positive bull power falls
        // from 50 to 45. The other ten oscillator rules cast neutral votes.
        Assert.Equal(-1d/11,result.OutputValues["Or"][1]);
        foreach(var p in BuiltInFormulaReferences.TechnicalRatingValues(bars,(IBuiltInIndicator)new TechnicalRatings()))Assert.Equal(p.Value,result.OutputValues[p.Key]);
    }
    [Fact]
    public void DirectFastSelectionMatchesIndependentComponentVotes()
    {
        var original=Enumerable.Range(0,64).Select(i=>new Bar(DateTime.UnixEpoch,100,120,80,100,1+i%3)).ToArray();
        var selected=Enumerable.Range(0,64).Select(i=>(double)(i%13-6)).ToArray();
        var projected=original.Select((b,i)=>new Bar(b.Time,b.Open,b.High,b.Low,selected[i],b.Volume)).ToArray();
        var indicator=new TechnicalRatings().Of(new Sma(1));
        var expected=BuiltInFormulaReferences.TechnicalRatingValues(projected,(IBuiltInIndicator)indicator);
        foreach(var series in Enum.GetValues<IndicatorCompute.TechnicalRatingSeries>())
        {
            var data=Data(original);data.SetCustomValues(selected.ToList());using var context=new ComputeContext();
            using var result=IndicatorCompute.ComputeTechnicalRatingsFast(data,context,series:series);
            var key=series==IndicatorCompute.TechnicalRatingSeries.MovingAverages?"Mr":series==IndicatorCompute.TechnicalRatingSeries.Oscillator?"Or":"Tr";
            Assert.Equal(expected[key],result.ToArray());Assert.Equal(selected,data.ChainedValues);
        }
    }
}

using OoplesFinance.StockIndicators.Builder.Compute;
using OoplesFinance.StockIndicators.Core;
using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Streaming;
using OoplesFinance.StockIndicators.Validation;

namespace OoplesFinance.StockIndicators.Tests.Unit.ValidationTests;

public sealed class VervoortSmoothedNumericalTests
{
    private static Bar B(double price, int i = 0) => new(DateTime.UnixEpoch.AddMinutes(i), price, price, price, price, 1);
    private static StockData Data(IReadOnlyList<Bar> bars) => new(bars.Select(b => b.Open), bars.Select(b => b.High),
        bars.Select(b => b.Low), bars.Select(b => b.Close), bars.Select(b => b.Volume), bars.Select(b => b.Time));
    private static OhlcvBar Native(Bar b) => new("SELO", BarTimeframe.Minutes(1), b.Time, b.Time, b.Open, b.High, b.Low, b.Close, b.Volume, true);
    public static IEnumerable<object[]> Cases => IndicatorValidationDiscovery.Discover(new[] { typeof(IIndicator).Assembly })
        .Where(c => c.IndicatorType == typeof(VervoortSmoothedOscillator)).Select(c => new object[] { c });
    public static IEnumerable<object[]> Routes => Cases.SelectMany(c => new[] { "batch", "fast", "arm", "native", "streaming" }
        .Select(route => new object[] { c[0], route }));
    [Theory, MemberData(nameof(Routes))]
    public void RoutesMatchIndependentTwoLegFormula(IndicatorValidationCase c, string route)
        => new OrdinalFamilyNumericalTests().CheckRoutes(c, route,
            bars => BuiltInFormulaReferences.VervoortSmoothedOutputs(bars, (IBuiltInIndicator)c.Factory()), IndicatorErrorBudget.Exact);
    [Theory, MemberData(nameof(Cases))]
    public Task SelectedInputsPreserveBands(IndicatorValidationCase c)
        => new OrdinalFamilyNumericalTests().SelectedSourcePreservesTheFormulaAndOriginalCandleFields(c);
    [Theory, MemberData(nameof(Cases))]
    public Task EnrolledConfigurationsPassNumericalFixtures(IndicatorValidationCase c)
        => new OrdinalFamilyNumericalTests().PublicConfigurationsPassEveryNumericalClass(c);
    [Fact]
    public void HandKeepsDistinctStochasticLeg()
    {
        var bars=Enumerable.Range(0,3).Select(i=>new Bar(DateTime.UnixEpoch.AddMinutes(i),6*i,6*i+6,6*i,6*i,1)).ToArray();
        var result=Data(bars).CalculateVervoortSmoothedOscillator(length1:1,length2:1,length3:1,smoothLength:1);
        Assert.Equal(new[]{0d,0,0},result.OutputValues["Vso"]);Assert.Equal(new[]{20d,20,20},result.OutputValues["Sk"]);
    }
    [Fact]
    public void AnalyticBandAndSmallSpreadCutoff()
    {
        var bars=new[]{0d,2,4}.Select((v,i)=>B(v,i)).ToArray();
        var result=Data(bars).CalculateVervoortSmoothedOscillator(length1:3,length3:1,smoothLength:1);
        Assert.Equal(70.41241452319315,result.OutputValues["Vso"][2],12);
        var near=new[]{1d,Math.BitIncrement(1d),1d}.Select((v,i)=>B(v,i)).ToArray();
        Assert.All(Data(near).CalculateVervoortSmoothedOscillator(length1:3,length3:1,smoothLength:1).OutputValues["Vso"],v=>Assert.Equal(0,v));
    }
    [Fact]
    public void ExtremePricesPreserveBothLegsSignalsPreviewAndReset()
    {
        foreach(var scale in new[]{double.Epsilon,1d,double.MaxValue/64})
        {
            var bars=Enumerable.Range(0,23).Select(i=>new Bar(DateTime.UnixEpoch.AddMinutes(i),(i%7-3)*scale,(i%7+5)*scale,-(i%5+5)*scale,(i%7-3)*scale,1)).ToArray();
            var signals=new List<Signal>();var expected=BuiltInFormulaReferences.VervoortSmoothedOutputs(bars,3,5,2,3,2,signals);
            var actual=Data(bars).CalculateVervoortSmoothedOscillator(3,5,2,3,2);foreach(var pair in expected)Assert.Equal(pair.Value,actual.OutputValues[pair.Key]);Assert.Equal(signals,actual.SignalsList);
            using var state=new VervoortSmoothedOscillatorState(3,5,2,3,2);using var signalState=new VervoortSmoothedWindow(3,5,2,3,2);
            for(var pass=0;pass<2;pass++)
            {
                state.Update(Native(B(17)),true,false);state.Reset();signalState.Next(17,17,17,17,true);signalState.Reset();
                for(var i=0;i<bars.Length;i++)
                {
                    state.Update(Native(B(-19)),false,false);Assert.Throws<ArgumentOutOfRangeException>(()=>state.Update(Native(B(double.NaN)),true,false));
                    var typical=new ExactMeanAccumulator();typical.Add(bars[i].High);typical.Add(bars[i].Low);typical.Add(bars[i].Close);
                    foreach(var final in new[]{false,false,true})Assert.Equal(signals[i],signalState.Next(bars[i].Close,typical.Mean(3),bars[i].High,bars[i].Low,final).Trade);
                    foreach(var final in new[]{false,false,true}){var point=state.Update(Native(bars[i]),final,true);Assert.Equal(expected["Vso"][i],point.Value);foreach(var pair in expected)Assert.Equal(pair.Value[i],point.Outputs![pair.Key]);}
                }
            }
        }
    }
    [Fact]
    public void HugePeriodsAndFastSelectionPreserveCallerState()
    {
        var bars=new[]{1d,3,2}.Select((v,i)=>B(v,i)).ToArray();var data=Data(bars);var values=data.CustomValuesList;var outputs=data.OutputValues;var signals=data.SignalsList;var name=data.IndicatorName;
        var expected=BuiltInFormulaReferences.VervoortSmoothedOutputs(bars,int.MaxValue,int.MaxValue,int.MaxValue,int.MaxValue,2);
        foreach(var series in new[]{IndicatorCompute.VervoortSmoothedSeries.Oscillator,IndicatorCompute.VervoortSmoothedSeries.Stochastic})
        {
            using var context=new ComputeContext();using var result=IndicatorCompute.ComputeVervoortSmoothedOscillatorFast(data,context,int.MaxValue,int.MaxValue,int.MaxValue,int.MaxValue,series:series);
            Assert.Equal(expected[series==IndicatorCompute.VervoortSmoothedSeries.Oscillator?"Vso":"Sk"],result.ToArray());
        }
        Assert.Same(values,data.CustomValuesList);Assert.Same(outputs,data.OutputValues);Assert.Same(signals,data.SignalsList);Assert.Equal(name,data.IndicatorName);
        Assert.Throws<ArgumentOutOfRangeException>(()=>data.CalculateVervoortSmoothedOscillator(stdDevMult:double.NaN));Assert.Same(outputs,data.OutputValues);
    }
    [Fact]
    public void CorePreservesPublicPrimaryAndOverlappingInput()
    {
        var input=Enumerable.Range(0,35).Select(i=>(double)(i%9-3)).ToArray();var expected=Data(input.Select((v,i)=>B(v,i)).ToArray()).CalculateVervoortSmoothedOscillator(length1:3).OutputValues["Vso"];
        var output=new double[input.Length];OscillatorCore.VervoortSmoothedOscillator(input,output,3);Assert.Equal(expected,output);
        var overlap=(double[])input.Clone();OscillatorCore.VervoortSmoothedOscillator(overlap,overlap,3);Assert.Equal(expected,overlap);
        OscillatorCore.VervoortSmoothedOscillator(Array.Empty<double>(),Array.Empty<double>(),3);
        var invalid=new[]{1d,double.NaN};var untouched=new[]{17d,19d};Assert.Throws<ArgumentOutOfRangeException>(()=>OscillatorCore.VervoortSmoothedOscillator(invalid,untouched,3));Assert.Equal(new[]{17d,19d},untouched);
    }
}

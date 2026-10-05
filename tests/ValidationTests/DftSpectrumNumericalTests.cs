using OoplesFinance.StockIndicators.Builder.Compute;
using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Streaming;
using OoplesFinance.StockIndicators.Validation;
namespace OoplesFinance.StockIndicators.Tests.Unit.ValidationTests;
public sealed class DftSpectrumNumericalTests
{
    private static Bar B(double price,int i=0)=>new(DateTime.UnixEpoch.AddMinutes(i),price,price,price,price,1);
    private static StockData Data(IReadOnlyList<Bar> bars)=>new(bars.Select(b=>b.Open),bars.Select(b=>b.High),bars.Select(b=>b.Low),bars.Select(b=>b.Close),bars.Select(b=>b.Volume),bars.Select(b=>b.Time));
    private static OhlcvBar Native(Bar b)=>new("DFT",BarTimeframe.Minutes(1),b.Time,b.Time,b.Open,b.High,b.Low,b.Close,b.Volume,true);
    public static IEnumerable<object[]> Cases=>IndicatorValidationDiscovery.Discover(new[]{typeof(IIndicator).Assembly}).Where(c=>c.IndicatorType==typeof(EhlersDiscreteFourierTransformSpectralEstimate)).Select(c=>new object[]{c});
    public static IEnumerable<object[]> Routes=>Cases.SelectMany(c=>new[]{"batch","fast","arm","native","streaming"}.Select(route=>new object[]{c[0],route}));
    [Theory,MemberData(nameof(Routes))]
    public void RoutesMatchIndependentSpectrum(IndicatorValidationCase c,string route)
        =>new OrdinalFamilyNumericalTests().CheckRoutes(c,route,bars=>BuiltInFormulaReferences.DftSpectrumOutputs(bars,(IBuiltInIndicator)c.Factory()),IndicatorErrorBudget.Exact);
    [Theory,MemberData(nameof(Cases))]
    public Task SelectedInputsPreserveSpectrum(IndicatorValidationCase c)=>new OrdinalFamilyNumericalTests().SelectedSourcePreservesTheFormulaAndOriginalCandleFields(c);
    [Theory,MemberData(nameof(Cases))]
    public Task EnrolledConfigurationsPassNumericalFixtures(IndicatorValidationCase c)=>new OrdinalFamilyNumericalTests().PublicConfigurationsPassEveryNumericalClass(c);
    [Fact]
    public void DyadicSpectrumMatchesOriginalRationalQuadrature()
    {
        foreach(var scale in new[]{double.Epsilon,1d,double.MaxValue/8})
        {
            var bars=Enumerable.Range(0,11).Select(i=>B((i%7-3)*scale,i)).ToArray();
            var original=BuiltInFormulaReferences.DftSpectrumOutputs(bars,7,3,rationalQuadrature:true)["Edftse"];
            Assert.Equal(original,BuiltInFormulaReferences.DftSpectrumOutputs(bars,7,3)["Edftse"]);
        }
    }
    [Fact]
    public void PowerExponentShortcutMatchesOriginalRounding()
    {
        foreach(var shift in new[]{0,2096,2097,2098,2130,4296,8592,13000})foreach(var sign in new[]{-1,1})foreach(var tail in new[]{-1,0,1})
        {
            var numerator=sign*((System.Numerics.BigInteger.One<<shift)+tail);var denominator=new System.Numerics.BigInteger(3);
            Assert.Equal(RocBankValue.RoundUnits(numerator,denominator),DftSpectrumWindow.RoundPower(numerator,denominator));
        }
    }
    [Fact]
    public void EmptySpectrumAndZeroPricesStayZero()
    {
        var bars=Enumerable.Range(0,13).Select(i=>B(i,i)).ToArray();Assert.All(Data(bars).CalculateEhlersDiscreteFourierTransformSpectralEstimate(3,4).OutputValues["Edftse"],v=>Assert.Equal(0,v));
        Assert.All(Data(bars.Select((b,i)=>B(0,i)).ToArray()).CalculateEhlersDiscreteFourierTransformSpectralEstimate(8,3).OutputValues["Edftse"],v=>Assert.Equal(0,v));
    }
    [Fact]
    public void ExtremePricesPreserveSpectrumSignalsPreviewAndReset()
    {
        foreach(var scale in new[]{double.Epsilon,1d,double.MaxValue/4})
        {
            var bars=Enumerable.Range(0,19).Select(i=>B((i%7-3)*scale,i)).ToArray();var signals=new List<Signal>();var expected=BuiltInFormulaReferences.DftSpectrumOutputs(bars,8,3,signals)["Edftse"];
            var actual=Data(bars).CalculateEhlersDiscreteFourierTransformSpectralEstimate(8,3);Assert.Equal(expected,actual.OutputValues["Edftse"]);Assert.Equal(signals,actual.SignalsList);
            using var state=new EhlersDiscreteFourierTransformSpectralEstimateState(8,3);using var signalState=new DftSpectrumWindow(8,3);
            for(var pass=0;pass<2;pass++)
            {
                for(var i=0;i<9;i++){state.Update(Native(B(17)),true,false);signalState.Next(17,true);}state.Reset();signalState.Reset();
                for(var i=0;i<bars.Length;i++)
                {
                    state.Update(Native(B(-19)),false,false);Assert.Throws<ArgumentOutOfRangeException>(()=>state.Update(Native(B(double.NaN)),true,false));
                    foreach(var final in new[]{false,false,true}){var point=state.Update(Native(bars[i]),final,true);Assert.Equal(expected[i],point.Value);Assert.Equal(expected[i],point.Outputs!["Edftse"]);Assert.Equal(signals[i],signalState.Next(bars[i].Close,final).Trade);}
                }
            }
        }
    }
    [Fact]
    public void NarrowHugeBinRangeUsesObservedHistoryAndPreservesCaller()
    {
        var bars=new[]{1d,3,2,-1,0,4,2,7}.Select((v,i)=>B(v,i)).ToArray();var data=Data(bars);var values=data.CustomValuesList;var outputs=data.OutputValues;var signals=data.SignalsList;var name=data.IndicatorName;
        var expected=BuiltInFormulaReferences.DftSpectrumOutputs(bars,int.MaxValue,int.MaxValue-1)["Edftse"];
        using var context=new ComputeContext();using var result=IndicatorCompute.ComputeDftSpectrumFast(data,context,int.MaxValue,int.MaxValue-1);Assert.Equal(expected,result.ToArray());
        Assert.Same(values,data.CustomValuesList);Assert.Same(outputs,data.OutputValues);Assert.Same(signals,data.SignalsList);Assert.Equal(name,data.IndicatorName);
        data.ClosePrices[2]=double.NaN;Assert.Throws<ArgumentOutOfRangeException>(()=>data.CalculateEhlersDiscreteFourierTransformSpectralEstimate());Assert.Same(outputs,data.OutputValues);Assert.Same(signals,data.SignalsList);
    }
    [Fact]
    public void SingleBinPublishesItsPeriodAfterRoofingStartup()
    {
        var bars=new[]{0d,0,0,0,8,0,0,0}.Select((v,i)=>B(v,i)).ToArray();var actual=Data(bars).CalculateEhlersDiscreteFourierTransformSpectralEstimate(3,3).OutputValues["Edftse"];
        Assert.All(actual.Take(4),v=>Assert.Equal(0,v));Assert.All(actual.Skip(4),v=>Assert.Equal(3,v));
    }
}

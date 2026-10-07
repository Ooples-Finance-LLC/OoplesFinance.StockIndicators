using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Builder.Compute;
using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Streaming;
using OoplesFinance.StockIndicators.Validation;
namespace OoplesFinance.StockIndicators.Tests.Unit.ValidationTests;
public sealed class TimeSeriesForecastNumericalTests
{
    private static StockData Data(Bar[] bars)=>new(bars.Select(b=>b.Open),bars.Select(b=>b.High),bars.Select(b=>b.Low),bars.Select(b=>b.Close),bars.Select(b=>b.Volume),bars.Select(b=>b.Time));
    private static Bar[] Bars(double[] prices)=>prices.Select((v,i)=>new Bar(DateTime.UnixEpoch.AddMinutes(i),v,v,v,v,1)).ToArray();
    private static OhlcvBar Native(Bar b)=>new("TSF",BarTimeframe.Minutes(1),b.Time,b.Time,b.Open,b.High,b.Low,b.Close,b.Volume,true);
    [Fact]
    public void RoundedEndpointAndCumulativeAbsoluteErrorPreserveAllBands()
    {
        foreach(var length in new[] {0,1,2,3,7,500})foreach(var scale in new[] {1d,double.Epsilon,double.MaxValue/8})Check(Bars(Enumerable.Range(0,32).Select(i=>(i%7-3)*scale).ToArray()),length);
        Check(Array.Empty<Bar>(),500);Check(Bars(new[] {-double.MaxValue,double.MaxValue,double.MaxValue}),3);
        var line=Bars(new[] {2d,4,6,8});Check(line,3);var exact=Data(line).CalculateTimeSeriesForecast(3);foreach(var key in new[] {"UpperBand","MiddleBand","LowerBand"})Assert.Equal(new[] {2d,4,6,8},exact.OutputValues[key]);
        var impulse=Bars(new[] {0d,0,6,0,0,0,0,0});Check(impulse,3);var retained=Data(impulse).CalculateTimeSeriesForecast(3);Assert.Equal(.5,retained.OutputValues["UpperBand"][7]);Assert.Equal(-.5,retained.OutputValues["LowerBand"][7]);Assert.Equal(0d,retained.OutputValues["MiddleBand"][7]);
        var tiny=Bars(new[] {0d,0,12*double.Epsilon});Check(tiny,3);Assert.Equal(11*double.Epsilon,Data(tiny).CalculateTimeSeriesForecast(3).OutputValues["UpperBand"][2]);
        var large=Bars(Enumerable.Range(0,80).Select(i=>(i%7-3)*(double.MaxValue/8)).ToArray());Check(large,3);var bands=Data(large).CalculateTimeSeriesForecast(3);Assert.All(bands.OutputValues["UpperBand"],v=>Assert.True(double.IsFinite(v)));Assert.All(bands.OutputValues["LowerBand"],v=>Assert.True(double.IsFinite(v)));
    }
    private static void Check(Bar[] bars,int length)
    {
        var expected=BuiltInFormulaReferences.TimeSeriesForecastOutputs(bars,length);var batch=Data(bars).CalculateTimeSeriesForecast(length);using var context=new ComputeContext();foreach(var key in expected.Keys) {Assert.Equal(expected[key],batch.OutputValues[key]);using var raw=IndicatorCompute.ComputeTimeSeriesForecastFast(Data(bars),context,length,key);Assert.Equal(expected[key],raw.Span.ToArray());}
        using var state=new TimeSeriesForecastState(length);for(var replay=0;replay<2;replay++) {state.Reset();for(var i=0;i<bars.Length;i++) {state.Update(Native(new Bar(bars[i].Time,3,4,-2,-1,7)),false,true);foreach(var final in new[] {false,false,true}) {var actual=state.Update(Native(bars[i]),final,true);foreach(var key in expected.Keys)Assert.Equal(expected[key][i],actual.Outputs![key]);}}}
    }
    [Fact]
    public void SelectedPricesFeedBothRegressionAndCumulativeResiduals()
    {
        var prices=new[] {2d,7,3,8,4,9,1};var expected=BuiltInFormulaReferences.TimeSeriesForecastOutputs(Bars(prices),3);using var context=new ComputeContext();foreach(var key in expected.Keys) {var data=Data(Bars(Enumerable.Repeat(4d,prices.Length).ToArray()));data.SetCustomValues(prices.ToList());Assert.Equal(expected[key],data.CalculateTimeSeriesForecast(3).OutputValues[key]);data=Data(Bars(Enumerable.Repeat(4d,prices.Length).ToArray()));data.SetCustomValues(prices.ToList());using var raw=IndicatorCompute.ComputeTimeSeriesForecastFast(data,context,3,key);Assert.Equal(expected[key],raw.Span.ToArray());}
    }
    [Fact]
    public void InvalidFieldsNeverAdvanceRegressionOrCumulativeError()
    {
        foreach(var field in Enumerable.Range(0,5))foreach(var invalid in new[] {double.NaN,double.PositiveInfinity,double.NegativeInfinity})foreach(var final in new[] {false,true})
        {
            using var state=new TimeSeriesForecastState(3);using var control=new TimeSeriesForecastState(3);var first=Native(new Bar(DateTime.UnixEpoch,0,3,-2,1,1));state.Update(first,true,true);control.Update(first,true,true);var v=new[] {0d,4,-2,1,1};v[field]=invalid;var bad=new OhlcvBar("TSF",BarTimeframe.Minutes(1),DateTime.UnixEpoch,DateTime.UnixEpoch,v[0],v[1],v[2],v[3],v[4],true);Assert.Throws<ArgumentOutOfRangeException>(()=>state.Update(bad,final,true));foreach(var b in Bars(new[] {4d,7,3,9,2,5}))Assert.Equal(control.Update(Native(b),true,true).Outputs!,state.Update(Native(b),true,true).Outputs!);
        }
    }
    public static IEnumerable<object[]> Cases=>IndicatorValidationDiscovery.Discover(new[] {typeof(IIndicator).Assembly}).Where(c=>c.IndicatorType==typeof(TimeSeriesForecast)).Select(c=>new object[] {c});
    public static IEnumerable<object[]> Routes=>Cases.SelectMany(c=>new[] {"batch","fast","arm","native","streaming"}.Select(route=>new object[] {c[0],route}));
    [Theory,MemberData(nameof(Routes))]
    public void AllRoutesMatchIndependentFormulasIncludingPreviewAndReset(IndicatorValidationCase testCase,string route)=>new OrdinalFamilyNumericalTests().CheckRoutes(testCase,route,bars=>BuiltInFormulaReferences.TimeSeriesForecastOutputs(bars,(IBuiltInIndicator)testCase.Factory()),IndicatorErrorBudget.Exact);
    [Theory,MemberData(nameof(Cases))]
    public Task SelectedSourcePreservesTheFormulaAndOriginalCandleFields(IndicatorValidationCase testCase)=>new OrdinalFamilyNumericalTests().SelectedSourcePreservesTheFormulaAndOriginalCandleFields(testCase);
    [Theory,MemberData(nameof(Cases))]
    public void EveryPublishedOutputRejectsAnInjectedValueFault(IndicatorValidationCase testCase)=>new OrdinalFamilyNumericalTests().EveryPublishedOutputRejectsAnInjectedValueFault(testCase);
    [Theory,MemberData(nameof(Cases))]
    public Task PublicConfigurationsPassEveryNumericalClass(IndicatorValidationCase testCase)=>new OrdinalFamilyNumericalTests().PublicConfigurationsPassEveryNumericalClass(testCase);
    [Fact]
    public void PrefixReferenceEqualsDirectCenteredWindowFits()
    {
        foreach(var length in new[] {0,1,2,3,7,500})foreach(var scale in new[] {1d,double.Epsilon,Math.Pow(2, -500),Math.Pow(2, 500),double.MaxValue/8})
        {
            var bars=Bars(Enumerable.Range(0,48).Select(i=>(i%11-5)*scale).ToArray());var direct=DirectCenteredFits(bars,length);var prefix=BuiltInFormulaReferences.TimeSeriesForecastOutputs(bars,length);foreach(var key in direct.Keys)Assert.Equal(direct[key],prefix[key]);
        }
        var extreme=Bars(new[] {-double.MaxValue,double.MaxValue,double.MaxValue});foreach(var key in new[] {"UpperBand","MiddleBand","LowerBand"})Assert.Equal(DirectCenteredFits(extreme,3)[key],BuiltInFormulaReferences.TimeSeriesForecastOutputs(extreme,3)[key]);
    }
    private static IReadOnlyDictionary<string, double[]> DirectCenteredFits(Bar[] bars, int length)
    {
        length = Math.Max(1, length); var prices = bars.Select(b => ReferenceFraction.FromDouble(b.Close)).ToArray(); var upper = new double[bars.Length]; var middle = new double[bars.Length]; var lower = new double[bars.Length]; var residuals = new ReferenceFraction[bars.Length];
        for (var i = 0; i < bars.Length; i++)
        {
            var start = Math.Max(0, i + 1 - length); var n = i + 1 - start; var average = prices.Skip(start).Take(n).Aggregate(new ReferenceFraction(0), (a,b) => a + b) / new ReferenceFraction(n); var center = new ReferenceFraction(n - 1) / new ReferenceFraction(2); var xx = new ReferenceFraction(0); var xy = new ReferenceFraction(0);
            for (var j = 0; j < n; j++) { var x = new ReferenceFraction(j) - center; xx += x * x; xy += x * (prices[start + j] - average); }
            var fit = (n == 1 ? average : average + xy / xx * center).RoundExtendedBinary64(); residuals[i] = (prices[i] - fit).Abs().RoundExtendedBinary64();
            var error = (residuals.Take(i + 1).Aggregate(new ReferenceFraction(0), (a,b) => a + b) / new ReferenceFraction(i + 1)).RoundExtendedBinary64();
            upper[i] = (fit + error).ToDouble(); middle[i] = fit.ToDouble(); lower[i] = (fit - error).ToDouble();
        }
        return new Dictionary<string, double[]> { { "UpperBand", upper }, { "MiddleBand", middle }, { "LowerBand", lower } };
    }
}

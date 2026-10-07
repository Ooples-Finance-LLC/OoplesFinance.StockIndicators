using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Builder.Compute;
using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Streaming;
using OoplesFinance.StockIndicators.Validation;
namespace OoplesFinance.StockIndicators.Tests.Unit.ValidationTests;
public sealed class EnhancedWilliamsNumericalTests
{
    private static Bar B(double price, double volume) => new(DateTime.UnixEpoch, price, price, price, price, volume);
    private static StockData Data(Bar[] bars) => new(bars.Select(b => b.Open), bars.Select(b => b.High), bars.Select(b => b.Low), bars.Select(b => b.Close), bars.Select(b => b.Volume), bars.Select(b => b.Time));
    private static OhlcvBar Native(Bar b) => new("AST", BarTimeframe.Minutes(1), b.Time, b.Time, b.Open, b.High, b.Low, b.Close, b.Volume, true);
    private static readonly (MovingAvgType Kind, int Reference)[] Kinds = { (MovingAvgType.SimpleMovingAverage, 1), (MovingAvgType.WeightedMovingAverage, 2), (MovingAvgType.ExponentialMovingAverage, 3), (MovingAvgType.WildersSmoothingMethod, 6) };
    public static IEnumerable<object[]> Cases => IndicatorValidationDiscovery.Discover(new[] { typeof(IIndicator).Assembly }).Where(c => c.IndicatorType == typeof(EnhancedWilliamsR)).Select(c => new object[] { c });
    public static IEnumerable<object[]> Routes => Cases.SelectMany(c => new[] { "batch", "fast", "arm", "native", "streaming" }.Select(route => new object[] { c[0], route }));
    [Theory, MemberData(nameof(Routes))]
    public void EveryRouteMatchesIndependentUnsimplifiedFormula(IndicatorValidationCase c, string route) => new OrdinalFamilyNumericalTests().CheckRoutes(c, route, bars => BuiltInFormulaReferences.EnhancedWilliamsOutputs(bars, (IBuiltInIndicator)c.Factory()), IndicatorErrorBudget.Exact);
    [Theory, MemberData(nameof(Cases))]
    public Task SelectedSourcePreservesCandles(IndicatorValidationCase c) => new OrdinalFamilyNumericalTests().SelectedSourcePreservesTheFormulaAndOriginalCandleFields(c);
    [Theory, MemberData(nameof(Cases))]
    public void EveryOutputRejectsInjectedFaults(IndicatorValidationCase c) => new OrdinalFamilyNumericalTests().EveryPublishedOutputRejectsAnInjectedValueFault(c);
    [Theory, MemberData(nameof(Cases))]
    public Task NumericalClassesAreEnrolled(IndicatorValidationCase c) => new OrdinalFamilyNumericalTests().PublicConfigurationsPassEveryNumericalClass(c);
    private static (Dictionary<string,double[]> Outputs, Signal[] Signals, bool[] Aligned) Check(Bar[] bars, int length = 5, int signalLength = 3, MovingAvgType kind = MovingAvgType.SimpleMovingAverage, int reference = 1)
    {
        var expected = BuiltInFormulaReferences.EnhancedWilliamsValues(bars, length, signalLength, reference); var batch = Data(bars).CalculateEnhancedWilliamsR(kind, length, signalLength);
        Assert.Equal(expected.Outputs["Ewr"], batch.CustomValuesList); Assert.Equal(expected.Signals, batch.SignalsList);
        foreach (var key in new[] { "Ewr", "Signal" })
        { Assert.Equal(expected.Outputs[key], batch.OutputValues[key]); using var context = new ComputeContext(); using var fast = IndicatorCompute.ComputeEnhancedWilliamsRFast(Data(bars), context, length, kind, signalLength, key == "Signal"); Assert.Equal(expected.Outputs[key], fast.ToArray()); }
        using var state = new EnhancedWilliamsRState(kind, length, signalLength); using var window = new EnhancedWilliamsWindow(kind, length, signalLength);
        for (var replay = 0; replay < 2; replay++)
        {
            state.Reset(); window.Reset();
            for (var i = 0; i < bars.Length; i++)
            {
                state.Update(Native(B(-99, 77)), false, false); window.Next(-99, 77, false);
                foreach (var final in new[] { false, false, true })
                {
                    var b = bars[i]; var point = state.Update(Native(b), final, true); var direct = window.Next(b.Close, b.Volume, final);
                    Assert.Equal(expected.Outputs["Ewr"][i], point.Value); Assert.Equal(point.Value, point.Outputs!["Ewr"]); Assert.Equal(expected.Outputs["Signal"][i], point.Outputs["Signal"]);
                    Assert.Equal(point.Value, direct.Line); Assert.Equal(point.Outputs["Signal"], direct.SignalLine); Assert.Equal(expected.Signals[i], direct.Trade); Assert.Equal(expected.Aligned[i], direct.Aligned);
                }
            }
        }
        return expected;
    }
    [Fact]
    public void HandBranchesPreserveNeutralAndExactZeroFactor()
    {
        var neutral = Check(Enumerable.Repeat(B(7, 3), 9).ToArray()); Assert.All(neutral.Outputs["Ewr"], v => Assert.Equal(50, v));
        var prices = new[] { 0d, 8, 7, 5, 3, 2 }; var bars = prices.Select((p,i) => B(p,i)).ToArray(); var values = Check(bars, 10, 1);
        Assert.Equal(50, values.Outputs["Ewr"][0]); Assert.Equal(201, values.Outputs["Ewr"][1]); Assert.True(values.Aligned[1]);
        // Final SMA price=5, volume=3, ranges=8 and5. P=-.75,V=.8,
        // normalized step=-.25 cancels AF=.25 exactly: use the other branch.
        Assert.False(values.Aligned[^1]); Assert.Equal(16.25, values.Outputs["Ewr"][^1]);
        Assert.Equal(values.Outputs["Ewr"], values.Outputs["Signal"]);
        Assert.Equal(Signal.Sell, values.Signals[4]);
        Check(bars, 9, 2); Check(bars, 11, 2);
    }
    [Fact]
    public void HandSignalsKeepStrictThresholdCrossings()
    {
        foreach (var sample in new[]
        {
            (new[] { 50d,150,50,-150,-50 }, new[] { Signal.None,Signal.None,Signal.Sell,Signal.None,Signal.Buy }),
            (new[] { 50d,150,100,50,-150,-100,-50 }, Enumerable.Repeat(Signal.None,7).ToArray())
        })
        {
            using var window = new EnhancedWilliamsWindow(MovingAvgType.SimpleMovingAverage,2,1);
            for (var i=0;i<sample.Item1.Length;i++)
            {
                var price=(double)(i%2);var target=sample.Item1[i];
                // Flat volume makes Ewr = 50 + 50*(price-priceMean).
                var point=window.Next(price,0,true,price-(target-50)/50,0,target);
                Assert.Equal(target,point.Line);Assert.Equal(target,point.SignalLine);Assert.Equal(sample.Item2[i],point.Trade);
            }
        }
    }
    [Fact]
    public void WideSignedTinyAndExpiredRangesKeepBothFactors()
    {
        foreach (var kind in Kinds) foreach (var scale in new[] { double.Epsilon, Math.Pow(2, -500), 1d, double.MaxValue / 8 })
            Check(Enumerable.Range(0, 19).Select(i => B((i % 7 - 3) * scale, (i % 9 - 4) * scale)).ToArray(), 5, 3, kind.Kind, kind.Reference);
        foreach (var kind in Kinds)
            Check(new[] { B(double.MaxValue,double.MaxValue), B(-double.MaxValue,-double.MaxValue), B(1,3), B(-1,2), B(0,1), B(4,7), B(3,-1) }, 2, 2, kind.Kind, kind.Reference);
    }
    [Fact]
    public void TinyPriceRatioTimesHugeVolumeRatioHasFiniteHandValues()
    {
        foreach (var variant in new[] { (double.MaxValue, double.Epsilon, 150d), (-double.MaxValue, double.Epsilon, 201d), (double.MaxValue, -double.Epsilon, -199d) })
        {
            var bars = new[] { B(variant.Item1,0), B(variant.Item2,double.Epsilon) }; var meanP = new[] { 0d,0 }; var meanV = new[] { -double.MaxValue,-double.MaxValue };
            var expected = BuiltInFormulaReferences.EnhancedWilliamsValues(bars, 2, 1, 1, meanP, meanV).Outputs;
            Assert.Equal(new[] { 50d, variant.Item3 }, expected["Ewr"]);
            foreach (var signal in new[] { false, true })
            {
                var calls = 0; Func<IReadOnlyList<double>,int,IReadOnlyList<double>> callback = (values, _) => ++calls == 1 ? meanP : calls == 2 ? meanV : values.ToArray();
                using var armed = ComponentAverage.Arm(Enumerable.Repeat(callback,signal ? 3 : 2).ToArray()); using var context = new ComputeContext();
                using var actual = IndicatorCompute.ComputeEnhancedWilliamsRFast(Data(bars),context,2,MovingAvgType.SimpleMovingAverage,1,signal);
                Assert.Equal(expected[signal ? "Signal" : "Ewr"],actual.ToArray()); Assert.Equal(signal ? 3 : 2,calls);
            }
        }
    }
    [Fact]
    public void ExtendedLinesCanCancelInsideSignalBeforePublication()
    {
        var bars = new[] { B(0,0), B(1,1), B(0,1) }; var meanP = new[] { 0d,-double.MaxValue,double.MaxValue }; var meanV = new[] { 0d,-double.MaxValue,-double.MaxValue };
        var expected = BuiltInFormulaReferences.EnhancedWilliamsValues(bars,3,2,1,meanP,meanV); using var window = new EnhancedWilliamsWindow(MovingAvgType.SimpleMovingAverage,3,2);
        for (var i=0;i<bars.Length;i++) { var point=window.Next(bars[i].Close,bars[i].Volume,true,meanP[i],meanV[i]); Assert.Equal(expected.Outputs["Ewr"][i],point.Line); Assert.Equal(expected.Outputs["Signal"][i],point.SignalLine); Assert.Equal(expected.Signals[i],point.Trade); }
        Assert.Equal(double.PositiveInfinity,expected.Outputs["Ewr"][1]); Assert.Equal(double.NegativeInfinity,expected.Outputs["Ewr"][2]); Assert.Equal(0,expected.Outputs["Signal"][2]);
    }
    [Fact]
    public void ExtremePeriodsKeepLazyWindowsAndMeanClamp()
    {
        var bars=Enumerable.Range(0,12).Select(i=>B(i%5-2,i%7-3)).ToArray();
        foreach(var kind in Kinds) { Check(bars,int.MaxValue,int.MaxValue,kind.Kind,kind.Reference); Check(bars,1,1,kind.Kind,kind.Reference); }
        Assert.Equal(2,EnhancedWilliamsWindow.MeanLength(1));Assert.Equal(529,EnhancedWilliamsWindow.MeanLength(1057));Assert.Equal(530,EnhancedWilliamsWindow.MeanLength(int.MaxValue));
    }
    [Fact]
    public void CustomMeansKeepThreeSlotsAndSelectedPricesWithOriginalVolume()
    {
        var selected=new[]{0d,2,1,4};var volumes=new[]{1d,3,2,5};var bars=volumes.Select(v=>B(99,v)).ToArray();var adjusted=selected.Select((p,i)=>B(p,volumes[i])).ToArray();
        var means=new double[4];var expected=BuiltInFormulaReferences.EnhancedWilliamsValues(adjusted,5,1,1,means,means).Outputs;
        foreach(var signal in new[]{false,true})
        {
            var requests=new List<int>();var inputs=new List<double[]>();Func<IReadOnlyList<double>,int,IReadOnlyList<double>> callback=(values,period)=>{requests.Add(period);inputs.Add(values.ToArray());return requests.Count<=2?means:values.ToArray();};
            using var armed=ComponentAverage.Arm(Enumerable.Repeat(callback,signal?3:2).ToArray());var data=Data(bars);data.SetCustomValues(selected.ToList());using var context=new ComputeContext();
            using var actual=IndicatorCompute.ComputeEnhancedWilliamsRFast(data,context,5,MovingAvgType.SimpleMovingAverage,1,signal);
            Assert.Equal(expected[signal?"Signal":"Ewr"],actual.ToArray());Assert.Equal(signal?new[]{3,3,1}:new[]{3,3},requests);Assert.Equal(selected,inputs[0]);Assert.Equal(volumes,inputs[1]);if(signal)Assert.Equal(expected["Ewr"],inputs[2]);Assert.Equal(selected,data.ChainedValues);
        }
        var calls=0;using var ignored=ComponentAverage.Arm((values,_)=>{calls++;return values;});var batch=Data(bars);batch.SetCustomValues(selected.ToList());batch.CalculateEnhancedWilliamsR(length:5,signalLength:1);Assert.Equal(0,calls);
        Assert.Equal(BuiltInFormulaReferences.EnhancedWilliamsValues(adjusted,5,1,1).Outputs["Ewr"],batch.CustomValuesList);
    }
    [Fact]
    public void LegacyMeansPreserveSupportedRoutesAndUnsupportedNativeRejection()
    {
        var bars=Enumerable.Range(0,17).Select(i=>B(i%5-2,i%7-3)).ToArray();
        foreach(var kind in new[]{MovingAvgType.LinearWeightedMovingAverage,MovingAvgType.DoubleExponentialMovingAverage})
        {
            var expected=Data(bars).CalculateEnhancedWilliamsR(kind,5,3);using var context=new ComputeContext();
            foreach(var signal in new[]{false,true}) {using var fast=IndicatorCompute.ComputeEnhancedWilliamsRFast(Data(bars),context,5,kind,3,signal);Assert.Equal(expected.OutputValues[signal?"Signal":"Ewr"],fast.ToArray());}
            if(kind==MovingAvgType.LinearWeightedMovingAverage)Assert.Throws<NotSupportedException>(()=>new EnhancedWilliamsRState(kind,5,3));
            else {using var state=new EnhancedWilliamsRState(kind,5,3);for(var i=0;i<bars.Length;i++){var actual=state.Update(Native(bars[i]),true,true);Assert.Equal(expected.OutputValues["Ewr"][i],actual.Value);Assert.Equal(expected.OutputValues["Signal"][i],actual.Outputs!["Signal"]);}}
        }
    }
    [Fact]
    public void ResetDropsOldVolumeMinimum()
    {
        var bars=new[]{B(0,1),B(2,2),B(1,3)};
        var expected=BuiltInFormulaReferences.EnhancedWilliamsValues(bars,3,2,1);
        Assert.Equal(51,expected.Outputs["Ewr"][1]);
        using var state=new EnhancedWilliamsRState(length:3,signalLength:2);
        using var window=new EnhancedWilliamsWindow(MovingAvgType.SimpleMovingAverage,3,2);
        state.Update(Native(B(17,-23)),true,false);window.Next(17,-23,true);
        state.Reset();window.Reset();
        for(var i=0;i<bars.Length;i++)foreach(var final in new[]{false,false,true})
        {
            var point=state.Update(Native(bars[i]),final,true);var direct=window.Next(bars[i].Close,bars[i].Volume,final);
            Assert.Equal(expected.Outputs["Ewr"][i],point.Value);Assert.Equal(point.Value,direct.Line);
            Assert.Equal(expected.Outputs["Signal"][i],point.Outputs!["Signal"]);Assert.Equal(point.Outputs["Signal"],direct.SignalLine);
            Assert.Equal(expected.Signals[i],direct.Trade);
        }
    }
    [Fact]
    public void InvalidCandlesCannotAdvancePriceVolumeOrSignalState()
    {
        foreach(var bad in new[]{double.NaN,double.PositiveInfinity,double.NegativeInfinity})foreach(var field in Enumerable.Range(0,5))foreach(var final in new[]{false,true})
        {
            using var state=new EnhancedWilliamsRState(length:3,signalLength:2);using var control=new EnhancedWilliamsRState(length:3,signalLength:2);var seed=Native(B(3,2));state.Update(seed,true,false);control.Update(seed,true,false);
            var v=new[]{2d,4,0,2,1};v[field]=bad;Assert.Throws<ArgumentOutOfRangeException>(()=>state.Update(Native(new Bar(DateTime.UnixEpoch,v[0],v[1],v[2],v[3],v[4])),final,true));
            foreach(var b in new[]{B(5,1),B(-2,3),B(0,7)}) {var expected=control.Update(Native(b),true,true);var actual=state.Update(Native(b),true,true);Assert.Equal(expected.Value,actual.Value);Assert.Equal(expected.Outputs!["Signal"],actual.Outputs!["Signal"]);}
        }
    }
}

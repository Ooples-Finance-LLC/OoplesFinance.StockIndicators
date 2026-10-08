using OoplesFinance.StockIndicators.Builder.Compute;
using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Validation;
using OoplesFinance.StockIndicators.Streaming;

namespace OoplesFinance.StockIndicators.Tests.Unit.ValidationTests;

public sealed class ValueChartNumericalTests
{
    [Theory]
    [InlineData(double.MaxValue, -double.MaxValue)]
    [InlineData(double.Epsilon, 0d)]
    public void OneBarCoordinatesRetainExactMedianAndRange(double high, double low)
    {
        // Length one: basis=(high+low)/2 and scale=(high-low)/25.
        // Hence each endpoint is exactly +/-25/2, independently of scale.
        // Missing four range samples are zeros, not repeated first samples.
        var time = DateTime.UnixEpoch;
        StockData Data() => new(new[] { low }, new[] { high }, new[] { low },
            new[] { high }, new[] { 1d }, new[] { time });
        var expected = new Dictionary<string, double>
        { ["vClose"] = 12.5, ["vHigh"] = 12.5, ["vLow"] = -12.5, ["vOpen"] = -12.5 };
        var reference = BuiltInFormulaReferences.ValueChartValues(new[] { new Bar(time, low, high, low, high, 1) }, 1, MovingAvgType.SimpleMovingAverage);
        foreach (var pair in expected) Assert.Equal(pair.Value, reference.Outputs[pair.Key][0]);
        var batch = Data().CalculateValueChartIndicator(length: 1);
        Assert.Empty(batch.CustomValuesList);
        foreach (var pair in expected) Assert.Equal(pair.Value, batch.OutputValues[pair.Key][0]);
        using var context = new ComputeContext();
        foreach (var pair in new[] { (IndicatorCompute.CandleSeries.Close, "vClose"), (IndicatorCompute.CandleSeries.High, "vHigh"),
            (IndicatorCompute.CandleSeries.Low, "vLow"), (IndicatorCompute.CandleSeries.Open, "vOpen") })
        {
            using var output = IndicatorCompute.ComputeValueChartIndicatorFast(Data(), context, 1,
                MovingAvgType.SimpleMovingAverage, pair.Item1);
            Assert.Equal(expected[pair.Item2], output.Span[0]);
        }
        using var state = new ValueChartIndicatorState(length: 1);
        var bar = new OhlcvBar("VC", BarTimeframe.Minutes(1), time, time, low, high, low, high, 1, true);
        for (var cycle = 0; cycle < 2; cycle++)
        {
            state.Reset();
            // Repeated previews must not add range samples to history.
            foreach (var final in new[] { false, false, true })
            {
                var result = state.Update(bar, final, true);
                Assert.Equal(12.5, result.Value);
                foreach (var pair in expected) Assert.Equal(pair.Value, result.Outputs![pair.Key]);
            }
        }
    }
    private static StockData Data(IReadOnlyList<Bar> bars) => new(bars.Select(b => b.Open), bars.Select(b => b.High),
        bars.Select(b => b.Low), bars.Select(b => b.Close), bars.Select(b => b.Volume), bars.Select(b => b.Time));
    public static IEnumerable<object[]> Cases => IndicatorValidationDiscovery.Discover(new[] { typeof(IIndicator).Assembly })
        .Where(c => c.IndicatorType == typeof(ValueChartIndicator)).Select(c => new object[] { c });
    public static IEnumerable<object[]> Routes => Cases.SelectMany(c => new[] { "batch", "fast", "arm", "native", "streaming" }
        .Select(route => new object[] { c[0], route }));
    [Theory, MemberData(nameof(Routes))]
    public void RoutesMatchIndependentCoordinates(IndicatorValidationCase c, string route) => new OrdinalFamilyNumericalTests()
        .CheckRoutes(c, route, bars => BuiltInFormulaReferences.ValueChartOutputs(bars, (IBuiltInIndicator)c.Factory()), IndicatorErrorBudget.Exact);
    [Theory, MemberData(nameof(Cases))]
    public void EveryOutputRejectsFaults(IndicatorValidationCase c) => new OrdinalFamilyNumericalTests().EveryPublishedOutputRejectsAnInjectedValueFault(c);
    [Theory, MemberData(nameof(Cases))]
    public Task NumericalClassesAreEnrolled(IndicatorValidationCase c) => new OrdinalFamilyNumericalTests().PublicConfigurationsPassEveryNumericalClass(c);
    [Theory, MemberData(nameof(Cases))]
    public async Task SelectedPricesPreserveProjectedRangesAndOriginalOpen(IndicatorValidationCase c)
    {
        await new OrdinalFamilyNumericalTests().SelectedSourcePreservesTheFormulaAndOriginalCandleFields(c);
        var options = ((IBuiltInIndicator)c.Factory()).CreateOptions();
        var length = (int)options.GetType().GetProperty("Length")!.GetValue(options)!;
        var kind = (MovingAvgType)options.GetType().GetProperty("MaType")!.GetValue(options)!;
        var bars = Enumerable.Range(0, 30).Select(i => new Bar(DateTime.UnixEpoch.AddMinutes(i), 100+i, 103+i, 97+i, 100+i, 1)).ToArray();
        var selected = bars.Select((_, i) => (double)(i*7%13-6)).ToArray();
        var projected = bars.Select((b, i) => new Bar(b.Time,b.Open,b.High,b.Low,selected[i],b.Volume)).ToArray();
        var expected = BuiltInFormulaReferences.ValueChartValues(projected, length, kind, true);
        var data = Data(bars); data.SetCustomValues(selected.ToList());
        using var context = new ComputeContext();
        foreach (var pair in new[] { (IndicatorCompute.CandleSeries.Close, "vClose"), (IndicatorCompute.CandleSeries.Open, "vOpen"),
            (IndicatorCompute.CandleSeries.High, "vHigh"), (IndicatorCompute.CandleSeries.Low, "vLow") })
        {
            using var output = IndicatorCompute.ComputeValueChartIndicatorFast(data, context, length, kind, pair.Item1);
            Assert.Equal(expected.Outputs[pair.Item2], output.ToArray());
        }
        Assert.Equal(selected, data.ChainedValues);
        data.CalculateValueChartIndicator(kind,length);
        foreach (var pair in expected.Outputs) Assert.Equal(pair.Value, data.OutputValues[pair.Key]);
        Assert.Equal(expected.Signals, data.SignalsList);
        Assert.Equal(bars.Select(b=>b.Open), data.OpenPrices); Assert.Equal(bars.Select(b=>b.Close), data.ClosePrices);
    }
    [Fact]
    public void CallbackSlotsAndShortReplacementsArePreserved()
    {
        var bars = Enumerable.Range(0, 8).Select(i => new Bar(DateTime.UnixEpoch.AddMinutes(i), i-1, i+2, i-2, i, 1)).ToArray();
        var basis = new[] { 2d, 0, -3, 1 }; var signal = new[] { -2d, 1, 3 };
        var expected = BuiltInFormulaReferences.ValueChartValues(bars, 3, MovingAvgType.SimpleMovingAverage, basisOverride:basis, signalOverride:signal);
        using (ComponentAverage.Arm(new Func<IReadOnlyList<double>,int,IReadOnlyList<double>>[] {
            (values,length) => { Assert.Equal(3,length); Assert.Equal(bars.Select(b=>(b.High+b.Low)/2),values); return basis; },
            (values,length) => { Assert.Equal(3,length); Assert.Equal(expected.Outputs["vClose"],values); return signal; } }))
        {
            var data=Data(bars).CalculateValueChartIndicator(length:3);
            foreach(var pair in expected.Outputs) Assert.Equal(pair.Value,data.OutputValues[pair.Key]);
            Assert.Equal(expected.Signals,data.SignalsList); Assert.Equal(2,ComponentAverage.Requests); Assert.Equal(2,ComponentAverage.Substitutions);
        }
        using (ComponentAverage.Arm(new Func<IReadOnlyList<double>,int,IReadOnlyList<double>>[] { (_,length)=> { Assert.Equal(3,length); return basis; } }))
        {
            using var context=new ComputeContext(); using var output=IndicatorCompute.ComputeValueChartIndicatorFast(Data(bars),context,3);
            Assert.Equal(expected.Outputs["vClose"],output.ToArray()); Assert.Equal(1,ComponentAverage.Requests);
        }
    }
    [Theory]
    [InlineData(int.MaxValue)]
    [InlineData(int.MinValue)]
    public void ExtremePeriodsMatchDirectWindowReference(int length)
    {
        var bars=Enumerable.Range(0,9).Select(i=>new Bar(DateTime.UnixEpoch.AddMinutes(i),i-1,i+2,i-2,i,1)).ToArray();
        var expected=BuiltInFormulaReferences.ValueChartValues(bars,length,MovingAvgType.SimpleMovingAverage);
        var data=Data(bars).CalculateValueChartIndicator(length:length);
        using var state=new ValueChartIndicatorState(length:length);
        for(var i=0;i<bars.Length;i++)
        {
            var b=bars[i];var point=state.Update(new OhlcvBar("VC",BarTimeframe.Minutes(1),b.Time,b.Time,b.Open,b.High,b.Low,b.Close,b.Volume,true),true,true);
            foreach(var pair in expected.Outputs) { Assert.Equal(pair.Value[i],data.OutputValues[pair.Key][i]); Assert.Equal(pair.Value[i],point.Outputs![pair.Key]); }
        }
    }
    [Fact]
    public void InvalidBarCannotAdvanceStateAndZeroRangePublishesZero()
    {
        using var state=new ValueChartIndicatorState(length:1);var time=DateTime.UnixEpoch;
        OhlcvBar Bar(double high)=>new("VC",BarTimeframe.Minutes(1),time,time,0,high,0,1,1,true);
        Assert.ThrowsAny<ArgumentException>(()=>state.Update(Bar(double.NaN),true,true));
        var invalidVolume=new OhlcvBar("VC",BarTimeframe.Minutes(1),time,time,0,1,0,1,double.NaN,true);
        Assert.ThrowsAny<ArgumentException>(()=>state.Update(invalidVolume,true,true));
        Assert.Equal(12.5,state.Update(Bar(1),true,true).Value);
        state.Reset(); var flat=new OhlcvBar("VC",BarTimeframe.Minutes(1),time,time,2,2,2,2,1,true);
        Assert.All(state.Update(flat,true,true).Outputs!.Values,value=>Assert.Equal(0,value));
    }
    [Theory]
    [InlineData(int.MaxValue, 530)]
    [InlineData(11, 3)]
    public void RangePeriodClampsAt530AndFiveRangeHistoryExpires(int length, int rangeLength)
    {
        var bars=Enumerable.Range(0,rangeLength+6).Select(i=>new Bar(DateTime.UnixEpoch.AddMinutes(i),1,i==0?2:1,1,1,1)).ToArray();
        var batch=Data(bars).CalculateValueChartIndicator(length:length);
        // Basis stays zero during the huge SMA warmup. The initial unit range
        // expires from the ceil(length/5), capped-at-530 extrema window, then from five-range history.
        Assert.Equal(25d/Math.Min(5,rangeLength),batch.OutputValues["vClose"][rangeLength-1]);
        Assert.Equal(25d/Math.Min(4,rangeLength),batch.OutputValues["vClose"][rangeLength]);
        Assert.Equal(25,batch.OutputValues["vClose"][rangeLength+3]);
        Assert.Equal(0,batch.OutputValues["vClose"][rangeLength+4]);
        using var state=new ValueChartIndicatorState(length:length);
        for(var i=0;i<bars.Length;i++)
        {
            var b=bars[i];var point=state.Update(new OhlcvBar("VC",BarTimeframe.Minutes(1),b.Time,b.Time,b.Open,b.High,b.Low,b.Close,b.Volume,true),true,false);
            Assert.Equal(batch.OutputValues["vClose"][i],point.Value);Assert.Null(point.Outputs);
        }
    }
}

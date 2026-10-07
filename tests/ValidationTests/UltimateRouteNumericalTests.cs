using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Builder.Compute;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Streaming;
using OoplesFinance.StockIndicators.Validation;
namespace OoplesFinance.StockIndicators.Tests.Unit.ValidationTests;
public sealed class UltimateRouteNumericalTests
{
    public static IEnumerable<object[]> Cases => IndicatorValidationDiscovery.Discover(new[] { typeof(IIndicator).Assembly })
        .Where(c => c.IndicatorType == typeof(UltimateMovingAverage) || c.IndicatorType == typeof(UltimateMovingAverageBands)).Select(c => new object[] { c });
    public static IEnumerable<object[]> Routes => Cases.SelectMany(c => new[] { "batch", "fast", "arm", "native", "streaming" }.Select(route => new object[] { c[0], route }));
    [Theory, MemberData(nameof(Routes))]
    public void EveryRouteMatchesIndependentPowerAndBandBounds(IndicatorValidationCase c, string route) => new OrdinalFamilyNumericalTests().CheckRoutes(c, route,
        bars => BuiltInFormulaReferences.UltimateOutputs(bars, (IBuiltInIndicator)c.Factory()), BuiltInFormulaReferences.UltimateBudget);
    [Theory, MemberData(nameof(Cases))]
    public void EveryOutputRejectsInjectedFaults(IndicatorValidationCase c) => new OrdinalFamilyNumericalTests().EveryPublishedOutputRejectsAnInjectedValueFault(c);
    [Theory, MemberData(nameof(Cases))]
    public Task NumericalClassesAreEnrolled(IndicatorValidationCase c) => new OrdinalFamilyNumericalTests().PublicConfigurationsPassEveryNumericalClass(c);
    private static StockData Data(Bar[] bars) => new(bars.Select(b => b.Open), bars.Select(b => b.High), bars.Select(b => b.Low), bars.Select(b => b.Close), bars.Select(b => b.Volume), bars.Select(b => b.Time));
    [Theory, MemberData(nameof(Cases))]
    public async Task SelectedInputPreservesOriginalCandleFlow(IndicatorValidationCase c)
    {
        await new OrdinalFamilyNumericalTests().SelectedSourcePreservesTheFormulaAndOriginalCandleFields(c);
        var indicator = (IBuiltInIndicator)c.Factory(); var options = indicator.CreateOptions();
        var bands = indicator.BatchName == IndicatorName.UltimateMovingAverageBands;
        var minimum = bands ? (int)options.GetType().GetProperty("MinLength")!.GetValue(options)! : 5;
        var maximum = bands ? (int)options.GetType().GetProperty("MaxLength")!.GetValue(options)! : 50;
        var multiplier = bands ? (double)options.GetType().GetProperty("StdDevMult")!.GetValue(options)! : 0;
        var kind = (MovingAvgType)options.GetType().GetProperty("MaType")!.GetValue(options)!;
        var bars = Enumerable.Range(0, 24).Select(i => new Bar(DateTime.UnixEpoch.AddMinutes(i), 100+i, 103+i, 97+i, 100+i, 1+i%4)).ToArray();
        var selected = bars.Select((_, i) => (double)(i*7%13-6)).ToArray();
        var projected = bars.Select((b,i) => new Bar(b.Time,b.Open,b.High,b.Low,selected[i],b.Volume)).ToArray();
        var expected = BuiltInFormulaReferences.UltimateOutputs(projected, indicator);
        var data = Data(bars); data.SetCustomValues(selected.ToList()); using var context = new ComputeContext();
        if (bands)
        {
            foreach (var pair in new[] { (IndicatorCompute.ChannelBand.Upper,"UpperBand"), (IndicatorCompute.ChannelBand.Middle,"MiddleBand"), (IndicatorCompute.ChannelBand.Lower,"LowerBand") })
            {
                using var actual = IndicatorCompute.ComputeUltimateMovingAverageBandsFast(data,context,minimum,maximum,multiplier,kind,pair.Item1);
                for(var i=0;i<selected.Length;i++) Assert.True(BuiltInFormulaReferences.UltimateBudget.Accepts(expected[pair.Item2][i],actual.Span[i]));
            }
            Assert.Equal(selected,data.ChainedValues); data.CalculateUltimateMovingAverageBands(kind,minimum,maximum,multiplier);
        }
        else
        {
            using var actual = IndicatorCompute.ComputeUltimateMovingAverageFast(data,context,minimum,maximum,1,kind);
            for(var i=0;i<selected.Length;i++) Assert.True(BuiltInFormulaReferences.UltimateBudget.Accepts(expected["Uma"][i],actual.Span[i]));
            Assert.Equal(selected,data.ChainedValues); data.CalculateUltimateMovingAverage(kind,minimum,maximum);
        }
        foreach(var key in expected.Keys) for(var i=0;i<selected.Length;i++) Assert.True(BuiltInFormulaReferences.UltimateBudget.Accepts(expected[key][i],data.OutputValues[key][i]));
        Assert.Equal(bars.Select(b=>b.Close),data.ClosePrices); Assert.Equal(bars.Select(b=>b.High),data.HighPrices); Assert.Equal(bars.Select(b=>b.Volume),data.Volumes);
    }

    [Fact]
    public void CallbackOrderShortMeansAndSelectedPricesArePreserved()
    {
        var prices = new[] { -1d, -1, 1, 1, -1 }; var means = new[] { 0d, 0, 0, -1 };
        var bars = prices.Select((_,i) => new Bar(DateTime.UnixEpoch.AddMinutes(i),9,9,9,9,0)).ToArray();
        var projected = bars.Select((b,i) => new Bar(b.Time,b.Open,b.High,b.Low,prices[i],b.Volume)).ToArray();
        foreach (var mode in new[] { "UmaBatch", "UmaFast", "BandsBatch", "MiddleBand", "UpperBand", "LowerBand" })
        {
            var bands = !mode.StartsWith("Uma",StringComparison.Ordinal);
            var expected = BuiltInFormulaReferences.UltimateValues(projected,2,4,MovingAvgType.SimpleMovingAverage,2,bands,means);
            var data = Data(bars); data.SetCustomValues(prices.ToList());
            using (ComponentAverage.Arm(new Func<IReadOnlyList<double>,int,IReadOnlyList<double>>[] {
                (values,length) => { Assert.Equal(prices,values); Assert.Equal(4,length); return means; } }))
            {
                if (mode == "UmaBatch")
                {
                    data.CalculateUltimateMovingAverage(minLength:2,maxLength:4);
                    Assert.Equal(new[] { Signal.StrongSell,Signal.Sell,Signal.StrongBuy,Signal.Buy,Signal.StrongSell },data.SignalsList);
                }
                else if (mode == "BandsBatch") data.CalculateUltimateMovingAverageBands(minLength:2,maxLength:4);
                else
                {
                    using var context = new ComputeContext();
                    using var output = mode == "UmaFast" ? IndicatorCompute.ComputeUltimateMovingAverageFast(data,context,2,4)
                        : IndicatorCompute.ComputeUltimateMovingAverageBandsFast(data,context,2,4,2,band:mode=="UpperBand" ? IndicatorCompute.ChannelBand.Upper : mode=="LowerBand" ? IndicatorCompute.ChannelBand.Lower : IndicatorCompute.ChannelBand.Middle);
                    var key = mode == "UmaFast" ? "Uma" : mode;
                    for(var i=0;i<prices.Length;i++) Assert.True(BuiltInFormulaReferences.UltimateBudget.Accepts(expected[key][i],output.Span[i]));
                    Assert.Equal(prices,data.ChainedValues);
                }
                if (mode.EndsWith("Batch",StringComparison.Ordinal)) foreach(var key in expected.Keys)
                    for(var i=0;i<prices.Length;i++) Assert.True(BuiltInFormulaReferences.UltimateBudget.Accepts(expected[key][i],data.OutputValues[key][i]));
                Assert.Equal(1,ComponentAverage.Requests); Assert.Equal(1,ComponentAverage.Substitutions);
                Assert.All(data.ClosePrices,x=>Assert.Equal(9,x));
            }
        }
        using (ComponentAverage.Arm(Array.Empty<Func<IReadOnlyList<double>,int,IReadOnlyList<double>>>()))
        { Data(bars).CalculateUltimateMovingAverageBands(); Assert.Equal(1,ComponentAverage.Requests); }
    }

    [Fact]
    public void HugeFractionalPeriodMatchesIndependentEulerMaclaurinSum()
    {
        var prices = new[] { 1d,3,2,4 }; var volumes = new[] { 1d,2,3,4 };
        var bars = prices.Select((p,i)=>new Bar(DateTime.UnixEpoch.AddMinutes(i),p,p,p,p,volumes[i])).ToArray();
        var expected = BuiltInFormulaReferences.UltimateValues(bars,int.MaxValue,int.MaxValue,MovingAvgType.SimpleMovingAverage,2,true);
        var batch = Data(bars).CalculateUltimateMovingAverageBands(minLength:int.MaxValue,maxLength:int.MaxValue);
        using var state = new UltimateMovingAverageBandsState(minLength:int.MaxValue,maxLength:int.MaxValue);
        for(var i=0;i<bars.Length;i++)
        {
            var b=bars[i]; var native=state.Update(new OhlcvBar("UMA",BarTimeframe.Minutes(1),b.Time,b.Time,b.Open,b.High,b.Low,b.Close,b.Volume,true),true,true);
            foreach(var key in expected.Keys)
            {
                Assert.True(BuiltInFormulaReferences.UltimateBudget.Accepts(expected[key][i],batch.OutputValues[key][i]));
                Assert.True(BuiltInFormulaReferences.UltimateBudget.Accepts(expected[key][i],native.Outputs![key]));
            }
        }
    }
}

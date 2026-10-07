using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Builder.Compute;
using OoplesFinance.StockIndicators.Builder.Specs;
using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Streaming;
using OoplesFinance.StockIndicators.Validation;

namespace OoplesFinance.StockIndicators.Tests.Unit.ValidationTests;

public sealed class KeltnerNumericalTests
{
    private static Bar[] BarsOf(double[] prices) => prices.Select((p, i) => new Bar(DateTime.UnixEpoch.AddMinutes(i), p, p, p, p, i % 5 == 0 ? 0 : i % 5 == 1 ? double.MaxValue : i % 5 == 2 ? double.Epsilon : 7)).ToArray();
    private static StockData Data(Bar[] bars) => new(bars.Select(b => b.Open), bars.Select(b => b.High), bars.Select(b => b.Low), bars.Select(b => b.Close), bars.Select(b => b.Volume), bars.Select(b => b.Time));
    private static OhlcvBar Native(Bar b) => new("KC", BarTimeframe.Minutes(1), b.Time, b.Time, b.Open, b.High, b.Low, b.Close, b.Volume, true);
    [Fact]
    public void MiddleAliasExposesOnlyItsConfiguredAverage()
    {
        var bars = BarsOf(new[] { 1d, 3, 2, -4, 0, 0, 0 });
        var indicator = new KeltnerChannelMiddle(3, new Sma()); var builtIn = (IBuiltInIndicator)indicator;
        var state = StatefulIndicatorFactory.Create(new IndicatorSpec(builtIn.BatchName, builtIn.CreateOptions(), builtIn.BatchOutputKey));
        Assert.NotNull(state); using var lifetime = state as IDisposable;
        var expected = new[] { 0d, 0, 2, 1d / 3, -2d / 3, -4d / 3, 0 };
        for (var i = 0; i < bars.Length; i++) { var output = state.Update(Native(bars[i]), true, true); Assert.Single(output.Outputs!); Assert.Equal(expected[i], output.Outputs!["MiddleBand"]); }
    }
    [Fact]
    public void BandsAndWidthsPreserveFiniteResultsAcrossExtremeRanges()
    {
        foreach (var length in new[] { 1, 2, 7 })
        foreach (var rangeLength in new[] { 1, 3 })
        foreach (var kind in new[] { MovingAvgType.SimpleMovingAverage, MovingAvgType.WeightedMovingAverage, MovingAvgType.ExponentialMovingAverage, MovingAvgType.WildersSmoothingMethod })
        foreach (var multiplier in new[] { 0d, .5, 2, double.MaxValue })
        foreach (var scale in new[] { 1d, double.Epsilon, double.MaxValue / 16 })
        {
            var bars = Enumerable.Range(0, 12).Select(i => new Bar(DateTime.UnixEpoch.AddMinutes(i), (i % 7 - 3) * scale, (i % 5 + 2) * scale, -(i % 3 + 1) * scale, (i % 9 - 4) * scale, 1)).ToArray();
            Check(bars, kind, length, rangeLength, multiplier);
        }
        Check(Array.Empty<Bar>(), MovingAvgType.ExponentialMovingAverage, 2, 2, 2);
        var extremes = Enumerable.Range(0, 8).Select(i => new Bar(DateTime.UnixEpoch.AddMinutes(i), double.MaxValue, double.MaxValue, -double.MaxValue, double.MaxValue, 1)).ToArray();
        Check(extremes, MovingAvgType.ExponentialMovingAverage, 2, 2, 2);
        Assert.Equal(400, Data(extremes).CalculateKeltnerChannelWidth(length: 2).CustomValuesList[0]);
        Assert.Equal(80, Data(new[] { new Bar(DateTime.UnixEpoch, 10, 11, 9, 10, 1) }).CalculateKeltnerChannelWidth(length: 1).CustomValuesList[0]);
    }
    private static void Check(Bar[] bars, MovingAvgType kind, int length, int rangeLength, double multiplier)
    {
        var code = kind == MovingAvgType.SimpleMovingAverage ? 1 : kind == MovingAvgType.WeightedMovingAverage ? 2 : kind == MovingAvgType.ExponentialMovingAverage ? 3 : 6;
        var expected = BuiltInFormulaReferences.KeltnerOutputs(bars, length, rangeLength, code, multiplier);
        var batch = Data(bars).CalculateKeltnerChannels(kind, length, rangeLength, multiplier);
        using var context = new ComputeContext();
        foreach (var key in new[] { "UpperBand", "MiddleBand", "LowerBand" })
        {
            Assert.Equal(expected[key], batch.OutputValues[key]);
            using var raw = IndicatorCompute.TryComputeFast(Data(bars), new IndicatorSpec(IndicatorName.KeltnerChannels, new KeltnerChannelsSpecOptions(length, rangeLength, multiplier, kind), key), context);
            Assert.NotNull(raw); Assert.Equal(expected[key], raw.Value.ToArray());
        }
        using var state = new KeltnerChannelsState(kind, length, rangeLength, multiplier);
        for (var replay = 0; replay < 2; replay++)
        {
            state.Reset();
            for (var i = 0; i < bars.Length; i++)
            foreach (var final in new[] { false, false, true }) { var actual = state.Update(Native(bars[i]), final, true); foreach (var key in new[] { "UpperBand", "MiddleBand", "LowerBand" }) Assert.Equal(expected[key][i], actual.Outputs![key]); }
        }
        if (code != 3 || rangeLength != length) return;
        var width = expected["Kcw"]; Assert.Equal(width, Data(bars).CalculateKeltnerChannelWidth(length, multiplier).CustomValuesList);
        using var widthRaw = IndicatorCompute.ComputeKeltnerChannelWidthFast(Data(bars), context, length, multiplier); Assert.Equal(width, widthRaw.Span.ToArray());
        var core = new double[bars.Length]; OoplesFinance.StockIndicators.Core.VolatilityCore.KeltnerChannelWidth(bars.Select(b => b.High).ToArray(), bars.Select(b => b.Low).ToArray(), bars.Select(b => b.Close).ToArray(), core, length, multiplier); Assert.Equal(width, core);
        using var widthState = new KeltnerChannelWidthState(length, multiplier);
        for (var replay = 0; replay < 2; replay++) { widthState.Reset(); for (var i = 0; i < bars.Length; i++) foreach (var final in new[] { false, false, true }) Assert.Equal(width[i], widthState.Update(Native(bars[i]), final, true).Value); }
    }
    [Fact]
    public void SelectedPricesAndCustomerAtrThenCenterKeepTheirOrder()
    {
        var bars = Enumerable.Range(1, 3).Select(i => new Bar(DateTime.UnixEpoch.AddMinutes(i), i, 5, 0, i, 1)).ToArray();
        var prices = new[] { 4d, 1, 2 }; var data = Data(bars); data.SetCustomValues(prices.ToList());
        var expected = BuiltInFormulaReferences.KeltnerOutputs(bars.Select((b, i) => new Bar(b.Time, b.Open, b.High, b.Low, prices[i], b.Volume)).ToArray(), 2, 2, 3, 2);
        using var context = new ComputeContext(); using var raw = IndicatorCompute.ComputeKeltnerChannelWidthFast(data, context, 2); Assert.Equal(expected["Kcw"], raw.Span.ToArray());
        foreach (var batch in new[] { false, true })
        {
            using var armed = ComponentAverage.Arm(new Func<IReadOnlyList<double>, int, IReadOnlyList<double>>[] {
                (values, period) => { Assert.Equal(3, period); Assert.Equal(new[] { 5d, 5, 5 }, values); return new[] { 2d, 2, 2 }; },
                (values, period) => { Assert.Equal(2, period); Assert.Equal(new[] { 1d, 2, 3 }, values); return new[] { 3d, 3, 3 }; } });
            if (batch) Assert.Equal(new[] { 7d, 7, 7 }, Data(bars).CalculateKeltnerChannels(length1: 2, length2: 3).OutputValues["UpperBand"]);
            else { using var result = IndicatorCompute.TryComputeFast(Data(bars), new IndicatorSpec(IndicatorName.KeltnerChannels, new KeltnerChannelsSpecOptions(2, 3), "UpperBand"), context); Assert.NotNull(result); Assert.Equal(new[] { 7d, 7, 7 }, result.Value.ToArray()); }
            Assert.Equal(2, ComponentAverage.Substitutions);
        }
    }
    [Fact]
    public void BatchOnlyAveragesRemainAvailableForBothChannelStages()
    {
        var bars = Enumerable.Range(0, 12).Select(i => new Bar(DateTime.UnixEpoch.AddMinutes(i), 10 + i, 13 + i, 8 + i, 11 + i % 5, 1)).ToArray();
        foreach (var centerKind in new[] { MovingAvgType.HullMovingAverage, MovingAvgType.ExponentialMovingAverage })
        foreach (var rangeKind in new[] { MovingAvgType.HullMovingAverage, MovingAvgType.WildersSmoothingMethod })
        {
            var middle = CalculationsHelper.GetMovingAverageList(Data(bars), centerKind, 4, bars.Select(b => b.Close).ToList());
            var ranges = bars.Select((b, i) => Math.Max(b.High - b.Low, Math.Max(Math.Abs(b.High - bars[i == 0 ? 0 : i - 1].Close), Math.Abs(b.Low - bars[i == 0 ? 0 : i - 1].Close)))).ToList();
            var atr = CalculationsHelper.GetMovingAverageList(Data(bars), rangeKind, 3, ranges);
            var expected = middle.Select((v, i) => v + 2 * atr[i]).ToArray();
            var actual = Data(bars).CalculateKeltnerChannels(centerKind, 4, 3, 2, rangeKind);
            for (var i = 0; i < bars.Length; i++) Assert.Equal(expected[i], actual.OutputValues["UpperBand"][i], 12);
            if (rangeKind != MovingAvgType.WildersSmoothingMethod) continue;
            using var context = new ComputeContext();
            using var raw = IndicatorCompute.TryComputeFast(Data(bars), new IndicatorSpec(IndicatorName.KeltnerChannels, new KeltnerChannelsSpecOptions(4, 3, 2, centerKind), "UpperBand"), context);
            Assert.NotNull(raw);
            for (var i = 0; i < bars.Length; i++) Assert.Equal(expected[i], raw.Value.Span[i], 12);
        }
    }
    [Fact]
    public void InvalidFieldsNeverAdvanceState()
    {
        foreach (var width in new[] { false, true })
        foreach (var field in Enumerable.Range(0, 5))
        foreach (var invalid in new[] { double.NaN, double.PositiveInfinity, double.NegativeInfinity })
        foreach (var final in new[] { false, true })
        {
            IStreamingIndicatorState state = width ? new KeltnerChannelWidthState(2) : new KeltnerChannelsState(length1: 2, length2: 3);
            IStreamingIndicatorState control = width ? new KeltnerChannelWidthState(2) : new KeltnerChannelsState(length1: 2, length2: 3);
            using var lifetime = state as IDisposable; using var controlLifetime = control as IDisposable;
            foreach (var bar in BarsOf(new[] { 1d, 3, 2 })) { state.Update(Native(bar), true, true); control.Update(Native(bar), true, true); }
            var values = new[] { 2d, 4, 1, 2, 1 }; values[field] = invalid;
            var bad = new OhlcvBar("KC", BarTimeframe.Minutes(1), DateTime.UnixEpoch, DateTime.UnixEpoch, values[0], values[1], values[2], values[3], values[4], true);
            Assert.Throws<ArgumentOutOfRangeException>(() => state.Update(bad, final, true));
            var good = Native(BarsOf(new[] { 4d })[0]); Assert.Equal(control.Update(good, true, true).Value, state.Update(good, true, true).Value);
        }
    }
    public static IEnumerable<object[]> Cases => IndicatorValidationDiscovery.Discover(new[] { typeof(IIndicator).Assembly })
        .Where(c => c.IndicatorType == typeof(KeltnerChannels) || c.IndicatorType == typeof(KeltnerChannelWidth)).Select(c => new object[] { c });
    public static IEnumerable<object[]> Routes => Cases.SelectMany(c => new[] { "batch", "fast", "arm", "native", "streaming" }.Select(route => new object[] { c[0], route }));
    [Theory, MemberData(nameof(Routes))]
    public void AllRoutesMatchIndependentFormulasIncludingPreviewAndReset(IndicatorValidationCase testCase, string route) =>
        new OrdinalFamilyNumericalTests().CheckRoutes(testCase, route, bars => BuiltInFormulaReferences.KeltnerOutputs(bars, (IBuiltInIndicator)testCase.Factory()), IndicatorErrorBudget.Exact);
    [Theory, MemberData(nameof(Cases))]
    public Task SelectedSourcePreservesTheFormulaAndOriginalCandleFields(IndicatorValidationCase testCase) =>
        new OrdinalFamilyNumericalTests().SelectedSourcePreservesTheFormulaAndOriginalCandleFields(testCase);
    [Theory, MemberData(nameof(Cases))]
    public void EveryPublishedOutputRejectsAnInjectedValueFault(IndicatorValidationCase testCase) =>
        new OrdinalFamilyNumericalTests().EveryPublishedOutputRejectsAnInjectedValueFault(testCase);
    [Theory, MemberData(nameof(Cases))]
    public Task PublicConfigurationsPassEveryNumericalClass(IndicatorValidationCase testCase) =>
        new OrdinalFamilyNumericalTests().PublicConfigurationsPassEveryNumericalClass(testCase);
}

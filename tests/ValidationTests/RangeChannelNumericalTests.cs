using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Builder.Compute;
using OoplesFinance.StockIndicators.Builder.Specs;
using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Streaming;
using OoplesFinance.StockIndicators.Validation;
namespace OoplesFinance.StockIndicators.Tests.Unit.ValidationTests;
public sealed class RangeChannelNumericalTests
{
    private static StockData Data(Bar[] bars) => new(bars.Select(b => b.Open), bars.Select(b => b.High), bars.Select(b => b.Low), bars.Select(b => b.Close), bars.Select(b => b.Volume), bars.Select(b => b.Time));
    private static OhlcvBar Native(Bar b) => new("RC", BarTimeframe.Minutes(1), b.Time, b.Time, b.Open, b.High, b.Low, b.Close, b.Volume, true);
    [Fact]
    public void RoundedPriceChannelsAndSmoothCenterChannelsKeepDistinctContracts()
    {
        foreach (var length in new[] { 1, 2, 7 })
        foreach (var kind in new[] { MovingAvgType.SimpleMovingAverage, MovingAvgType.WeightedMovingAverage, MovingAvgType.ExponentialMovingAverage, MovingAvgType.WildersSmoothingMethod })
        foreach (var multiplier in new[] { 0d, .5, 2.5, double.MaxValue })
        foreach (var scale in new[] { 1d, double.Epsilon, double.MaxValue / 16 })
            Check(Enumerable.Range(0, 12).Select(i => new Bar(DateTime.UnixEpoch.AddMinutes(i), (i % 7 - 3) * scale, (i % 5 + 2) * scale, -(i % 3 + 1) * scale, (i % 9 - 4) * scale, 1)).ToArray(), length, kind, multiplier);
        Check(Array.Empty<Bar>(), 2, MovingAvgType.SimpleMovingAverage, 2);
        var extremes = Enumerable.Range(0, 8).Select(i => new Bar(DateTime.UnixEpoch.AddMinutes(i), double.MaxValue, double.MaxValue, -double.MaxValue, double.MaxValue, 1)).ToArray();
        Check(extremes, 2, MovingAvgType.ExponentialMovingAverage, 2);
        var ties = new[] { new Bar(DateTime.UnixEpoch, 1, 2, 1, 1, 1) };
        var rounded = Data(ties).CalculateAverageTrueRangeChannel(length: 1, mult: .5);
        Assert.Equal(2, rounded.OutputValues["UpperBand"][0]); Assert.Equal(0, rounded.OutputValues["LowerBand"][0]); Assert.Equal(1, rounded.OutputValues["MiddleBand"][0]);
        var unrounded = Data(ties).CalculateStollerAverageRangeChannels(length: 1, atrMult: .5);
        Assert.Equal(1.5, unrounded.OutputValues["UpperBand"][0]); Assert.Equal(.5, unrounded.OutputValues["LowerBand"][0]);
    }
    private static void Check(Bar[] bars, int length, MovingAvgType kind, double multiplier)
    {
        var code = kind == MovingAvgType.SimpleMovingAverage ? 1 : kind == MovingAvgType.WeightedMovingAverage ? 2 : kind == MovingAvgType.ExponentialMovingAverage ? 3 : 6;
        foreach (var rounded in new[] { false, true })
        {
            var expected = BuiltInFormulaReferences.RangeChannelOutputs(bars, length, code, multiplier, rounded);
            var batch = rounded ? Data(bars).CalculateAverageTrueRangeChannel(kind, length, multiplier) : Data(bars).CalculateStollerAverageRangeChannels(kind, length, multiplier);
            using var context = new ComputeContext();
            foreach (var key in expected.Keys)
            {
                Assert.Equal(expected[key], batch.OutputValues[key]);
                var spec = rounded ? new IndicatorSpec(IndicatorName.AverageTrueRangeChannel, new AverageTrueRangeChannelSpecOptions(length, multiplier, kind), key) : new IndicatorSpec(IndicatorName.StollerAverageRangeChannels, new StollerAverageRangeChannelsSpecOptions(length, multiplier, kind), key);
                using var raw = IndicatorCompute.TryComputeFast(Data(bars), spec, context); Assert.NotNull(raw); Assert.Equal(expected[key], raw.Value.ToArray());
            }
            IStreamingIndicatorState state = rounded ? new AverageTrueRangeChannelState(kind, length, multiplier) : new StollerAverageRangeChannelsState(kind, length, multiplier);
            using var lifetime = state as IDisposable;
            for (var replay = 0; replay < 2; replay++)
            {
                state.Reset();
                for (var i = 0; i < bars.Length; i++) foreach (var final in new[] { false, false, true })
                {
                    var output = state.Update(Native(bars[i]), final, true); foreach (var key in expected.Keys) Assert.Equal(expected[key][i], output.Outputs![key]);
                }
            }
        }
    }
    [Fact]
    public void CustomerStagesAndSelectedPricesRetainTheirOrder()
    {
        var highs = new[] { 10d, 5, 7, 4 }; var lows = new[] { -2d, -2, 0, 0 }; var prices = new[] { 9d, -1, 6, 2 };
        var bars = Enumerable.Range(0, 4).Select(i => new Bar(DateTime.UnixEpoch.AddMinutes(i), 4, highs[i], lows[i], 4, 1)).ToArray();
        using var context = new ComputeContext();
        foreach (var rounded in new[] { false, true })
        {
            var expected = BuiltInFormulaReferences.RangeChannelOutputs(bars.Select((b, i) => new Bar(b.Time, b.Open, b.High, b.Low, prices[i], b.Volume)).ToArray(), 2, 1, 2, rounded);
            var data = Data(bars); data.SetCustomValues(prices.ToList());
            var spec = rounded ? new IndicatorSpec(IndicatorName.AverageTrueRangeChannel, new AverageTrueRangeChannelSpecOptions(2, 2), "UpperBand") : new IndicatorSpec(IndicatorName.StollerAverageRangeChannels, new StollerAverageRangeChannelsSpecOptions(2, 2), "UpperBand");
            using var raw = IndicatorCompute.TryComputeFast(data, spec, context); Assert.NotNull(raw); Assert.Equal(expected["UpperBand"], raw.Value.ToArray());
            foreach (var batch in new[] { false, true })
            {
                Func<IReadOnlyList<double>, int, IReadOnlyList<double>> center = (values, period) => { Assert.Equal(2, period); Assert.Equal(prices, values); return new[] { 3d, 3, 3, 3 }; };
                Func<IReadOnlyList<double>, int, IReadOnlyList<double>> atr = (values, period) => { Assert.Equal(2, period); Assert.Equal(new[] { 12d, 11, 8, 6 }, values); return new[] { 2d, 2, 2, 2 }; };
                using var armed = ComponentAverage.Arm(rounded ? new[] { atr, center } : new[] { center, atr });
                var selected = Data(bars); selected.SetCustomValues(prices.ToList());
                var target = rounded ? prices.Select(p => p + 4).ToArray() : new[] { 7d, 7, 7, 7 };
                if (batch) Assert.Equal(target, (rounded ? selected.CalculateAverageTrueRangeChannel(length: 2, mult: 2) : selected.CalculateStollerAverageRangeChannels(length: 2)).OutputValues["UpperBand"]);
                else { using var output = IndicatorCompute.TryComputeFast(selected, spec, context); Assert.NotNull(output); Assert.Equal(target, output.Value.ToArray()); }
                Assert.Equal(2, ComponentAverage.Substitutions);
            }
        }
    }
    [Fact]
    public void LegacyBatchOnlyAveragesRemainAvailable()
    {
        var bars = Enumerable.Range(0, 12).Select(i => new Bar(DateTime.UnixEpoch.AddMinutes(i), 10 + i, 13 + i, 8 + i, 11 + i % 5, 1)).ToArray();
        var kind = MovingAvgType.HullMovingAverage; var prices = bars.Select(b => b.Close).ToList();
        var center = CalculationsHelper.GetMovingAverageList(Data(bars), kind, 4, prices);
        var ranges = bars.Select((b, i) => Math.Max(b.High - b.Low, Math.Max(Math.Abs(b.High - bars[i == 0 ? 0 : i - 1].Close), Math.Abs(b.Low - bars[i == 0 ? 0 : i - 1].Close)))).ToList();
        var atr = CalculationsHelper.GetMovingAverageList(Data(bars), kind, 4, ranges);
        using var context = new ComputeContext();
        foreach (var rounded in new[] { false, true })
        {
            var batch = rounded ? Data(bars).CalculateAverageTrueRangeChannel(kind, 4, 2) : Data(bars).CalculateStollerAverageRangeChannels(kind, 4, 2);
            var spec = rounded ? new IndicatorSpec(IndicatorName.AverageTrueRangeChannel, new AverageTrueRangeChannelSpecOptions(4, 2, kind), "UpperBand") : new IndicatorSpec(IndicatorName.StollerAverageRangeChannels, new StollerAverageRangeChannelsSpecOptions(4, 2, kind), "UpperBand");
            using var raw = IndicatorCompute.TryComputeFast(Data(bars), spec, context); Assert.NotNull(raw);
            for (var i = 0; i < bars.Length; i++)
            {
                var expected = rounded ? Math.Round(prices[i] + 2 * atr[i]) : center[i] + 2 * atr[i];
                Assert.Equal(expected, batch.OutputValues["UpperBand"][i], 12); Assert.Equal(expected, raw.Value.Span[i], 12);
            }
        }
    }
    [Fact]
    public void InvalidFieldsNeverAdvanceState()
    {
        foreach (var rounded in new[] { false, true }) foreach (var field in Enumerable.Range(0, 5))
        foreach (var invalid in new[] { double.NaN, double.PositiveInfinity, double.NegativeInfinity }) foreach (var final in new[] { false, true })
        {
            IStreamingIndicatorState state = rounded ? new AverageTrueRangeChannelState(length: 2) : new StollerAverageRangeChannelsState(length: 2);
            IStreamingIndicatorState control = rounded ? new AverageTrueRangeChannelState(length: 2) : new StollerAverageRangeChannelsState(length: 2);
            using var lifetime = state as IDisposable; using var controlLifetime = control as IDisposable;
            var first = new Bar(DateTime.UnixEpoch, 2, 3, 1, 2, 1); state.Update(Native(first), true, true); control.Update(Native(first), true, true);
            var values = new[] { 2d, 4, 1, 2, 1 }; values[field] = invalid;
            var bad = new OhlcvBar("RC", BarTimeframe.Minutes(1), DateTime.UnixEpoch, DateTime.UnixEpoch, values[0], values[1], values[2], values[3], values[4], true);
            Assert.Throws<ArgumentOutOfRangeException>(() => state.Update(bad, final, true));
            Assert.Equal(control.Update(Native(first), true, true).Value, state.Update(Native(first), true, true).Value);
        }
    }
    public static IEnumerable<object[]> Cases => IndicatorValidationDiscovery.Discover(new[] { typeof(IIndicator).Assembly })
        .Where(c => c.IndicatorType == typeof(BollingerBandsFibonacciRatios) || c.IndicatorType == typeof(AverageTrueRangeChannel) || c.IndicatorType == typeof(StollerAverageRangeChannels)).Select(c => new object[] { c });
    public static IEnumerable<object[]> Routes => Cases.SelectMany(c => new[] { "batch", "fast", "arm", "native", "streaming" }.Select(route => new object[] { c[0], route }));
    [Theory, MemberData(nameof(Routes))]
    public void AllRoutesMatchIndependentFormulasIncludingPreviewAndReset(IndicatorValidationCase testCase, string route) =>
        new OrdinalFamilyNumericalTests().CheckRoutes(testCase, route, bars => BuiltInFormulaReferences.RangeChannelOutputs(bars, (IBuiltInIndicator)testCase.Factory()), IndicatorErrorBudget.Exact);
    [Theory, MemberData(nameof(Cases))]
    public Task SelectedSourcePreservesTheFormulaAndOriginalCandleFields(IndicatorValidationCase testCase) => new OrdinalFamilyNumericalTests().SelectedSourcePreservesTheFormulaAndOriginalCandleFields(testCase);
    [Theory, MemberData(nameof(Cases))]
    public void EveryPublishedOutputRejectsAnInjectedValueFault(IndicatorValidationCase testCase) => new OrdinalFamilyNumericalTests().EveryPublishedOutputRejectsAnInjectedValueFault(testCase);
    [Theory, MemberData(nameof(Cases))]
    public Task PublicConfigurationsPassEveryNumericalClass(IndicatorValidationCase testCase) => new OrdinalFamilyNumericalTests().PublicConfigurationsPassEveryNumericalClass(testCase);
}

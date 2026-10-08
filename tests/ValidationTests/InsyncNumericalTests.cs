using OoplesFinance.StockIndicators.Builder.Compute;
using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Streaming;
using OoplesFinance.StockIndicators.Validation;

namespace OoplesFinance.StockIndicators.Tests.Unit.ValidationTests;

public sealed class InsyncNumericalTests
{
    private static StockData Data(Bar[] bars) => new(bars.Select(b => b.Open), bars.Select(b => b.High), bars.Select(b => b.Low), bars.Select(b => b.Close), bars.Select(b => b.Volume), bars.Select(b => b.Time));
    private static Bar[] Prices(IEnumerable<double> values) => values.Select((v, i) => new Bar(DateTime.UnixEpoch.AddMinutes(i), v, v, v, v, 1)).ToArray();
    private static OhlcvBar Native(Bar b) => new("INSYNC", BarTimeframe.Minutes(1), b.Time, b.Time, b.Open, b.High, b.Low, b.Close, b.Volume, true);
    public static IEnumerable<object[]> Cases => IndicatorValidationDiscovery.Discover(new[] { typeof(IIndicator).Assembly }).Where(c => c.IndicatorType == typeof(InsyncIndex)).Select(c => new object[] { c });
    public static IEnumerable<object[]> Routes => Cases.SelectMany(c => new[] { "batch", "fast", "arm", "native", "streaming" }.Select(route => new object[] { c[0], route }));
    [Theory, MemberData(nameof(Routes))]
    public void EveryRouteMatchesIndependentComponentVotes(IndicatorValidationCase c, string route) => new OrdinalFamilyNumericalTests().CheckRoutes(c, route, bars => BuiltInFormulaReferences.InsyncOutputs(bars, (IBuiltInIndicator)c.Factory()), IndicatorErrorBudget.Exact);
    [Theory, MemberData(nameof(Cases))]
    public Task SelectedSourcePreservesCandles(IndicatorValidationCase c) => new OrdinalFamilyNumericalTests().SelectedSourcePreservesTheFormulaAndOriginalCandleFields(c);
    [Theory, MemberData(nameof(Cases))]
    public void EveryOutputRejectsInjectedFaults(IndicatorValidationCase c) => new OrdinalFamilyNumericalTests().EveryPublishedOutputRejectsAnInjectedValueFault(c);
    [Theory, MemberData(nameof(Cases))]
    public Task NumericalClassesAreEnrolled(IndicatorValidationCase c) => new OrdinalFamilyNumericalTests().PublicConfigurationsPassEveryNumericalClass(c);

    private static InsyncIndex Indicator(int period, double mult = 2, double divisor = 10000, int? slowLength = null) => new(
        fastLength: period, slowLength: slowLength ?? (period == int.MaxValue ? period : period + 1), mfiLength: period,
        bbLength: period, cciLength: period, dpoLength: period, rocLength: period, rsiLength: period,
        stochLength: period, stochKLength: period, stochDLength: period, smaLength: period, stdDevMult: mult, divisor: divisor);
    private static double[] Check(Bar[] bars, int period = 3, double mult = 2, double divisor = 10000, double[]? selected = null, int? slowLength = null)
    {
        var slow = slowLength ?? (period == int.MaxValue ? period : period + 1);
        var expected = BuiltInFormulaReferences.InsyncValues(bars, Indicator(period, mult, divisor, slow), selected);
        var data = Data(bars); if (selected is not null) data.SetCustomValues(selected.ToList());
        var batch = data.CalculateInsyncIndex(fastLength: period, slowLength: slow, mfiLength: period,
            bbLength: period, cciLength: period, dpoLength: period, rocLength: period, rsiLength: period,
            stochLength: period, stochKLength: period, stochDLength: period, smaLength: period, stdDevMult: mult, divisor: divisor);
        Assert.Equal(expected.Line, batch.ChainedValues); Assert.Equal(expected.Line, batch.OutputValues["Iidx"]);
        var trades = expected.Line.Select((v, i) =>
        {
            var previous = i > 0 ? expected.Line[i - 1] : 0; var before = i > 1 ? expected.Line[i - 2] : 0;
            var slope = v - previous; var priorSlope = previous - before;
            return slope > 0 && slope > priorSlope ? Signal.StrongBuy : slope < 0 && slope < priorSlope ? Signal.StrongSell
                : slope > 0 || previous < 5 && v > 5 ? Signal.Buy : slope < 0 || previous > 95 && v < 95 ? Signal.Sell : Signal.None;
        }).ToArray();
        Assert.Equal(trades, batch.SignalsList);
        using var context = new ComputeContext(); var raw = Data(bars); if (selected is not null) raw.SetCustomValues(selected.ToList());
        using var fast = IndicatorCompute.ComputeInsyncIndexFast(raw, context, period, slow, period, period, period, period, period, period, period, period, period, period, mult, divisor);
        Assert.Equal(expected.Line, fast.ToArray());
        if (selected is not null) Assert.Equal(selected, raw.ChainedValues);
        using var native = new InsyncIndexState(fastLength: period, slowLength: slow, mfiLength: period,
            bbLength: period, cciLength: period, dpoLength: period, rocLength: period, rsiLength: period,
            stochLength: period, stochKLength: period, stochDLength: period, smaLength: period, stdDevMult: mult, divisor: divisor);
        if (selected is not null) ((ICustomInputConsumer)native).ReadCloseAsInput();
        for (var pass = 0; pass < 2; pass++)
        {
            native.Update(Native(Prices(new[] { 7d })[0]), true, false); native.Reset();
            for (var i = 0; i < bars.Length; i++)
            {
                native.Update(Native(Prices(new[] { -9d })[0]), false, false);
                var bar = bars[i]; var candle = Native(bar);
                if (selected is not null) candle = new("INSYNC", BarTimeframe.Minutes(1), bar.Time, bar.Time, bar.Open, bar.High, bar.Low, selected[i], bar.Volume, true);
                foreach (var final in new[] { false, false, true })
                {
                    var point = native.Update(candle, final, true); Assert.Equal(expected.Line[i], point.Value); Assert.Equal(point.Value, point.Outputs!["Iidx"]);
                }
            }
        }
        Assert.All(expected.Line, value => { Assert.InRange(value, -5, 105); Assert.Equal(0, value % 5); });
        return expected.Line;
    }

    [Fact]
    public void HandComponentsAndDelayedEqualityVotesRemainExplicit()
    {
        Assert.Equal(new[] { 45d, 40, 40, 45 }, Check(Prices(Enumerable.Repeat(100d, 4)), 1));
        var bars = new[] { new Bar(DateTime.UnixEpoch, 3, 4, 2, 3, 2), new Bar(DateTime.UnixEpoch.AddMinutes(1), 6, 8, 4, 6, 2) };
        var reference = BuiltInFormulaReferences.InsyncValues(bars, Indicator(1, divisor: 1));
        Assert.Equal(new[] { 0d, 6 }, reference.Components["Emv"].Select(v => v.ToDouble()));
        Assert.Equal(new[] { 0d, 1.5 }, reference.Components["Macd"].Select(v => v.ToDouble()));
        Assert.Equal(new[] { -3d, -6 }, reference.Components["Dpo"].Select(v => v.ToDouble()));
        Assert.Equal(new[] { 0d, 100 }, reference.Components["Roc"].Select(v => v.ToDouble()));
        Assert.Equal(new[] { 0d, -5 }, reference.Votes["DpoSell"]);
        Check(bars, 1, divisor: 1);
    }
    [Fact]
    public void OneUlpThresholdsAndExtendedDirectionsAreNotTies()
    {
        foreach (var bounds in new[] { (-100d, 100d), (5d, 95d), (20d, 80d), (30d, 70d) })
        {
            Assert.Equal(0, InsyncVotes.Band(bounds.Item1, bounds.Item1, bounds.Item2));
            Assert.Equal(0, InsyncVotes.Band(bounds.Item2, bounds.Item1, bounds.Item2));
            Assert.Equal(-5, InsyncVotes.Band(Math.BitDecrement(bounds.Item1), bounds.Item1, bounds.Item2));
            Assert.Equal(5, InsyncVotes.Band(Math.BitIncrement(bounds.Item2), bounds.Item1, bounds.Item2));
        }
        Assert.Equal(0, InsyncVotes.Direction(Math.BitDecrement(1d), 1));
        Assert.Equal(5, InsyncVotes.Direction(1d, 1));
        Assert.Equal(-5, InsyncVotes.InverseDirection(-1d, -1));
        Assert.Equal(0, InsyncVotes.InverseDirection(Math.BitIncrement(-1d), -1));
        Assert.Equal(5, InsyncVotes.Direction(new RocBankValue(2, 2048), new RocBankValue(1, 2048)));
        Assert.Equal(-5, InsyncVotes.Direction(new RocBankValue(-2, 2048), new RocBankValue(-1, 2048)));
        Assert.Equal(0, InsyncVotes.Direction(new RocBankValue(1, 2048), new RocBankValue(2, 2048)));
        Assert.Equal(5, InsyncVotes.Band(new RocBankValue(1, 2048), 5, 95));
    }
    [Fact]
    public void ExtremeComponentsStillProduceBoundedVotes()
    {
        var values = new[] { double.MaxValue, -double.MaxValue, double.Epsilon, -double.Epsilon, 0, 1, -2, 4, 1, 0 };
        var bars = Prices(values).Select((b, i) => new Bar(b.Time, b.Open, Math.Max(b.Close, 0), Math.Min(b.Close, 0), b.Close,
            i % 3 == 0 ? double.Epsilon : i % 3 == 1 ? double.MaxValue : 0)).ToArray();
        foreach (var period in new[] { 1, 2, 3, 7 }) Check(bars, period);
        Check(bars, 2, double.Epsilon, double.MaxValue); Check(bars, 2, -double.MaxValue, -double.MaxValue);
        Check(Prices(Enumerable.Range(0, 20).Select(i => (i % 7 - 3) * double.Epsilon)), 3);
        Check(Prices(Enumerable.Range(0, 20).Select(i => i % 3 == 0 ? Math.BitIncrement(double.MaxValue / 2) : double.MaxValue / 2)), 3);
    }
    [Fact]
    public void NormalizedAndExtremePeriodsAllocateOnlyObservedHistory()
    {
        var bars = Prices(new[] { 1d, -2, 0, 4, 3 });
        foreach (var period in new[] { int.MinValue, 0, 1, int.MaxValue }) { Check(Array.Empty<Bar>(), period); Check(bars, period); }
    }
    [Fact]
    public void SelectedInputsPreserveOriginalEaseRanges()
    {
        var bars = Prices(new[] { 1d, 2, 3, 4, 5, 6 }).Select(b => new Bar(b.Time, b.Open, b.High + 1, b.Low - 1, b.Close, 2)).ToArray();
        Check(bars, 2, selected: new[] { 9d, -2, 3, 99, 0, -1 });
        using var armed = ComponentAverage.Arm((_, _) => throw new InvalidOperationException("Insync has fixed component formulas."));
        Check(bars); Assert.Equal(0, ComponentAverage.Requests);
    }
    [Fact]
    public void LongHistoryExpiryAndZeroWidthsKeepVoteBoundaries()
    {
        var random = new Random(651);
        var bars = Prices(Enumerable.Range(0, 130).Select(_ => random.Next(-20, 21) / 4d));
        foreach (var period in new[] { 1, 2, 5, 14 }) Check(bars, period);
        Check(bars, 3, 0, 0); Check(bars, 3, -2, -1);
        Check(Prices(new[] { -3d, 2, -1, 4, -7, 1, 0 }), 3);
    }
    [Fact]
    public void OverflowingMacdStillContributesItsDirectionVote()
    {
        var bars = Prices(Enumerable.Repeat(-double.MaxValue, 26).Concat(new[] { double.MaxValue, 1d, 0, -1 }));
        var expected = BuiltInFormulaReferences.InsyncValues(bars, Indicator(1, slowLength: 26));
        Assert.Equal(double.PositiveInfinity, expected.Components["Macd"][26].ToDouble());
        Assert.Equal(5, expected.Votes["Macd"][26]);
        Check(bars, 1, slowLength: 26);
    }
    [Fact]
    public void NonfiniteConsumedParametersRejectEmptyInput()
    {
        foreach (var bad in new[] { double.NaN, double.PositiveInfinity, double.NegativeInfinity }) foreach (var width in new[] { false, true })
        {
            var mult = width ? bad : 2; var divisor = width ? 10000 : bad;
            Assert.Throws<ArgumentOutOfRangeException>(() => Data(Array.Empty<Bar>()).CalculateInsyncIndex(stdDevMult: mult, divisor: divisor));
            using var context = new ComputeContext();
            Assert.Throws<ArgumentOutOfRangeException>(() => IndicatorCompute.ComputeInsyncIndexFast(Data(Array.Empty<Bar>()), context, stdDevMult: mult, divisor: divisor));
            Assert.Throws<ArgumentOutOfRangeException>(() => new InsyncIndexState(stdDevMult: mult, divisor: divisor));
        }
    }
    [Fact]
    public void UnconsumedSignalOptionsDoNotAlterComposite()
    {
        var bars = Prices(new[] { 1d, 2, 4, 3, -1, 9, 2, 4 });
        var expected = Data(bars).CalculateInsyncIndex().ChainedValues.ToArray();
        foreach (var period in new[] { 1, 7, int.MaxValue })
            Assert.Equal(expected, Data(bars).CalculateInsyncIndex(maType: MovingAvgType.ExponentialMovingAverage,
                signalLength: period, emoLength: period).ChainedValues);
    }
}

using OoplesFinance.StockIndicators.Builder.Compute;
using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Streaming;
using OoplesFinance.StockIndicators.Validation;
using F = OoplesFinance.StockIndicators.Helpers.UltimatePowerWeights.Fraction;
using R = OoplesFinance.StockIndicators.Validation.ConfluenceReferenceWave.R;
using W = OoplesFinance.StockIndicators.Validation.ConfluenceReferenceWave;
namespace OoplesFinance.StockIndicators.Tests.Unit.ValidationTests;

public sealed class ConfluenceNumericalTests
{
    private static Bar B(double p) => new(DateTime.UnixEpoch, p, p, p, p, 1);
    private static StockData Data(Bar[] bars) => new(bars.Select(b => b.Open), bars.Select(b => b.High), bars.Select(b => b.Low), bars.Select(b => b.Close), bars.Select(b => b.Volume), bars.Select((_, i) => DateTime.UnixEpoch.AddMinutes(i)));
    private static OhlcvBar Native(Bar b) => new("CI", BarTimeframe.Minutes(1), b.Time, b.Time, b.Open, b.High, b.Low, b.Close, b.Volume, true);
    public static IEnumerable<object[]> Cases => new[] { MovingAvgType.SimpleMovingAverage, MovingAvgType.WeightedMovingAverage, MovingAvgType.ExponentialMovingAverage, MovingAvgType.WildersSmoothingMethod }
        .SelectMany(kind => new[] { 1, 3, int.MaxValue }.Select(length => new object[] { kind, length }));
    private static int Kind(MovingAvgType kind) => kind == MovingAvgType.SimpleMovingAverage ? 1 : kind == MovingAvgType.WeightedMovingAverage ? 2 : kind == MovingAvgType.ExponentialMovingAverage ? 3 : 6;
    public static IEnumerable<object[]> Discovered => IndicatorValidationDiscovery.Discover(new[] { typeof(IIndicator).Assembly })
        .Where(c => c.IndicatorType == typeof(ConfluenceIndicator)).Select(c => new object[] { c });
    [Theory, MemberData(nameof(Discovered))]
    public Task EnrolledConfigurationsPassNumericalFixtures(IndicatorValidationCase c) => new OrdinalFamilyNumericalTests().PublicConfigurationsPassEveryNumericalClass(c);
    [Theory, MemberData(nameof(Discovered))]
    public void ReferenceRejectsOutputFaults(IndicatorValidationCase c) => new OrdinalFamilyNumericalTests().EveryPublishedOutputRejectsAnInjectedValueFault(c);
    [Theory, MemberData(nameof(Discovered))]
    public Task SelectedSourcesPassBuilderAndLiveRoutes(IndicatorValidationCase c) => new OrdinalFamilyNumericalTests().SelectedSourcePreservesTheFormulaAndOriginalCandleFields(c);
    [Fact]
    public void CallbackSlotsAndWeightedBatchBypassRemainCompatible()
    {
        var bars = new[] { 2d, 3, -4, 8 }.Select(B).ToArray();
        foreach (var fast in new[] { false, true })
            foreach (var kind in new[] { MovingAvgType.SimpleMovingAverage, MovingAvgType.WeightedMovingAverage })
            {
                var periods = new List<int>();
                var callbacks = Enumerable.Range(0, 9).Select(_ => new Func<IReadOnlyList<double>, int, IReadOnlyList<double>>((values, period) =>
                { periods.Add(period); return Enumerable.Repeat(0d, values.Count).ToArray(); })).ToArray();
                using var scope = ComponentAverage.Arm(callbacks);
                if (fast) { using var context = new ComputeContext(); using var output = IndicatorCompute.ComputeConfluenceIndicatorFast(Data(bars), context, 3, kind); Assert.All(output.ToArray(), v => Assert.Equal(0, v)); }
                else Data(bars).CalculateConfluenceIndicator(kind, 3);
                Assert.Equal(!fast ? Array.Empty<int>() : new[] { 3, 5, 9, 17, 2, 4, 8, 16, 16 }, periods);
            }
    }
    [Theory, MemberData(nameof(Cases))]
    public void RoutesPreviewResetAndLargePeriodsMatchIndependentReference(MovingAvgType kind, int length)
    {
        var bars = new[] { 2d, 2, 2, -4, 9, 0, 17, -9, 31 }.Select(B).ToArray();
        var expected = BuiltInFormulaReferences.ConfluenceExact(bars, length, Kind(kind));
        Assert.Equal(expected, Data(bars).CalculateConfluenceIndicator(kind, length).OutputValues["Ci"]);
        using var context = new ComputeContext(); using var fast = IndicatorCompute.ComputeConfluenceIndicatorFast(Data(bars), context, length, kind);
        Assert.Equal(expected, fast.ToArray());
        using var state = new ConfluenceIndicatorState(kind, length);
        for (var repeat = 0; repeat < 2; repeat++)
        {
            state.Reset();
            for (var i = 0; i < bars.Length; i++)
            {
                state.Update(Native(B(91)), false, false);
                foreach (var final in new[] { false, false, true })
                    Assert.Equal(expected[i], state.Update(Native(bars[i]), final, true).Outputs!["Ci"]);
            }
        }
    }
    [Theory]
    [InlineData(MovingAvgType.SimpleMovingAverage)]
    [InlineData(MovingAvgType.WeightedMovingAverage)]
    [InlineData(MovingAvgType.ExponentialMovingAverage)]
    [InlineData(MovingAvgType.WildersSmoothingMethod)]
    public void ExtremeAndSubnormalCandleMeansKeepExactVotes(MovingAvgType kind)
    {
        foreach (var scale in new[] { double.MaxValue / 4, double.Epsilon })
        {
            var bars = new[] { 1d, -2, 3, -4, 2, 0 }.Select(x => new Bar(DateTime.UnixEpoch, scale * 4, -scale * 4, scale, x * scale, 1)).ToArray();
            var expected = BuiltInFormulaReferences.ConfluenceExact(bars, 2, Kind(kind));
            Assert.Equal(expected, Data(bars).CalculateConfluenceIndicator(kind, 2).OutputValues["Ci"]);
            using var state = new ConfluenceIndicatorState(kind, 2);
            Assert.Equal(expected, bars.Select(b => state.Update(Native(b), true, false).Value));
        }
    }
    [Fact]
    public void UnitPeriodOpposingVotesCancel()
    {
        var bars = Enumerable.Repeat(B(2), 3).ToArray();
        Assert.Equal(new[] { 0d, 0, 0 }, BuiltInFormulaReferences.ConfluenceExact(bars, 1, 1));
        Assert.Equal(new[] { 0d, 0, 0 }, Data(bars).CalculateConfluenceIndicator(length: 1).OutputValues["Ci"]);
    }
    [Fact]
    public void RejectedBarDoesNotAdvanceAnyStage()
    {
        using var actual = new ConfluenceIndicatorState(length: 2); using var clean = new ConfluenceIndicatorState(length: 2);
        actual.Update(Native(B(7)), true, false); clean.Update(Native(B(7)), true, false);
        foreach (var value in new[] { double.NaN, double.PositiveInfinity, double.NegativeInfinity })
            foreach (var slot in Enumerable.Range(0, 5))
            {
                var p = new[] { 1d, 2, 0, 1, 3 }; p[slot] = value;
                var bad = new Bar(DateTime.UnixEpoch, p[0], p[1], p[2], p[3], p[4]);
                Assert.Throws<ArgumentOutOfRangeException>(() => actual.Update(Native(bad), true, true));
            }
        foreach (var p in new[] { 3d, -5, 17, 90 }) Assert.Equal(clean.Update(Native(B(p)), true, false).Value, actual.Update(Native(B(p)), true, false).Value);
    }
    [Fact]
    public void SelectedInputsReplaceBothMeansAndSignalsUsePublishedDifferences()
    {
        var selected = new[] { 2d, -4, 9, 0, 17, -9, 31, 2, 2, 2 };
        var expected = BuiltInFormulaReferences.ConfluenceExact(selected.Select(B).ToArray(), 2, 2);
        var data = Data(Enumerable.Repeat(new Bar(DateTime.UnixEpoch, 100, 300, 50, 200, 1), selected.Length).ToArray());
        data.SetCustomValues(selected.ToList()); using var context = new ComputeContext();
        using var fast = IndicatorCompute.ComputeConfluenceIndicatorFast(data, context, 2, MovingAvgType.WeightedMovingAverage);
        Assert.Equal(expected, fast.ToArray()); Assert.Equal(selected, data.ChainedValues);
        data.CalculateConfluenceIndicator(MovingAvgType.WeightedMovingAverage, 2); Assert.Equal(expected, data.ChainedValues);
        Assert.Equal(Enumerable.Repeat(200d, selected.Length), data.ClosePrices);
        using var state = new ConfluenceIndicatorState(MovingAvgType.WeightedMovingAverage, 2);
        ((ICustomInputConsumer)state).ReadCloseAsInput();
        for (var i = 0; i < selected.Length; i++)
        {
            var b = new Bar(DateTime.UnixEpoch, 100, 300, 50, selected[i], 1);
            Assert.Equal(expected[i], state.Update(Native(b), true, false).Value);
            var slope = R.Of(expected[i]) - R.Of(i == 0 ? 0 : expected[i - 1]);
            var oldSlope = R.Of(i == 0 ? 0 : expected[i - 1]) - R.Of(i < 2 ? 0 : expected[i - 2]);
            var signal = slope.Sign > 0 ? slope.CompareTo(oldSlope) > 0 ? Signal.StrongBuy : Signal.Buy
                : slope.Sign < 0 ? slope.CompareTo(oldSlope) < 0 ? Signal.StrongSell : Signal.Sell : Signal.None;
            Assert.Equal(signal, data.SignalsList[i]);
        }
    }
    [Fact]
    public void IndependentWaveHandlesAlgebraicTiesAndTinyResiduals()
    {
        var primes = W.Factors(new long[] { 7, 11, 13 }); var tiny = R.Of(double.Epsilon);
        Assert.Equal(0, (W.Term(30, 1, 0) - W.Constant((R)1 / 2)).Sign(primes));
        Assert.Equal(0, W.Term(45, 1, -1).Sign(primes));
        Assert.Equal(1, (W.Term(tiny, 1, 0) - W.Term(2 * tiny, (R)1 / 2, 0)).Sign(primes));
        Assert.Equal(-1, (W.Term(30 + tiny, 0, 1) - W.Term(30, 0, 1)).Sign(primes));
        for (var n = 3; n <= 13; n++)
        {
            var expression = W.Constant(1);
            for (var j = 1; j < n; j++) expression += W.Term((R)(360 * j) / n, 0, 1);
            Assert.Equal(0, expression.Sign(W.Factors(new long[] { n })));
        }
    }
}

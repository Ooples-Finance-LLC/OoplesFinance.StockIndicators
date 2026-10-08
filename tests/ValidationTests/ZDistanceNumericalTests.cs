using OoplesFinance.StockIndicators.Builder.Compute;
using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Streaming;
using OoplesFinance.StockIndicators.Validation;
namespace OoplesFinance.StockIndicators.Tests.Unit.ValidationTests;
public sealed class ZDistanceNumericalTests
{
    private static Bar B(double price, double volume = 1, int i = 0) => new(DateTime.UnixEpoch.AddMinutes(i), price, price, price, price, volume);
    private static StockData Data(IReadOnlyList<Bar> bars) => new(bars.Select(b => b.Open), bars.Select(b => b.High), bars.Select(b => b.Low),
        bars.Select(b => b.Close), bars.Select(b => b.Volume), bars.Select(b => b.Time));
    private static OhlcvBar Native(Bar b) => new("ZD", BarTimeframe.Minutes(1), b.Time, b.Time, b.Open, b.High, b.Low, b.Close, b.Volume, true);
    public static IEnumerable<object[]> Cases => IndicatorValidationDiscovery.Discover(new[] { typeof(IIndicator).Assembly })
        .Where(c => c.IndicatorType == typeof(ZDistanceFromVwap)).Select(c => new object[] { c });
    public static IEnumerable<object[]> Routes => Cases.SelectMany(c => new[] { "batch", "fast", "arm", "native", "streaming" }.Select(route => new object[] { c[0], route }));
    [Theory, MemberData(nameof(Routes))]
    public void RoutesMatchIndependentTranslatedMeansAndRootBisection(IndicatorValidationCase c, string route)
        => new OrdinalFamilyNumericalTests().CheckRoutes(c, route, bars => BuiltInFormulaReferences.ZDistanceOutputs(bars, (IBuiltInIndicator)c.Factory()), IndicatorErrorBudget.Exact);
    [Theory, MemberData(nameof(Cases))]
    public Task SelectedInputRetainsOriginalVolumes(IndicatorValidationCase c) => new OrdinalFamilyNumericalTests().SelectedSourcePreservesTheFormulaAndOriginalCandleFields(c);
    private static double[] Check(Bar[] bars, MovingAvgType kind = MovingAvgType.VolumeWeightedAveragePrice, int length = 2)
    {
        var expected = BuiltInFormulaReferences.ZDistanceValues(bars, kind, length);
        var data = Data(bars).CalculateZDistanceFromVwapIndicator(kind, length);
        Assert.Equal(expected, data.CustomValuesList); Assert.Equal(expected, data.OutputValues["Zscore"]);
        using var context = new ComputeContext(); using var fast = IndicatorCompute.ComputeZDistanceFromVwapFast(Data(bars), context, length, kind);
        Assert.Equal(expected, fast.ToArray());
        using var state = new ZDistanceFromVwapState(kind, length);
        for (var replay = 0; replay < 2; replay++)
        {
            state.Update(Native(B(17, 3)), true, false); state.Update(Native(B(-7, -2)), true, false); state.Reset();
            for (var i = 0; i < bars.Length; i++)
            {
                state.Update(Native(B(-23, 9)), false, false);
                foreach (var final in new[] { false, false, true })
                {
                    var point = state.Update(Native(bars[i]), final, true);
                    Assert.Equal(expected[i], point.Value); Assert.Equal(point.Value, point.Outputs!["Zscore"]);
                }
            }
        }
        return expected;
    }
    [Fact]
    public void IndependentHandsUseHistoricalResidualsAndFullWidthWarmup()
    {
        // Means=[1,2,7/3], residuals=[0,1,-1/3]. Final mean square=(1+1/9)/2=5/9.
        Assert.Equal(new[] { 0d, Math.Sqrt(2), -Math.Sqrt(.2) }, Check(new[] { B(1), B(3), B(2, 2) }));
        // Zero total volume defines a zero mean; zero volume is not a dropped observation.
        Assert.Equal(new[] { 0d, Math.Sqrt(2), Math.Sqrt(8d / 13) }, Check(new[] { B(1), B(3, -1), B(2) }));
        Assert.Equal(new[] { 0d, 1, -1, 0 }, Check(new[] { B(1), B(2, 0), B(-2, 0), B(0, 0) }, length: 1));
        Assert.Equal(new[] { 0d, 0 }, Check(new[] { B(1), B(3) }, length: 3));
    }
    [Fact]
    public void SubnormalProductsAndResidualsRetainNormalizedMovement()
    {
        Assert.Equal(new[] { 0d, Math.Sqrt(2) }, Check(new[] { B(double.Epsilon, double.Epsilon), B(3 * double.Epsilon, double.Epsilon) }));
        Assert.Equal(new[] { 0d, Math.Sqrt(2) }, Check(new[] { B(1), B(Math.BitIncrement(1d)) }));
    }
    [Fact]
    public void MidpointMeansMustRemainUnpublishedAcrossResidualHistory()
    {
        // Both nonzero residuals are exactly +/- half an ULP; their squares are equal.
        Assert.Equal(new[] { 0d, Math.Sqrt(2), -1d }, Check(new[] { B(1), B(Math.BitIncrement(1d)), B(1) }));
    }
    [Theory]
    [InlineData(MovingAvgType.VolumeWeightedAveragePrice)]
    [InlineData(MovingAvgType.SimpleMovingAverage)]
    [InlineData(MovingAvgType.WeightedMovingAverage)]
    [InlineData(MovingAvgType.ExponentialMovingAverage)]
    [InlineData(MovingAvgType.WildersSmoothingMethod)]
    public void StandardMeansKeepSignedExtendedResiduals(MovingAvgType kind)
    {
        Check(Enumerable.Range(0, 14).Select(i => B((i * 7 % 13 - 6) * (double.MaxValue / 8), i % 2 == 0 ? double.MaxValue : -double.MaxValue, i)).ToArray(), kind, 3);
        Check(new[] { B(double.MaxValue, double.MaxValue), B(-double.MaxValue, -double.MaxValue), B(0, double.Epsilon), B(1, -double.Epsilon) }, kind, 2);
        Check(new[] { B(double.Epsilon), B(-double.Epsilon), B(0), B(double.Epsilon) }, kind, 3);
        Check(new[] { B(1, 0), B(3, 0), B(2, 0), B(-1, 0) }, kind, 2);
    }
    [Theory]
    [InlineData(int.MaxValue)]
    [InlineData(int.MinValue)]
    public void ExtremePeriodsAllocateOnlyObservedHistory(int length)
    {
        foreach (var kind in new[] { MovingAvgType.VolumeWeightedAveragePrice, MovingAvgType.SimpleMovingAverage,
            MovingAvgType.WeightedMovingAverage, MovingAvgType.ExponentialMovingAverage, MovingAvgType.WildersSmoothingMethod })
        {
            Check(Array.Empty<Bar>(), kind, length);
            Check(new[] { B(1), B(3), B(2), B(4) }, kind, length);
        }
    }
    [Fact]
    public void DirectSelectedPricesKeepOriginalVolumeWeights()
    {
        var bars = new[] { B(100), B(110), B(90, 2) }; var selected = new[] { 1d, 3, 2 };
        var expected = BuiltInFormulaReferences.ZDistanceValues(selected.Select((v, i) => B(v, bars[i].Volume, i)).ToArray(), length: 2);
        var data = Data(bars); data.SetCustomValues(selected.ToList()); using var context = new ComputeContext();
        using var output = IndicatorCompute.ComputeZDistanceFromVwapFast(data, context, length: 2);
        Assert.Equal(expected, output.ToArray()); Assert.Equal(selected, data.ChainedValues);
        data.CalculateZDistanceFromVwapIndicator(length: 2); Assert.Equal(expected, data.CustomValuesList);
        Assert.Equal(bars.Select(b => b.Close), data.ClosePrices); Assert.Equal(bars.Select(b => b.Volume), data.Volumes);
    }
    [Fact]
    public void RegisteredMeansRetainBatchCallbackBypass()
    {
        foreach (var kind in new[] { MovingAvgType.VolumeWeightedAveragePrice, MovingAvgType.WeightedMovingAverage, MovingAvgType.ExponentialMovingAverage })
        {
            using var armed = ComponentAverage.Arm((values, period) => throw new InvalidOperationException("Registered mean callback must be bypassed"));
            Check(new[] { B(1), B(3), B(2) }, kind);
            Assert.Equal(0, ComponentAverage.Requests); Assert.Equal(0, ComponentAverage.Substitutions);
        }
    }
    [Fact]
    public void InvalidCandlesCannotAdvanceMeansOrResidualHistory()
    {
        using var state = new ZDistanceFromVwapState(length: 2); using var control = new ZDistanceFromVwapState(length: 2);
        state.Update(Native(B(1)), true, true); control.Update(Native(B(1)), true, true);
        foreach (var bad in new[] { double.NaN, double.PositiveInfinity, double.NegativeInfinity })
        {
            foreach (var field in Enumerable.Range(0, 5))
            {
                var fields = new[] { 1d, 1, 1, 1, 1 }; fields[field] = bad;
                var bar = new Bar(DateTime.UnixEpoch, fields[0], fields[1], fields[2], fields[3], fields[4]);
                Assert.ThrowsAny<ArgumentException>(() => state.Update(Native(bar), true, true));
                var data = Data(new[] { bar }); data.SetCustomValues(new List<double> { 10 });
                using var context = new ComputeContext();
                Assert.ThrowsAny<ArgumentException>(() => data.CalculateZDistanceFromVwapIndicator());
                Assert.ThrowsAny<ArgumentException>(() => IndicatorCompute.ComputeZDistanceFromVwapFast(data, context));
            }
        }
        Assert.Equal(control.Update(Native(B(3)), true, true).Value, state.Update(Native(B(3)), true, true).Value);
    }
}

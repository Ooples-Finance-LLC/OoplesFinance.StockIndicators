using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Builder.Compute;
using OoplesFinance.StockIndicators.Builder.Specs;
using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Streaming;
using OoplesFinance.StockIndicators.Validation;
namespace OoplesFinance.StockIndicators.Tests.Unit.ValidationTests;
public sealed class FibonacciBandNumericalTests
{
    private static StockData Data(Bar[] bars) => new(bars.Select(b => b.Open), bars.Select(b => b.High), bars.Select(b => b.Low), bars.Select(b => b.Close), bars.Select(b => b.Volume), bars.Select(b => b.Time));
    [Fact]
    public void GeneratedShapePreservesAllThreePublishedBands()
    {
        var indicator = new BollingerBandsFibonacciRatios();
        Assert.IsAssignableFrom<MultiOutputIndicatorBase>(indicator);
        Assert.Equal(3, indicator.Outputs.Count);
        Assert.Equal(new[] { "UpperBand", "MiddleBand", "LowerBand" }, GeneratedIndicatorOutputs.KeysFor(IndicatorName.BollingerBandsFibonacciRatios));
    }
    [Fact]
    public void ThirdRatioAloneControlsPublishedBandsAcrossAllDirectRoutes()
    {
        foreach (var length in new[] { 1, 3 })
        foreach (var kind in new[] { MovingAvgType.SimpleMovingAverage, MovingAvgType.WeightedMovingAverage, MovingAvgType.ExponentialMovingAverage, MovingAvgType.WildersSmoothingMethod })
        foreach (var multiplier in new[] { -2d, 0, .5, 4.236, double.MaxValue })
        foreach (var scale in new[] { 1d, double.Epsilon, double.MaxValue / 16 })
        {
            var bars = Enumerable.Range(0, 12).Select(i => new Bar(DateTime.UnixEpoch.AddMinutes(i), (i % 7 - 3) * scale, (i % 5 + 2) * scale, -(i % 3 + 1) * scale, (i % 9 - 4) * scale, 1)).ToArray();
            var code = kind == MovingAvgType.SimpleMovingAverage ? 1 : kind == MovingAvgType.WeightedMovingAverage ? 2 : kind == MovingAvgType.ExponentialMovingAverage ? 3 : 6;
            var expected = BuiltInFormulaReferences.RangeChannelOutputs(bars, length, code, multiplier, false);
            var batch = Data(bars).CalculateBollingerBandsFibonacciRatios(kind, length, double.MaxValue, -double.MaxValue, multiplier);
            Assert.Equal(IndicatorName.BollingerBandsFibonacciRatios, batch.IndicatorName);
            using var context = new ComputeContext();
            foreach (var key in expected.Keys)
            {
                Assert.Equal(expected[key], batch.OutputValues[key]);
                using var raw = IndicatorCompute.TryComputeFast(Data(bars), new IndicatorSpec(IndicatorName.BollingerBandsFibonacciRatios, new BollingerBandsFibonacciRatiosSpecOptions(length, double.MaxValue, -double.MaxValue, multiplier, kind), key), context);
                Assert.NotNull(raw); Assert.Equal(expected[key], raw.Value.ToArray());
            }
            using var state = new BollingerBandsFibonacciRatiosState(kind, length, double.MaxValue, -double.MaxValue, multiplier);
            Assert.Equal(IndicatorName.BollingerBandsFibonacciRatios, state.Name);
            for (var replay = 0; replay < 2; replay++)
            {
                state.Reset();
                for (var i = 0; i < bars.Length; i++) foreach (var final in new[] { false, false, true })
                {
                    var b = bars[i]; var bar = new OhlcvBar("FIB", BarTimeframe.Minutes(1), b.Time, b.Time, b.Open, b.High, b.Low, b.Close, b.Volume, true);
                    var actual = state.Update(bar, final, true); foreach (var key in expected.Keys) Assert.Equal(expected[key][i], actual.Outputs![key]);
                }
            }
        }
    }
    [Fact]
    public void CustomerStagesUseCenterThenRangeWithTheThirdMultiplier()
    {
        var bars = Enumerable.Range(1, 3).Select(i => new Bar(DateTime.UnixEpoch.AddMinutes(i), i, 10, 0, i, 1)).ToArray();
        using var context = new ComputeContext();
        foreach (var batch in new[] { false, true })
        {
            using var armed = ComponentAverage.Arm(new Func<IReadOnlyList<double>, int, IReadOnlyList<double>>[] {
                (values, period) => { Assert.Equal(2, period); Assert.Equal(new[] { 1d, 2, 3 }, values); return new[] { 3d, 3, 3 }; },
                (values, period) => { Assert.Equal(2, period); Assert.Equal(new[] { 10d, 10, 10 }, values); return new[] { 2d, 2, 2 }; } });
            if (batch) Assert.Equal(new[] { 11d, 11, 11 }, Data(bars).CalculateBollingerBandsFibonacciRatios(length: 2, fibRatio1: 7, fibRatio2: 8, fibRatio3: 4).OutputValues["UpperBand"]);
            else
            {
                using var raw = IndicatorCompute.TryComputeFast(Data(bars), new IndicatorSpec(IndicatorName.BollingerBandsFibonacciRatios, new BollingerBandsFibonacciRatiosSpecOptions(2, 7, 8, 4), "UpperBand"), context);
                Assert.NotNull(raw); Assert.Equal(new[] { 11d, 11, 11 }, raw.Value.ToArray());
            }
            Assert.Equal(2, ComponentAverage.Substitutions);
        }
    }
}

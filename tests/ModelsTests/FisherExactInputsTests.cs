using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Validation;

namespace OoplesFinance.StockIndicators.Tests.Unit.ModelsTests;

public sealed class FisherExactInputsTests
{
    [Fact]
    public void ExactHalvesMatchIndependentMidpointsIncludingOverflowAndCancellation()
    {
        double[] values = [0, -0d, double.Epsilon, -double.Epsilon, Math.ScaleB(1, -1022),
            Math.BitDecrement(Math.ScaleB(1, -1021)), Math.ScaleB(1, -1021),
            Math.BitDecrement(1), 1, Math.BitIncrement(1), double.MaxValue, -double.MaxValue];
        int accepted = 0, fallback = 0;
        foreach (double high in values)
        foreach (double low in values)
        {
            if (!WindowFisherTransform.ExactInputs.TryMidpoint(high, low, out double actual))
            {
                fallback++;
                continue;
            }
            var exact = new ExactMeanAccumulator(); exact.Add(high); exact.Add(low);
            Assert.Equal(BitConverter.DoubleToInt64Bits(exact.Mean(2)), BitConverter.DoubleToInt64Bits(actual));
            accepted++;
        }
        Assert.True(accepted > 0);
        Assert.True(fallback > 0);
        Assert.True(WindowFisherTransform.ExactInputs.TryMidpoint(double.MaxValue, double.MaxValue, out var largest));
        Assert.Equal(double.MaxValue, largest);
    }

    [Fact]
    public void ExactDifferencesMatchIndependentRatiosAndRejectLostBits()
    {
        var random = new Random(73121);
        int accepted = 0, fallback = 0;
        for (int i = 0; i < 10000; i++)
        {
            double low = Math.ScaleB(random.NextDouble() * 2 - 1, random.Next(-1070, 1024));
            double high = Math.ScaleB(random.NextDouble() * 2 - 1, random.Next(-1070, 1024));
            if (high < low) (low, high) = (high, low);
            double price = i % 3 == 0 ? low : i % 3 == 1 ? high : low * .5 + high * .5;
            if (!WindowFisherTransform.ExactInputs.TryPosition(price, low, high, out var actual, out bool flat))
            {
                fallback++;
                continue;
            }
            var numerator = new ExactMeanAccumulator(); numerator.Add(price); numerator.Add(low, -1);
            var denominator = new ExactMeanAccumulator(); denominator.Add(high); denominator.Add(low, -1);
            Assert.Equal(denominator.Sign == 0, flat);
            double expected = flat ? 0 : numerator.Ratio(denominator);
            Assert.Equal(BitConverter.DoubleToInt64Bits(expected), BitConverter.DoubleToInt64Bits(actual));
            accepted++;
        }
        Assert.True(accepted > 0);
        Assert.True(fallback > 0);
        Assert.False(WindowFisherTransform.ExactInputs.TryPosition(1, Math.ScaleB(1, -54), Math.BitIncrement(1), out _, out _));
        Assert.False(WindowFisherTransform.ExactInputs.TryPosition(0, -double.MaxValue, double.MaxValue, out _, out _));
    }

    [Fact]
    public void CompleteTransformAndTriggerTrajectoriesPreserveBitsAfterReset()
    {
        var random = new Random(12637);
        double Value() => Math.ScaleB(random.NextDouble() * 2 - 1, random.Next(-1070, 1024));
        var ordinary = Enumerable.Range(0, 1000).Select(i => new Bar(default, 0,
            100 + random.NextDouble() * 30, 90 + random.NextDouble() * 10,
            95 + random.NextDouble() * 20, 1)).ToArray();
        var extreme = Enumerable.Range(0, 250).Select(i => new Bar(default, 0, Value(), Value(), Value(), 1)).ToArray();
        foreach (int period in new[] { 1, 3, 10, 31, int.MaxValue })
        foreach (bool midpoint in new[] { false, true })
        {
            var state = (IMultiOutputState)new WindowFisherTransform(period, midpoint).CreateState();
            var output = new double[4];
            foreach (var bars in new[] { ordinary, extreme, ordinary })
            {
                var expected = WindowFisherReference.Values(bars, period, midpoint);
                state.Reset();
                for (int i = 0; i < bars.Length; i++)
                {
                    state.Update(in bars[i], output);
                    for (int slot = 0; slot < 2; slot++)
                    {
                        Assert.Equal(BitConverter.DoubleToInt64Bits(expected[slot][i] ?? 0), BitConverter.DoubleToInt64Bits(output[slot]));
                        Assert.Equal(expected[slot][i].HasValue ? 1d : 0d, output[slot + 2]);
                    }
                }
            }
        }
    }
}

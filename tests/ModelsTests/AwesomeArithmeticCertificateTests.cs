using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Validation;

namespace OoplesFinance.StockIndicators.Tests.Unit.ModelsTests;

public sealed class AwesomeArithmeticCertificateTests
{
    [Fact]
    public void MidpointCellsMatchIndependentAccumulatorAcrossScalingBoundaries()
    {
        double[] values = [0, -0d, double.Epsilon, -double.Epsilon, Math.ScaleB(1, -1022),
            Math.BitDecrement(Math.ScaleB(1, -1021)), Math.ScaleB(1, -1021),
            Math.BitDecrement(1), 1, Math.BitIncrement(1), double.MaxValue, -double.MaxValue];
        int accepted = 0, fallback = 0;
        foreach (double high in values)
        foreach (double low in values)
        {
            if (!AwesomeWithDetails.Arithmetic.TryMidpoint(high, low, out double actual))
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
        Assert.False(AwesomeWithDetails.Arithmetic.TryMidpoint(Math.BitDecrement(Math.ScaleB(1, -1021)), 1, out _));
    }

    [Fact]
    public void NormalizationMatchesExactRatiosAndRejectsTiesAndUnsupportedRanges()
    {
        var random = new Random(66123);
        int accepted = 0, fallback = 0;
        for (int i = 0; i < 6000; i++)
        {
            double value = i % 3 == 0 ? random.NextDouble() * 20 - 10
                : Math.ScaleB(random.NextDouble() * 2 - 1, random.Next(-1070, 1024));
            double price = i % 3 == 0 ? 90 + random.NextDouble() * 40
                : Math.ScaleB(random.NextDouble() * 2 - 1, random.Next(-1070, 1024));
            if (!AwesomeWithDetails.Arithmetic.TryNormalize(value, price, out double actual))
            {
                fallback++;
                continue;
            }
            var numerator = new ExactMeanAccumulator(); numerator.Add(value, 100);
            var denominator = new ExactMeanAccumulator(); denominator.Add(price);
            Assert.Equal(BitConverter.DoubleToInt64Bits(numerator.Ratio(denominator)), BitConverter.DoubleToInt64Bits(actual));
            accepted++;
        }
        Assert.True(accepted > 0);
        Assert.True(fallback > 0);
        double adjacent = Math.BitIncrement(1);
        Assert.True(AwesomeWithDetails.Arithmetic.TryNormalize(adjacent, 100, out double cancelled));
        Assert.Equal(BitConverter.DoubleToInt64Bits(adjacent), BitConverter.DoubleToInt64Bits(cancelled));
        foreach (int sign in new[] { -1, 1 })
        foreach (int priceSign in new[] { -1, 1 })
            Assert.False(AwesomeWithDetails.Arithmetic.TryNormalize(sign * (1 + 8 * Math.ScaleB(1, -52)), priceSign, out _));
        foreach (var (value, price) in new[] { (double.Epsilon, 1d), (1d, double.Epsilon),
            (Math.ScaleB(1, 500), 1d), (Math.ScaleB(1, -500), 1d),
            (1d, Math.ScaleB(1, 201)), (1d, Math.ScaleB(1, -201)),
            (double.PositiveInfinity, 1d), (double.NaN, 1d),
            (1d, double.PositiveInfinity), (1d, double.NaN), (1d, 0d) })
            Assert.False(AwesomeWithDetails.Arithmetic.TryNormalize(value, price, out _));
        foreach (double price in new[] { -1d, 1d, double.MaxValue })
        {
            Assert.True(AwesomeWithDetails.Arithmetic.TryNormalize(-0d, price, out double zero));
            Assert.Equal(0L, BitConverter.DoubleToInt64Bits(zero));
        }
    }

    [Theory]
    [InlineData(1, 2)]
    [InlineData(2, 5)]
    [InlineData(5, 34)]
    [InlineData(10, 11)]
    public void AllRoundedStagesMatchRationalRulesAcrossResetAndChainedInput(int fast, int slow)
    {
        var random = new Random(73121);
        var ordinary = Enumerable.Range(0, 257).Select(i => new Bar(default, 100,
            100 + random.NextDouble() * 35, 90 + random.NextDouble() * 10,
            95 + random.NextDouble() * 30, 1)).ToArray();
        var inputs = new List<Bar[]>();
        foreach (double scale in new[] { 1d, -1d, Math.ScaleB(1, -1020), Math.ScaleB(1, 1016) })
            inputs.Add(ordinary.Select(b => new Bar(default, b.Open * scale, b.High * scale,
                b.Low * scale, b.Close * scale, 1)).ToArray());
        inputs.Add(Enumerable.Range(0, 109).Select(i =>
        {
            double value = i % 13 == 0 ? 0 : (i % 2 == 0 ? double.MaxValue : -double.MaxValue);
            return new Bar(default, value, value, value, value, 1);
        }).ToArray());
        inputs.Add(Enumerable.Range(0, 109).Select(i =>
        {
            double value = (i % 7 - 3) * double.Epsilon;
            return new Bar(default, value, value, value, value, 1);
        }).ToArray());
        // At periods1/2 the rounded oscillator is BitIncrement(1), with price2.5.
        // Rounding 100*oscillator before division gives the wrong adjacent value.
        inputs.Add(new[] { .5 - Math.ScaleB(1, -51), 2.5 }
            .Select(value => new Bar(default, value, value, value, value, 1)).ToArray());
        inputs.Add(ordinary);
        foreach (bool chained in new[] { false, true })
        {
            var owner = new AwesomeWithDetails(fast, slow);
            if (chained) owner.Of(new FirstValueEma(1));
            var state = (IMultiOutputState)owner.CreateState();
            var output = new double[4];
            foreach (var bars in inputs)
            {
                state.Reset();
                var actual = Enumerable.Range(0, 4).Select(_ => new double[bars.Length]).ToArray();
                for (int i = 0; i < bars.Length; i++)
                {
                    state.Update(in bars[i], output);
                    for (int slot = 0; slot < 4; slot++) actual[slot][i] = output[slot];
                }
                foreach (var rule in owner.ValidationRules)
                    rule.Check(new IndicatorValidationContext("Awesome private arithmetic", bars, actual, 0));
            }
            if (chained)
            {
                foreach (var invalid in new[] {
                    new Bar(default, 20, double.PositiveInfinity, 10, 20, 1),
                    new Bar(default, 20, 30, double.NaN, 20, 1) })
                {
                    state.Reset();
                    var expected = Record.Exception(() =>
                    {
                        var quote = new ExactMeanAccumulator();
                        quote.Add(invalid.High); quote.Add(invalid.Low);
                    });
                    var actual = Record.Exception(() => state.Update(in invalid, output));
                    Assert.NotNull(expected);
                    Assert.NotNull(actual);
                    Assert.Equal(expected.GetType(), actual.GetType());
                    Assert.Equal(expected.Message, actual.Message);
                }
            }
        }
    }
}

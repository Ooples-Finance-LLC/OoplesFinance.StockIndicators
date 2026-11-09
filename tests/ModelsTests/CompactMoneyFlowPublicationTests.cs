using System.Numerics;
using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.Tests.Unit.ModelsTests;

public sealed class CompactMoneyFlowPublicationTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public void BatchPriceBoundsMatchOriginalAcrossPreviewResetAndExtremeInputs(int scenario)
    {
        var bars = new List<(double High, double Low, double Close, double Volume)>();
        var random = new Random(91404);
        if (scenario == 0)
            for (int i = 0; i < 256; i++)
            {
                double high = 100 + random.NextDouble(), low = high * .5;
                bars.Add((high, low, low + (high - low) * random.NextDouble(), Math.ScaleB(1d, i % 20)));
            }
        else if (scenario == 1)
        {
            double edge = Math.ScaleB(1d, -1021);
            foreach (double high in new[] { Math.BitDecrement(edge), edge, Math.BitIncrement(edge), 4d, double.MaxValue })
            foreach (double low in new[] { high * .5, Math.BitDecrement(high * .5) })
            foreach (double close in new[] { low, Math.BitIncrement(low), Math.BitDecrement(high), high })
            foreach (double volume in new[] { 0d, -0d, 1d, Math.ScaleB(1d, 1000), double.MaxValue, -double.MaxValue })
                bars.Add((high, low, close, volume));
        }
        else
        {
            for (int i = 0; i < 256; i++)
            {
                double high = random.NextDouble() * 200 - 100;
                bars.Add((high, -high, high * .5, (random.NextDouble() - .5) * 1e12));
            }
            bars.AddRange(new[] { (double.Epsilon, 0d, double.Epsilon, double.Epsilon),
                (double.MaxValue, -double.MaxValue, double.MaxValue, double.MaxValue),
                (4d, 2d, Math.BitIncrement(4d), 1d), (4d, 4d, 4d, double.MaxValue) });
        }
        var original = new MoneyFlowAccumulationWindow();
        var candidate = new MoneyFlowAccumulationWindow();
        for (int repeat = 0; repeat < 2; repeat++)
        {
            original.Reset(); candidate.Reset();
            foreach (var bar in bars)
            foreach (bool commit in new[] { false, false, true })
            {
                var expected = original.Next(bar.High, bar.Low, bar.Close, bar.Volume, commit);
                var actual = candidate.NextBatch(bar.High, bar.Low, bar.Close, bar.Volume, commit);
                Equal(expected.Mantissa, actual.Mantissa);
                Assert.Equal(expected.UpperShift, actual.UpperShift);
                Equal(expected.Publish(), actual.Publish());
            }
        }
    }

    [Fact]
    public void CompactSumsMatchTheWideIntegerReference()
    {
        var random = new Random(73389);
        for (int width = 64; width <= 128; width++)
        for (int replay = 0; replay < 8; replay++)
        {
            var bytes = new byte[16]; random.NextBytes(bytes);
            var integer = (new BigInteger(bytes, isUnsigned: true) & ((BigInteger.One << (width - 1)) - 1))
                | (BigInteger.One << (width - 1)) | BigInteger.One;
            foreach (int scale in new[] { 0, 50, 1074, 2098 - width })
            foreach (int sign in new[] { -1, 1 }) Check(sign * integer, scale);
        }
    }

    [Fact]
    public void GuardStickyParityAndMantissaCarryPreserveBothSigns()
    {
        BigInteger leading = BigInteger.One << 64;
        foreach (BigInteger midpoint in new[] { leading + 2048, leading + 6144, (leading << 1) - 2048 })
        foreach (int offset in new[] { -1, 0, 1 })
        foreach (int sign in new[] { -1, 1 }) Check(sign * (midpoint + offset), 1074);
        var tie = FromInteger(leading + 2048, 1074);
        Equal(Math.ScaleB(1, 64), tie.SumWithCompactRounding());
        var oddTie = FromInteger(leading + 6144, 1074);
        Equal(Math.ScaleB(1, 64) + 8192, oddTie.SumWithCompactRounding());
        foreach (int sign in new[] { -1, 1 }) Check(sign * ((BigInteger.One << 128) - 1), 1970);
    }

    [Fact]
    public void SmallLargeFractionalAndExtendedSumsRetainTheirOriginalRounding()
    {
        foreach (int sign in new[] { -1, 1 })
        {
            Check(sign * ((BigInteger.One << 63) - 1), 0);
            Check(sign * ((BigInteger.One << 200) + 1), 0);
            var fractional = FromInteger(sign * ((BigInteger.One << 100) + 1), 0);
            fractional.ScaleByPowerOfTwo(-100);
            Equal(fractional.Mean(1), fractional.SumWithCompactRounding());
            var extended = new ExactMeanAccumulator();
            extended.Add(sign * double.MaxValue); extended.Add(sign * double.MaxValue);
            Equal(extended.Mean(1), extended.SumWithCompactRounding());
        }
        var zero = new ExactMeanAccumulator(); zero.Add(-0d);
        Equal(0, zero.SumWithCompactRounding());
    }

    [Fact]
    public void MoneyFlowPreviewResetAndExtendedCancellationRetainExactState()
    {
        var window = new MoneyFlowAccumulationWindow();
        Equal(double.MaxValue, window.NextBatch(1, 0, 1, double.MaxValue, true).Publish());
        var preview = window.NextBatch(1, 0, 1, double.MaxValue, false);
        Assert.True(preview.UpperShift > 0);
        Equal(double.PositiveInfinity, preview.Publish());
        Equal(0, window.NextBatch(1, 0, 0, double.MaxValue, true).Publish());
        window.NextBatch(1, 0, 1, double.MaxValue, true);
        var extended = window.NextBatch(1, 0, 1, double.MaxValue, true);
        Assert.True(extended.UpperShift > 0);
        Equal(double.MaxValue, window.NextBatch(1, 0, 0, double.MaxValue, true).Publish());
        window.Reset();
        Equal(0, window.NextBatch(1, 1, 1, double.MaxValue, true).Publish());
    }

    [Fact]
    public async Task CapturedIdentityLinePreservesWideHistoryAndOverflowPrecedence()
    {
        Bar[] wide = [new(default, 0, 1, 0, 1, Math.ScaleB(1, 64)),
            new(default, 0, 1, 0, 1, 2048), new(default, 0, 1, 0, 1, 1),
            new(default, 0, 1, 0, 0, Math.ScaleB(1, 64)), new(default, 0, 1, 0, 0, 2049),
            new(default, 0, 1, 0, 0, double.Epsilon), new(default, 0, 1, 0, 1, double.Epsilon)];
        Bar[] overflow = [new(default, 0, 1, 0, 1, double.MaxValue),
            new(default, 0, 1, 0, 1, double.MaxValue), new(default, 0, 1, 0, 0, double.MaxValue)];
        foreach (var bars in new Bar[][] { wide, overflow, [.. overflow, new Bar(default, 0, 1, 0, 1, double.NaN)] })
        foreach (var history in Enum.GetValues<IndicatorHistoryMode>())
        {
            var indicator = new Adl(1);
            StockIndicatorBuilder Builder() => new StockIndicatorBuilder().ConfigureSource(Bars.From(bars))
                .ConfigureIndicators(indicator).ConfigureHistory(history);
            IIndicatorRun? expected = null, actual = null;
            try
            {
                var expectedError = await Record.ExceptionAsync(async () => expected = await Builder().ConfigureBehavior(_ => { }).BuildAsync());
                var actualError = await Record.ExceptionAsync(async () => actual = await Builder().BuildAsync());
                Assert.Equal(expectedError?.GetType(), actualError?.GetType());
                Assert.Equal(expectedError?.Message, actualError?.Message);
                if (expectedError is not null) continue;
                foreach (var output in indicator.Outputs)
                    Assert.Equal(expected![output].ToArray().Select(BitConverter.DoubleToInt64Bits),
                        actual![output].ToArray().Select(BitConverter.DoubleToInt64Bits));
                Assert.Equal(actual![indicator.Outputs[0]].ToArray(), actual[indicator.Outputs[1]].ToArray());
            }
            finally { expected?.Dispose(); actual?.Dispose(); }
        }
    }

    [Fact]
    public void BatchAndStreamingCallsShareExactCommittedAndPreviewState()
    {
        var original = new MoneyFlowAccumulationWindow();
        var mixed = new MoneyFlowAccumulationWindow();
        double[] volumes = [1, Math.ScaleB(1, 64), .1, double.Epsilon, double.MaxValue, double.MaxValue, 3];
        for (int replay = 0; replay < 2; replay++)
        {
            original.Reset(); mixed.Reset();
            for (int i = 0; i < 70; i++)
            {
                double close = i % 4 == 0 ? 0 : 1, volume = volumes[i % volumes.Length];
                Same(original.Next(1, 0, close, volume, false), mixed.NextBatch(1, 0, close, volume, false));
                var expected = original.Next(1, 0, close, volume, true);
                var actual = i % 3 == 0 ? mixed.Next(1, 0, close, volume, true) : mixed.NextBatch(1, 0, close, volume, true);
                Same(expected, actual);
            }
        }

        static void Same(RocBankValue expected, RocBankValue actual)
        {
            Assert.Equal(expected.UpperShift, actual.UpperShift);
            Equal(expected.Mantissa, actual.Mantissa);
            Equal(expected.Publish(), actual.Publish());
        }
    }

    private static void Check(BigInteger integer, int scale)
    {
        var sum = FromInteger(integer, scale);
        double expected = ExactMeanAccumulator.UnitRatio(integer << scale, BigInteger.One);
        Equal(expected, sum.Mean(1));
        Equal(expected, sum.SumWithCompactRounding());
    }

    private static ExactMeanAccumulator FromInteger(BigInteger integer, int scale)
    {
        var sum = new ExactMeanAccumulator();
        int sign = integer.Sign, offset = 0;
        for (var magnitude = BigInteger.Abs(integer); !magnitude.IsZero; magnitude >>= 32, offset += 32)
        {
            uint chunk = (uint)(magnitude & uint.MaxValue);
            if (chunk != 0) sum.Add(Math.ScaleB(sign * (double)chunk, scale + offset - 1074));
        }
        return sum;
    }

    private static void Equal(double expected, double actual) =>
        Assert.Equal(BitConverter.DoubleToInt64Bits(expected), BitConverter.DoubleToInt64Bits(actual));
}

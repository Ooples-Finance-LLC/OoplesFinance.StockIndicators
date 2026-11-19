using System.Numerics;
using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Validation;

namespace OoplesFinance.StockIndicators.Tests.Unit.ModelsTests;

[Collection("IndicatorValuesDispatch")]
public sealed class AccelerationWordMeanTests
{
    [Fact]
    public void AcceptedWordsMatchIndependentFractionsAtTiesCarriesAndSigns()
    {
        int accepted = 0;
        foreach (int count in new[] { 1, 2, 3, 20, 2047, 65535, int.MaxValue })
        foreach (int sign in new[] { -1, 1 })
        foreach (int exponent in new[] { -450, -449, 0, 385, 386, 396 })
        foreach (int width in new[] { 54, 58, 63, 64 })
        foreach (int tail in new[] { -1, 1, 3, 5, 17 })
        {
            Int128 integer = sign * (((Int128)1 << (width - 1)) + tail);
            var sum = JurikDyadic.FromBinaryProduct(integer, exponent);
            var divisor = new ExactMeanAccumulator.WordMeanDivisor(count);
            if (!ValuesBarExecution.TryAccelerationMean(in sum, in divisor, out double result)) continue;
            Equal(Expected(integer, exponent, count), result);
            accepted++;
        }
        Assert.True(accepted > 500);
        foreach (Int128 coefficient in new[] { ((Int128)1 << 54) - 1, ((Int128)1 << 54) + 1, (Int128)ulong.MaxValue })
        foreach (int sign in new[] { -1, 1 })
        foreach (int count in new[] { 1, 3, 20 })
        {
            Int128 integer = sign * coefficient;
            var sum = JurikDyadic.FromBinaryProduct(integer, 0);
            var divisor = new ExactMeanAccumulator.WordMeanDivisor(count);
            Assert.True(ValuesBarExecution.TryAccelerationMean(in sum, in divisor, out double result));
            Equal(Expected(integer, 0, count), result);
        }
        var random = new Random(121126);
        for (int i = 0; i < 5000; i++)
        {
            Int128 integer = ((Int128)1 << 53) + random.NextInt64();
            if ((i & 1) != 0) integer = -integer;
            int exponent = random.Next(-450, 387), count = new[] { 1, 3, 20, 2047 }[i % 4];
            var sum = JurikDyadic.FromBinaryProduct(integer, exponent);
            var divisor = new ExactMeanAccumulator.WordMeanDivisor(count);
            if (ValuesBarExecution.TryAccelerationMean(in sum, in divisor, out double result))
                Equal(Expected(integer, exponent, count), result);
        }
    }

    [Fact]
    public void UnsupportedWidthsScalesAndExtendedStatesRetainOriginalMean()
    {
        var divisor = new ExactMeanAccumulator.WordMeanDivisor(20);
        foreach ((Int128 integer, int exponent) in new (Int128, int)[]
        {
            (0, 0), (1, 0), (((Int128)1 << 53) - 1, 0),
            ((Int128)1 << 64, 0),
            (((Int128)1 << 53) + 1, -451), (((Int128)1 << 53) + 1, 397)
        })
        {
            var sum = JurikDyadic.FromBinaryProduct(integer, exponent);
            Assert.False(ValuesBarExecution.TryAccelerationMean(in sum, in divisor, out _));
            Equal(Expected(integer, exponent, 20), sum.Mean(20));
        }
        var wide = JurikDyadic.FromUnits((BigInteger.One << 3000) + 1);
        Assert.False(ValuesBarExecution.TryAccelerationMean(in wide, in divisor, out _));
        // This synthetic factory state is outside the rolling sum's signed
        // magnitude bound. Preserve its existing checked-arithmetic exception.
        var minimum = JurikDyadic.FromBinaryProduct(Int128.MinValue, 0);
        Assert.False(ValuesBarExecution.TryAccelerationMean(in minimum, in divisor, out _));
        Assert.Throws<OverflowException>(() => minimum.Mean(20));
        var normal = JurikDyadic.FromBinaryProduct(((Int128)1 << 54) + 1, 0);
        Assert.False(ValuesBarExecution.TryAccelerationMean(in normal, default, out _));
    }

    [Theory]
    [InlineData(3)] [InlineData(20)] [InlineData(31)]
    public async Task CapturedWindowsPreserveIndependentMeansAndOwnedHistory(int period)
    {
        double[] values = [Math.ScaleB(1, 53), 1, 3, -Math.ScaleB(1, 53), -1,
            Math.ScaleB(1, 62), 17, -Math.ScaleB(1, 62)];
        double[] scales = [1, Math.ScaleB(1, -450), Math.ScaleB(1, 386),
            Math.ScaleB(1, -500), Math.ScaleB(1, 500)];
        Bar[] bars = Enumerable.Range(0, 257).Select(i => new Bar(DateTime.UnixEpoch.AddMinutes(i),
            0, 0, 0, values[i % values.Length] * scales[i / 53 % scales.Length], 1)).ToArray();
        double?[][] expected = RangeAccelerationReference.Values(bars, period);
        foreach (var mode in Enum.GetValues<IndicatorHistoryMode>())
        {
            var owner = new RangeAccelerationBands(period);
            var input = bars.ToArray();
            using var run = await new StockIndicatorBuilder().ConfigureSource(Bars.From(input))
                .ConfigureIndicators(owner).ConfigureHistory(mode).BuildAsync();
            Array.Clear(input);
            for (int slot = 0; slot < 3; slot++)
            {
                Assert.Equal(expected[slot].Select(v => BitConverter.DoubleToInt64Bits(v ?? 0)),
                    run[owner.Outputs[slot]].ToArray().Select(BitConverter.DoubleToInt64Bits));
                Assert.Equal(expected[slot].Select(v => v.HasValue ? 1d : 0d), run[owner.Outputs[slot + 3]].ToArray());
            }
            Assert.Equal(bars[^1], run.Latest.Bar);
            if (mode != IndicatorHistoryMode.Full) continue;
            int row = 0;
            await foreach (var snapshot in run) Assert.Equal(bars[row++], snapshot.Bar);
            Assert.Equal(bars.Length, row);
        }
    }

    private static double Expected(Int128 integer, int exponent, int count)
    {
        var value = new ReferenceFraction((BigInteger)integer);
        var scale = new ReferenceFraction(BigInteger.One << Math.Abs(exponent));
        return ((exponent < 0 ? value / scale : value * scale) / new ReferenceFraction(count)).ToDouble();
    }

    private static void Equal(double expected, double actual) =>
        Assert.Equal(BitConverter.DoubleToInt64Bits(expected), BitConverter.DoubleToInt64Bits(actual));
}

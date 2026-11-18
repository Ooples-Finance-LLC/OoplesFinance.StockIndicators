using AiDotNet.Tensors.Helpers;
using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Validation;

namespace OoplesFinance.StockIndicators.Tests.Unit.ModelsTests;

[Collection("IndicatorValuesDispatch")]
public sealed class LaggedBlockExecutionTests
{
    private static Bar[] Data(int count) => Enumerable.Range(0, count).Select(i =>
        new Bar(DateTime.UnixEpoch.AddMinutes(i), 100, 102, 98, (i * 13 % 101 - 50) / 8d, i + 1)).ToArray();

    private static void Close(Bar[] bars, int row, double close)
    {
        var b = bars[row];
        bars[row] = new(b.Time, b.Open, b.High, b.Low, close, b.Volume);
    }

    // Separate exact rational formula, with no rolling state or production
    // certificate. ToDouble retains the sign of a nonzero underflowed quotient.
    private static double[] Reference(Bar[] bars, int period, PriceChangeKind kind)
    {
        var result = new double[bars.Length];
        for (int row = period; row < bars.Length; row++)
        {
            var current = ReferenceFraction.FromDouble(bars[row].Close);
            var prior = ReferenceFraction.FromDouble(bars[row - period].Close);
            if (kind != PriceChangeKind.Difference && bars[row - period].Close == 0) continue;
            var numerator = kind is PriceChangeKind.Difference or PriceChangeKind.Fraction or PriceChangeKind.Percent
                ? current - prior : current;
            if (kind is PriceChangeKind.Percent or PriceChangeKind.RatioPercent)
                numerator *= new ReferenceFraction(100);
            result[row] = (kind == PriceChangeKind.Difference ? numerator : numerator / prior).ToDouble();
        }
        return result;
    }

    private static StockIndicatorBuilder Builder(Bar[] bars, IIndicator owner, IndicatorHistoryMode mode) =>
        new StockIndicatorBuilder().ConfigureSource(Bars.From(bars)).ConfigureIndicators(owner).ConfigureHistory(mode);

    private static async Task<double[]> Check(Bar[] bars, int period, PriceChangeKind kind, params int[] workers)
    {
        var owner = new LaggedPriceChange(period, kind);
        var expected = Reference(bars, period, kind);
        Assert.All(expected, value => Assert.True(double.IsFinite(value)));
        var state = (IIndicatorState)owner.CreateState();
        Assert.Equal(expected.Select(BitConverter.DoubleToInt64Bits),
            bars.Select(b => BitConverter.DoubleToInt64Bits(state.Update(in b))));
        int saved = CpuParallelSettings.MaxDegreeOfParallelism;
        try
        {
            foreach (int degree in workers)
            foreach (var mode in Enum.GetValues<IndicatorHistoryMode>())
            {
                CpuParallelSettings.MaxDegreeOfParallelism = degree;
                var input = bars.ToArray();
                using var run = await Builder(input, owner, mode).BuildAsync();
                Array.Clear(input);
                run.Dispose();
                Assert.Equal(expected.Select(BitConverter.DoubleToInt64Bits),
                    run[owner].ToArray().Select(BitConverter.DoubleToInt64Bits));
                Assert.Equal(bars[^1], run.Latest.Bar);
                if (mode == IndicatorHistoryMode.Full)
                {
                    int row = 0;
                    await foreach (var snapshot in run) Assert.Equal(bars[row++], snapshot.Bar);
                    Assert.Equal(bars.Length, row);
                }
            }
        }
        finally { CpuParallelSettings.MaxDegreeOfParallelism = saved; }
        return expected;
    }

    [Theory]
    [InlineData(PriceChangeKind.Difference)] [InlineData(PriceChangeKind.Fraction)]
    [InlineData(PriceChangeKind.Percent)] [InlineData(PriceChangeKind.Ratio)] [InlineData(PriceChangeKind.RatioPercent)]
    public async Task AllTypedBlocksMatchIndependentFormulaAndRetainOwnedHistory(PriceChangeKind kind)
    {
        var bars = Data(8193);
        Close(bars, 0, -0d);
        Close(bars, 4095, 0d);
        Close(bars, 4096, -0d);
        await Check(bars, 3, kind, 1, 2, 8);
    }

    [Theory]
    [InlineData(8191, 1)] [InlineData(8192, 1)] [InlineData(8193, 15)]
    [InlineData(8193, 16)] [InlineData(8193, 17)] [InlineData(8193, 63)]
    [InlineData(8193, 64)] [InlineData(8193, 65)] [InlineData(8193, 4095)]
    [InlineData(8193, 4096)] [InlineData(8193, 4097)] [InlineData(8193, int.MaxValue)]
    public async Task BlockAliasStartupAndOriginalRouteBoundariesRemainExact(int count, int period)
    {
        foreach (var kind in new[] { PriceChangeKind.Difference, PriceChangeKind.Percent })
            await Check(Data(count), period, kind, 2);
    }

    [Theory]
    [InlineData(PriceChangeKind.Fraction)] [InlineData(PriceChangeKind.Percent)] [InlineData(PriceChangeKind.RatioPercent)]
    public async Task RejectedSeedAndLateOwnGridUseOriginalExactFormula(PriceChangeKind kind)
    {
        foreach (int row in new[] { 4095, 8079 })
        {
            var bars = Data(8192);
            Close(bars, row, Math.BitIncrement(100d));
            var proof = new OoplesFinance.StockIndicators.Core.SmaCpuKernel.Certificate(200);
            Assert.True(proof.Include(1d / 8));
            Assert.False(proof.Include(bars[row].Close));
            await Check(bars, 20, kind, 1, 2);
        }
    }

    [Theory]
    [InlineData(PriceChangeKind.Difference)] [InlineData(PriceChangeKind.Fraction)]
    [InlineData(PriceChangeKind.Percent)] [InlineData(PriceChangeKind.Ratio)] [InlineData(PriceChangeKind.RatioPercent)]
    public async Task SubnormalsAndNonzeroSignedUnderflowKeepTheirBits(PriceChangeKind kind)
    {
        var tiny = Data(8192);
        for (int row = 0; row < tiny.Length; row++) Close(tiny, row, (row % 3 - 1) * double.Epsilon);
        await Check(tiny, 1, kind, 2);

        var extreme = Data(8192);
        for (int row = 0; row < extreme.Length; row++) Close(extreme, row, double.MaxValue);
        Close(extreme, extreme.Length - 1, -double.Epsilon);
        var expected = await Check(extreme, 1, kind, 1, 2);
        if (kind is PriceChangeKind.Ratio or PriceChangeKind.RatioPercent)
            Assert.Equal(long.MinValue, BitConverter.DoubleToInt64Bits(expected[^1]));
    }

    [Theory]
    [InlineData(PriceChangeKind.Difference)] [InlineData(PriceChangeKind.Fraction)]
    [InlineData(PriceChangeKind.Percent)] [InlineData(PriceChangeKind.Ratio)] [InlineData(PriceChangeKind.RatioPercent)]
    public async Task ReverseVisitationReportsEarliestOverflowWithItsValue(PriceChangeKind kind)
    {
        var bars = Data(8192);
        for (int row = 0; row < bars.Length; row++) Close(bars, row, 1);
        Close(bars, 16, kind == PriceChangeKind.Difference ? -double.MaxValue : double.Epsilon);
        Close(bars, 17, double.MaxValue);
        Close(bars, 3000, kind == PriceChangeKind.Difference ? double.MaxValue : double.Epsilon);
        Close(bars, 3001, -double.MaxValue);
        int saved = CpuParallelSettings.MaxDegreeOfParallelism;
        try
        {
            foreach (int degree in new[] { 1, 2 })
            foreach (var mode in Enum.GetValues<IndicatorHistoryMode>())
            {
                CpuParallelSettings.MaxDegreeOfParallelism = degree;
                var owner = new LaggedPriceChange(1, kind);
                var expected = await Assert.ThrowsAsync<IndicatorOutputException>(() =>
                    Builder(bars, owner, mode).ConfigureBehavior(_ => { }).BuildAsync());
                var actual = await Assert.ThrowsAsync<IndicatorOutputException>(() => Builder(bars, owner, mode).BuildAsync());
                Assert.Equal(17, actual.BarIndex);
                Assert.Equal(double.PositiveInfinity, actual.Value);
                Assert.Equal(expected.Message, actual.Message);

                var valid = bars[^1];
                bars[^1] = new(valid.Time, valid.Open, valid.High, valid.Low, valid.Close, double.NaN);
                await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => Builder(bars, owner, mode).BuildAsync());
                bars[^1] = valid;
            }
        }
        finally { CpuParallelSettings.MaxDegreeOfParallelism = saved; }
    }

    [Fact]
    public async Task AllInputFieldsAndCancelledBuildsRecoverWithoutChangingFailureOrder()
    {
        var bars = Data(8192);
        int saved = CpuParallelSettings.MaxDegreeOfParallelism;
        try
        {
            CpuParallelSettings.MaxDegreeOfParallelism = 2;
            foreach (var mode in Enum.GetValues<IndicatorHistoryMode>())
            {
                var owner = new LaggedPriceChange(1, PriceChangeKind.Ratio);
                var builder = Builder(bars, owner, mode);
                await Assert.ThrowsAnyAsync<OperationCanceledException>(() => builder.BuildAsync(new CancellationToken(true)));
                foreach (int row in new[] { 0, 4095, 4096, 8191 })
                for (int field = 0; field < 5; field++)
                {
                    var b = bars[row];
                    bars[row] = new(b.Time, field == 0 ? double.NaN : b.Open,
                        field == 1 ? double.PositiveInfinity : b.High,
                        field == 2 ? double.NegativeInfinity : b.Low,
                        field == 3 ? double.NaN : b.Close, field == 4 ? double.NaN : b.Volume);
                    var expected = await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
                        Builder(bars, owner, mode).ConfigureBehavior(_ => { }).BuildAsync());
                    var actual = await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => builder.BuildAsync());
                    Assert.Equal(expected.Message, actual.Message);
                    bars[row] = b;
                }
                using var recovered = await builder.BuildAsync();
                Assert.Equal(Reference(bars, 1, PriceChangeKind.Ratio).Select(BitConverter.DoubleToInt64Bits),
                    recovered[owner].ToArray().Select(BitConverter.DoubleToInt64Bits));
            }
        }
        finally { CpuParallelSettings.MaxDegreeOfParallelism = saved; }
    }
}

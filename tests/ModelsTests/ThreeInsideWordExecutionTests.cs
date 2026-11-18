using AiDotNet.Tensors.Helpers;
using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.Tests.Unit.ModelsTests;

[Collection("IndicatorValuesDispatch")]
public sealed class ThreeInsideWordExecutionTests
{
    private static Bar Make(int i, double open, double close, double volume = 1) =>
        new(DateTime.UnixEpoch.AddMinutes(i), open, Math.Max(open, close), Math.Min(open, close), close, volume);

    private static Bar[] Data(int count)
    {
        var bars = Enumerable.Range(0, count).Select(i => Make(i, 100, 101, i + 1)).ToArray();
        for (int target = 79; target < count; target += 80)
        {
            bool bullish = target / 80 % 2 != 0;
            bars[target - 2] = Make(target - 2, bullish ? 121 : 100, bullish ? 100 : 121, target - 1);
            bars[target - 1] = Make(target - 1, bullish ? 107 : 110, bullish ? 110 : 107, target);
            bars[target] = Make(target, bullish ? 115 : 105, bullish ? 122 : 99, target + 1);
        }
        return bars;
    }

    private static double Price(int encoded, ulong significand) =>
        BitConverter.UInt64BitsToDouble(((ulong)encoded << 52) | (significand - (1UL << 52)));

    private static double[] Reference(Bar[] bars, ThreeInsideCandle owner)
    {
        var independent = TripleBodyCandleReference.Evaluate(bars, owner.LongPeriod,
            owner.ShortPeriod, TripleBodyCandleKind.ThreeInside).ToArray();
        var state = (IIndicatorState)owner.CreateState();
        Assert.Equal(independent.Select(BitConverter.DoubleToInt64Bits),
            bars.Select(b => BitConverter.DoubleToInt64Bits(state.Update(in b))));
        return independent;
    }

    private static async Task<double[]> Check(Bar[] bars, ThreeInsideCandle owner, int[]? degrees = null)
    {
        var expected = Reference(bars, owner);
        int prior = CpuParallelSettings.MaxDegreeOfParallelism;
        try
        {
            foreach (int workers in degrees ?? [1, 2])
            foreach (var mode in Enum.GetValues<IndicatorHistoryMode>())
            {
                CpuParallelSettings.MaxDegreeOfParallelism = workers;
                var input = bars.ToArray();
                using var run = await new StockIndicatorBuilder().ConfigureSource(Bars.From(input))
                    .ConfigureIndicators(owner).ConfigureHistory(mode).BuildAsync();
                Array.Clear(input);
                run.Dispose();
                Assert.Equal(expected.Select(BitConverter.DoubleToInt64Bits),
                    run[owner].ToArray().Select(BitConverter.DoubleToInt64Bits));
                Assert.Equal(bars[^1], run.Latest.Bar);
                if (mode == IndicatorHistoryMode.Full)
                {
                    int index = 0;
                    await foreach (var snapshot in run) Assert.Equal(bars[index++], snapshot.Bar);
                    Assert.Equal(bars.Length, index);
                }
            }
        }
        finally { CpuParallelSettings.MaxDegreeOfParallelism = prior; }
        return expected;
    }

    [Theory]
    [InlineData(1, 1)] [InlineData(1, 64)] [InlineData(64, 1)]
    [InlineData(10, 10)] [InlineData(10, 64)] [InlineData(64, 10)] [InlineData(64, 64)]
    public async Task UnequalWindowsAndPartitionHalosMatchIndependentFullTrajectory(int longPeriod, int shortPeriod)
    {
        var actual = await Check(Data(8193), new ThreeInsideCandle(longPeriod, shortPeriod));
        if (longPeriod == 10 && shortPeriod == 10)
        {
            Assert.Contains(100d, actual);
            Assert.Contains(-100d, actual);
        }
    }

    [Fact]
    public async Task ShortEqualityIsAcceptedAndLongEqualityIsRejected()
    {
        var bars = Enumerable.Range(0, 8193).Select(i => Make(i, 100, 101)).ToArray();
        // The short prior sum includes the long first body: 21 + 9*1 = 30.
        bars[98] = Make(98, 100, 121);
        bars[99] = Make(99, 110, 107); // 10*3 == 30: inclusive short threshold.
        bars[100] = Make(100, 105, 99);
        // Long body1 equals its prior mean1; geometry alone must not signal.
        bars[198] = Make(198, 100, 101);
        bars[199] = Make(199, 100.5, 100.5);
        bars[200] = Make(200, 100.75, 99.5);
        var expected = await Check(bars, new ThreeInsideCandle());
        Assert.Equal(-100d, expected[100]);
        Assert.Equal(0d, expected[200]);
    }

    [Fact]
    public async Task ExactBodyAcrossBinadesDoesNotUseRoundedFloatingSubtraction()
    {
        const ulong unit = 1UL << 52;
        double high = Price(1031, 2 * unit - 1);
        double priorOpen = Price(1027, unit + 2);
        double firstOpen = Price(1027, unit + 1);
        // Both ordinary subtractions round to the same double, but the exact
        // first body is one common-grid unit longer than the previous body.
        Assert.Equal(high - priorOpen, high - firstOpen);
        var bars = Data(8193);
        bars[297] = Make(297, priorOpen, high);
        bars[298] = Make(298, firstOpen, high);
        bars[299] = Make(299, 200, 201);
        bars[300] = Make(300, 200, Price(1027, unit));
        var expected = await Check(bars, new ThreeInsideCandle(1, 1), [1, 2, 8]);
        Assert.Equal(-100d, expected[300]);
    }

    [Theory]
    [InlineData(1)] [InlineData(3)] [InlineData(2044)] [InlineData(2046)]
    public async Task ExtremeNormalCoordinatesAndFullWidthPeriodProductsRemainExact(int encoded)
    {
        const ulong unit = 1UL << 52;
        var bars = Data(8193).Select((b, i) =>
        {
            double open = Price(encoded, unit + (ulong)(b.Open - 90) * 1024);
            double close = Price(encoded, unit + (ulong)(b.Close - 90) * 1024);
            return Make(i, open, close);
        }).ToArray();
        if (encoded is 3 or 2044)
        {
            int grid = encoded - 2;
            bars[300] = Make(300, Price(grid, unit + 1), Price(grid + 4, 2 * unit - 1));
        }
        await Check(bars, new ThreeInsideCandle(64, 1));
    }

    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(2)] [InlineData(3)]
    public async Task UnsupportedInitialAndLateCoordinatesUseOriginalStateWithoutLosingHistory(int kind)
    {
        foreach (int index in new[] { 0, 4095, 4096, 8191 })
        {
            var bars = Data(8193);
            bars[index] = kind switch
            {
                0 => Make(index, 0d, -0d),
                1 => Make(index, double.Epsilon, Math.BitIncrement(double.Epsilon)),
                2 => Make(index, -100, -101),
                _ => Make(index, 2048, 2049)
            };
            await Check(bars, new ThreeInsideCandle(1, 64));
        }
    }

    [Theory]
    [InlineData(8191)] [InlineData(8192)] [InlineData(8193)]
    public async Task SourceSizeGuardAndPeriodsOutsideWordDomainKeepExactContracts(int count)
    {
        await Check(Data(count), new ThreeInsideCandle(10, 10));
        if (count == 8193) await Check(Data(count), new ThreeInsideCandle(65, 1));
    }

    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(2)] [InlineData(3)] [InlineData(4)]
    public async Task EveryInputFieldRetainsValidationDiagnosticsAndBuilderRecovery(int field)
    {
        int prior = CpuParallelSettings.MaxDegreeOfParallelism;
        try
        {
            CpuParallelSettings.MaxDegreeOfParallelism = 2;
            foreach (int index in new[] { 4095, 8192 })
            foreach (double invalid in new[] { double.NaN, double.PositiveInfinity })
            foreach (var mode in Enum.GetValues<IndicatorHistoryMode>())
            {
                var bars = Data(8193); var valid = bars[index];
                var fields = new[] { valid.Open, valid.High, valid.Low, valid.Close, valid.Volume };
                fields[field] = invalid;
                bars[index] = new(valid.Time, fields[0], fields[1], fields[2], fields[3], fields[4]);
                var owner = new ThreeInsideCandle();
                Task<IIndicatorRun> Build(IBarSource source) => new StockIndicatorBuilder().ConfigureSource(source)
                    .ConfigureIndicators(owner).ConfigureHistory(mode).BuildAsync();
                var expected = await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => Build(Bars.From(bars, b => b)));
                var builder = new StockIndicatorBuilder().ConfigureSource(Bars.From(bars)).ConfigureIndicators(owner).ConfigureHistory(mode);
                await Assert.ThrowsAnyAsync<OperationCanceledException>(() => builder.BuildAsync(new CancellationToken(true)));
                var actual = await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => builder.BuildAsync());
                Assert.Equal(expected.Message, actual.Message);
                bars[index] = valid;
                using var run = await builder.BuildAsync();
                Assert.Equal(Reference(bars, owner), run[owner].ToArray());
            }
        }
        finally { CpuParallelSettings.MaxDegreeOfParallelism = prior; }
    }

    [Fact]
    public async Task MixedBoundedRootsAndDuplicateIdentityPreserveOutputsAndFullHistory()
    {
        var bars = Data(8193);
        IndicatorBase[] roots = [new ThreeInsideCandle(1, 64), new TwoCrowsCandle(), new RollingPriceSum(7)];
        var expected = roots.Select(owner =>
        {
            var state = (IIndicatorState)owner.CreateState();
            return bars.Select(b => state.Update(in b)).ToArray();
        }).ToArray();
        int prior = CpuParallelSettings.MaxDegreeOfParallelism;
        try
        {
            CpuParallelSettings.MaxDegreeOfParallelism = 2;
            foreach (var mode in Enum.GetValues<IndicatorHistoryMode>())
            {
                var input = bars.ToArray();
                using var run = await new StockIndicatorBuilder().ConfigureSource(Bars.From(input))
                    .ConfigureIndicators(roots.Concat(new[] { roots[0] }).ToArray()).ConfigureHistory(mode).BuildAsync();
                Array.Clear(input); run.Dispose();
                for (int i = 0; i < roots.Length; i++) Assert.Equal(expected[i], run[roots[i]].ToArray());
                if (mode == IndicatorHistoryMode.Full)
                {
                    int index = 0;
                    await foreach (var snapshot in run) Assert.Equal(bars[index++], snapshot.Bar);
                    Assert.Equal(bars.Length, index);
                }
            }
        }
        finally { CpuParallelSettings.MaxDegreeOfParallelism = prior; }
    }
}

using AiDotNet.Tensors.Helpers;
using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.Tests.Unit.ModelsTests;

[Collection("IndicatorValuesDispatch")]
public sealed class SmallTripleBodyWordExecutionTests
{
    private static IndicatorBase Owner(int kind, int lp = 10, int sp = 10) => kind switch
    {
        0 => new ThreeInsideCandle(lp, sp),
        1 => new TwoCrowsCandle(lp),
        _ => new UpsideGapTwoCrowsCandle(lp, sp)
    };
    private static Bar Make(int i, double open, double close) =>
        new(DateTime.UnixEpoch.AddMinutes(i), open, Math.Max(open, close), Math.Min(open, close), close, i + 1);
    private static (double Open, double Close)[] Pattern(int kind) => kind switch
    {
        0 => [(100, 121), (110, 107), (105, 99)],
        1 => [(100, 121), (127, 124), (126, 110)],
        _ => [(100, 121), (127, 124), (130, 122)]
    };
    private static Bar[] Data(int count, int kind)
    {
        var bars = Enumerable.Range(0, count).Select(i => Make(i, 100, 101)).ToArray();
        foreach (int target in new[] { 79, 127, 499, 500, 501, 999, 4095, 4096, 8190 })
            if (target < count)
                for (int j = 0; j < 3; j++)
                {
                    var p = Pattern(kind)[j]; int i = target - 2 + j;
                    bars[i] = Make(i, p.Open, p.Close);
                }
        return bars;
    }
    private static double[] Reference(Bar[] bars, int kind, int lp, int sp)
    {
        if (kind == 1) sp = lp;
        var independent = TripleBodyCandleReference.Evaluate(bars, lp, sp, (TripleBodyCandleKind)kind).ToArray();
        var state = (IIndicatorState)Owner(kind, lp, sp).CreateState();
        Assert.Equal(independent.Select(BitConverter.DoubleToInt64Bits), bars.Select(b => BitConverter.DoubleToInt64Bits(state.Update(in b))));
        return independent;
    }
    private static async Task<double[]> Check(Bar[] bars, int kind, int lp = 10, int sp = 10, int[]? degrees = null)
    {
        var expected = Reference(bars, kind, lp, sp);
        int prior = CpuParallelSettings.MaxDegreeOfParallelism;
        try
        {
            foreach (int workers in degrees ?? [1, 2, 8])
            foreach (var mode in Enum.GetValues<IndicatorHistoryMode>())
            {
                CpuParallelSettings.MaxDegreeOfParallelism = workers;
                var input = bars.ToArray(); var owner = Owner(kind, lp, sp);
                using var run = await new StockIndicatorBuilder().ConfigureSource(Bars.From(input))
                    .ConfigureIndicators(owner).ConfigureHistory(mode).BuildAsync();
                Array.Clear(input); run.Dispose();
                Assert.Equal(expected.Select(BitConverter.DoubleToInt64Bits), run[owner].ToArray().Select(BitConverter.DoubleToInt64Bits));
                if (bars.Length > 0) Assert.Equal(bars[^1], run.Latest.Bar);
                if (mode == IndicatorHistoryMode.Full)
                {
                    int i = 0;
                    await foreach (var snapshot in run) Assert.Equal(bars[i++], snapshot.Bar);
                    Assert.Equal(bars.Length, i);
                }
            }
        }
        finally { CpuParallelSettings.MaxDegreeOfParallelism = prior; }
        return expected;
    }

    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(2)]
    public async Task SmallEqualUnequalAndFullWidthPeriodsMatchBothIndependentTrajectories(int kind)
    {
        foreach (var (lp, sp) in new[] { (1, 1), (1, 64), (64, 1), (10, 10), (64, 64) })
        {
            var expected = await Check(Data(1000, kind), kind, lp, sp);
            if (lp == 10 && sp == 10) Assert.Contains(-100d, expected);
        }
    }

    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(2)]
    public async Task EmptyWarmupWorkerAnd8192RoutingBoundariesRetainContracts(int kind)
    {
        foreach (int count in new[] { 0, 1, 2, 11, 12, 13, 511, 512, 513, 8191, 8192, 8193 })
            await Check(Data(count, kind), kind, degrees: [1, 2]);
        await Check(Data(1000, kind), kind, 65, 1, [1, 2]);
    }

    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(2)]
    public async Task ShortEqualityIsInclusiveLongEqualityIsStrictAndTwoCrowsHasNoShortThreshold(int kind)
    {
        var bars = Data(1000, kind);
        for (int i = 70; i <= 100; i++) bars[i] = Make(i, 100, 101);
        bars[98] = Make(98, 100, 121);
        bars[99] = kind == 0 ? Make(99, 110, 107) : Make(99, 127, 124);
        bars[100] = kind switch { 0 => Make(100, 105, 99), 1 => Make(100, 126, 110), _ => Make(100, 130, 122) };
        for (int i = 170; i <= 200; i++) bars[i] = Make(i, 100, 101);
        bars[198] = Make(198, 100, 101);
        bars[199] = kind == 0 ? Make(199, 100.5, 100.5) : Make(199, 103, 102);
        bars[200] = kind switch { 0 => Make(200, 100.75, 99.5), 1 => Make(200, 102.5, 100.5), _ => Make(200, 104, 101.5) };
        if (kind == 1)
        {
            bars[298] = Make(298, 100, 121); bars[299] = Make(299, 160, 130); bars[300] = Make(300, 150, 110);
        }
        if (kind == 0)
        {
            bars[398] = Make(398, 121, 100); bars[399] = Make(399, 107, 110); bars[400] = Make(400, 122, 122);
        }
        var expected = await Check(bars, kind);
        if (kind == 0) Assert.Equal(100d, expected[400]);
        Assert.Equal(-100d, expected[100]); Assert.Equal(0d, expected[200]);
        if (kind == 1) Assert.Equal(-100d, expected[300]);
    }

    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(2)]
    public async Task WideUnroundedBodyDifferenceKeepsLowGridBits(int kind)
    {
        const ulong unit = 1UL << 52;
        static double Price(int e, ulong m) => BitConverter.UInt64BitsToDouble(((ulong)e << 52) | (m - (1UL << 52)));
        double high = Price(1031, unit + unit / 2), priorOpen = Price(1027, unit + 2), firstOpen = Price(1027, unit + 1);
        Assert.Equal(high - priorOpen, high - firstOpen);
        var bars = Data(1000, kind);
        bars[297] = Make(297, priorOpen, high); bars[298] = Make(298, firstOpen, high);
        bars[299] = kind == 0 ? Make(299, 200, 201) : Make(299, 400, 399);
        bars[300] = kind switch { 0 => Make(300, 200, Price(1027, unit)), 1 => Make(300, 399.5, 300), _ => Make(300, 401, 390) };
        var expected = await Check(bars, kind, 1, 1);
        Assert.Equal(-100d, expected[300]);
    }

    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(2)]
    public async Task UnsupportedFirstAndPartitionAdjacentCoordinatesReplayCapturedHistory(int kind)
    {
        foreach (int index in new[] { 0, 499, 500, 999 })
        foreach (var (open, close) in new[] { (0d, -0d), (double.Epsilon, Math.BitIncrement(double.Epsilon)), (-100d, -101d), (2048d, 2049d) })
        {
            var bars = Data(1000, kind); bars[index] = Make(index, open, close);
            await Check(bars, kind, 1, 64, [1, 2]);
        }
    }

    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(2)]
    public async Task FiniteUnusedFieldsDoNotRestrictTheBodyCertificate(int kind)
    {
        var bars = Data(1000, kind); var b = bars[500];
        bars[500] = new(b.Time, b.Open, -double.MaxValue, double.MaxValue, b.Close, -double.MaxValue);
        await Check(bars, kind);
    }

    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(2)] [InlineData(3)] [InlineData(4)]
    public async Task AllFieldsPreserveDeferredValidationCancellationAndRecovery(int field)
    {
        int prior = CpuParallelSettings.MaxDegreeOfParallelism;
        try
        {
            CpuParallelSettings.MaxDegreeOfParallelism = 2;
            foreach (int kind in new[] { 0, 1, 2 })
            foreach (var mode in Enum.GetValues<IndicatorHistoryMode>())
            {
                var bars = Data(1000, kind); var valid = bars[500];
                var values = new[] { valid.Open, valid.High, valid.Low, valid.Close, valid.Volume }; values[field] = double.NaN;
                bars[500] = new(valid.Time, values[0], values[1], values[2], values[3], values[4]);
                var owner = Owner(kind);
                Task<IIndicatorRun> Build(IBarSource source) => new StockIndicatorBuilder().ConfigureSource(source).ConfigureIndicators(owner).ConfigureHistory(mode).BuildAsync();
                var expected = await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => Build(Bars.From(bars, b => b)));
                var builder = new StockIndicatorBuilder().ConfigureSource(Bars.From(bars)).ConfigureIndicators(owner).ConfigureHistory(mode);
                await Assert.ThrowsAnyAsync<OperationCanceledException>(() => builder.BuildAsync(new CancellationToken(true)));
                var actual = await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => builder.BuildAsync()); Assert.Equal(expected.Message, actual.Message);
                bars[500] = valid; using var run = await builder.BuildAsync(); Assert.Equal(Reference(bars, kind, 10, 10), run[owner].ToArray());
            }
        }
        finally { CpuParallelSettings.MaxDegreeOfParallelism = prior; }
    }
}

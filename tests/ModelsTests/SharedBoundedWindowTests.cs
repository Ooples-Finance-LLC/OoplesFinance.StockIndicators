using AiDotNet.Tensors.Helpers;
using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.Tests.Unit.ModelsTests;

[Collection("IndicatorValuesDispatch")]
public sealed class SharedBoundedWindowTests
{
    public static IEnumerable<object[]> Configurations => from operation in Enumerable.Range(0, 12)
        from period in new[] { 1, 3, 20, 4096, 4097, int.MaxValue } select new object[] { operation, period };
    private static IndicatorBase Create(int operation, int period) => operation switch
    {
        0 => new Wma(period), 1 => new WilliamsR(period), 2 => new HighestHigh(period),
        3 => new LowestLow(period), 4 => new RollingPriceSum(period),
        5 => new WindowRangeMidpoint(period), 6 => new WindowRangeMidpoint(period, true),
        7 => new DecayingWindowExtreme(period), 8 => new DecayingWindowExtreme(period, true),
        9 => new WindowExtremeIndex(period), 10 => new WindowExtremeIndex(period, true), _ => new WindowPriceRange(period)
    };

    [Theory, MemberData(nameof(Configurations))]
    public async Task WorkerSeedsMatchEvaluatorBitsAndOwnedSnapshots(int operation, int period)
    {
        int previous = CpuParallelSettings.MaxDegreeOfParallelism;
        try
        {
            double[] values = [-0d, 0d, 1, 1, -.25, 2, -2, .25, 1, -0d, 0d];
            int count = period == int.MaxValue ? 65 : 8193;
            var bars = Enumerable.Range(0, count).Select(i => new Bar(DateTime.UnixEpoch.AddMinutes(i), 0,
                values[i % values.Length], values[(i + 3) % values.Length], values[(i + 5) % values.Length], 1)).ToArray();
            var indicator = Create(operation, period);
            using var expected = await Build(bars, [indicator]).ConfigureBehavior(_ => { }).BuildAsync();
            foreach (int workers in new[] { 1, 8 })
            foreach (var history in Enum.GetValues<IndicatorHistoryMode>())
            {
                CpuParallelSettings.MaxDegreeOfParallelism = workers;
                var builder = Build(bars, [indicator]).ConfigureHistory(history);
                using var actual = await builder.BuildAsync();
                Compare(expected, actual, indicator);
                Assert.Equal(expected.Latest.IsWarmedUp, actual.Latest.IsWarmedUp);
                Assert.Equal(bars[^1], actual.Latest.Bar);
                if (history == IndicatorHistoryMode.Full)
                {
                    Assert.Contains("Fused CPU values", builder.LastExecution!.Reason);
                    int index = 0;
                    await foreach (var snapshot in actual) Assert.Equal(bars[index++], snapshot.Bar);
                    Assert.Equal(count, index);
                }
            }
        }
        finally { CpuParallelSettings.MaxDegreeOfParallelism = previous; }
    }

    [Theory]
    [InlineData(0)] [InlineData(1)]
    public async Task NewFamiliesPreserveEmptyClampedPeriodsWideValuesMixedAndComposedRoutes(int operation)
    {
        double[] values = [-0d, 0d, double.MaxValue, -double.MaxValue, double.Epsilon, -double.Epsilon, 1];
        foreach (int period in new[] { -3, 0, 1, 3, int.MaxValue })
        foreach (int count in new[] { 0, 1, 65 })
        {
            var bars = Enumerable.Range(0, count).Select(i => new Bar(default, 0,
                values[i % 7], values[(i + 1) % 7], values[(i + 2) % 7], 1)).ToArray();
            var indicator = Create(operation, period);
            await CompareRoutes(bars, [indicator]);
            await CompareRoutes(bars, [indicator, new FirstValueEma(3)]);
        }
        var ordinaryBars = Enumerable.Range(0, 65).Select(i => new Bar(default, i, i + 3, i - 1, i + 1, 1)).ToArray();
        var composed = Create(operation, 3).Of(new Sma(3));
        Assert.False(ValuesBarExecution.SupportsOwned([composed]));
        await CompareRoutes(ordinaryBars, [composed]);
    }

    [Theory]
    [InlineData(1004)] [InlineData(1005)] [InlineData(1023)] [InlineData(1024)] [InlineData(8192)]
    public async Task InvalidCapturedTailsPrecedeEarlierOverflowAndDoNotEscapePoolLifetime(int invalidIndex)
    {
        int previous = CpuParallelSettings.MaxDegreeOfParallelism;
        try
        {
            CpuParallelSettings.MaxDegreeOfParallelism = 8;
            var bars = Enumerable.Range(0, 8193).Select(i => new Bar(default, 0, 1, 0, .5, 1)).ToArray();
            var indicator = new WilliamsR(20);
            using var saved = await Build(bars, [indicator]).BuildAsync();
            var savedValues = saved[indicator].ToArray();
            var original = bars.ToArray();
            bars[0] = new Bar(default, 0, double.Epsilon, 0, double.MaxValue, 1);
            await CompareRoutes(bars, [indicator]);
            bars[invalidIndex] = new Bar(default, 0, 1, 0, .5, double.NaN);
            await CompareRoutes(bars, [indicator]);
            Array.Copy(original, bars, bars.Length);
            using var again = await Build(bars, [indicator]).BuildAsync();
            Assert.Equal(savedValues, saved[indicator].ToArray());
            int index = 0;
            await foreach (var snapshot in saved) Assert.Equal(original[index++], snapshot.Bar);
            using var cancel = new CancellationTokenSource(); cancel.Cancel();
            var builder = Build(bars, [indicator]);
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => builder.BuildAsync(cancel.Token));
            Assert.Null(builder.LastExecution);
            await Assert.ThrowsAsync<NotSupportedException>(() => builder.ConfigureExecution(IndicatorExecutionBackend.Gpu).BuildAsync());
            Assert.Null(builder.LastExecution);
        }
        finally { CpuParallelSettings.MaxDegreeOfParallelism = previous; }
    }

    [Theory]
    [InlineData(1)] [InlineData(3)] [InlineData(int.MaxValue)]
    public async Task SumStartupWideCancellationAndMixedRootsMatchTheOwnedEvaluator(int period)
    {
        double[] values = [double.MaxValue, double.MaxValue, -double.MaxValue, double.Epsilon, -double.Epsilon, -0d, 0d, 1];
        foreach (int count in new[] { 0, 1, 65 })
        {
            var bars = Enumerable.Range(0, count).Select(i => new Bar(default, 0, 1, 0, values[i % values.Length], 1)).ToArray();
            var sum = new RollingPriceSum(period);
            await CompareRoutes(bars, [sum]);
            await CompareRoutes(bars, [sum, new FirstValueEma(3)]);
        }
    }

    [Theory]
    [InlineData(1)] [InlineData(-1)]
    public async Task SumParallelInputErrorsPrecedeOverflowAndSnapshotsSurviveLaterRuns(int sign)
    {
        int previous = CpuParallelSettings.MaxDegreeOfParallelism;
        try
        {
            CpuParallelSettings.MaxDegreeOfParallelism = 8;
            var bars = Enumerable.Repeat(new Bar(default, 0, 1, 0, .5, 1), 8193).ToArray();
            var sum = new RollingPriceSum(20);
            using var saved = await Build(bars, [sum]).BuildAsync();
            var values = saved[sum].ToArray();
            for (int i = 0; i < 20; i++) bars[i] = new Bar(default, 0, 1, 0, sign * double.MaxValue, 1);
            await CompareRoutes(bars, [sum]);
            bars[1023] = new Bar(default, 0, 1, 0, .5, double.NaN);
            await CompareRoutes(bars, [sum]);
            Array.Fill(bars, new Bar(default, 0, 1, 0, .25, 1));
            using var again = await Build(bars, [sum]).BuildAsync();
            Assert.Equal(values, saved[sum].ToArray());
            await foreach (var snapshot in saved) Assert.Equal(.5, snapshot.Bar.Close);
            var builder = Build(bars, [sum]);
            using var cancel = new CancellationTokenSource(); cancel.Cancel();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => builder.BuildAsync(cancel.Token));
            Assert.Null(builder.LastExecution);
            // Certified sums now support GPU; this finer grid must still reject.
            bars[0] = new Bar(default, 0, 1, 0, .1, 1);
            await Assert.ThrowsAsync<NotSupportedException>(() => builder.ConfigureExecution(IndicatorExecutionBackend.Gpu).BuildAsync());
            Assert.Null(builder.LastExecution);
        }
        finally { CpuParallelSettings.MaxDegreeOfParallelism = previous; }
    }

    [Fact]
    public async Task EngulfingExhaustsBodyEndpointsAcrossWorkerBoundariesAndRetainsOwnership()
    {
        int previous = CpuParallelSettings.MaxDegreeOfParallelism;
        try
        {
            double[] endpoints = [-double.MaxValue, -1, -double.Epsilon, -0d, 0d, double.Epsilon, 1, double.MaxValue];
            var list = new List<Bar>();
            foreach (var a in endpoints) foreach (var b in endpoints)
            foreach (var c in endpoints) foreach (var d in endpoints)
            {
                list.Add(new Bar(default, a, 1, -1, b, 1));
                list.Add(new Bar(default, c, 1, -1, d, 1));
            }
            list.Add(list[0]);
            var bars = list.ToArray();
            var pattern = new EngulfingPattern();
            foreach (int workers in new[] { 1, 2, 8 })
            {
                CpuParallelSettings.MaxDegreeOfParallelism = workers;
                await CompareRoutes(bars, [pattern]);
                await CompareRoutes(bars, [pattern, new HighestHigh(3)]);
            }
            foreach (int count in new[] { 0, 1, 2, 3 })
                await CompareRoutes(bars.Take(count).ToArray(), [pattern]);
            var builder = Build(bars, [pattern]);
            using var saved = await builder.BuildAsync();
            Assert.Contains("Fused CPU values", builder.LastExecution!.Reason);
            var values = saved[pattern].ToArray();
            var original = bars.ToArray();
            foreach (int index in new[] { 1022, 1023, 1024, 8192 })
            {
                bars[index] = new Bar(default, 0, 1, 0, 1, double.NaN);
                await CompareRoutes(bars, [pattern]);
                bars[index] = original[index];
            }
            Array.Fill(bars, new Bar(default, 0, 0, 0, 0, 0));
            using var again = await Build(bars, [pattern]).BuildAsync();
            Assert.Equal(values, saved[pattern].ToArray());
            int position = 0;
            await foreach (var snapshot in saved) Assert.Equal(original[position++], snapshot.Bar);
            using var cancel = new CancellationTokenSource(); cancel.Cancel();
            builder = Build(bars, [pattern]);
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => builder.BuildAsync(cancel.Token));
            Assert.Null(builder.LastExecution);
            await Assert.ThrowsAsync<NotSupportedException>(() => builder.ConfigureIndicators(pattern, new HighestHigh(3))
                .ConfigureExecution(IndicatorExecutionBackend.Gpu).BuildAsync());
            Assert.Null(builder.LastExecution);
        }
        finally { CpuParallelSettings.MaxDegreeOfParallelism = previous; }
    }

    [Theory]
    [InlineData(1)] [InlineData(3)] [InlineData(20)] [InlineData(4096)] [InlineData(4097)] [InlineData(int.MaxValue)]
    public async Task MixedBoundedWindowsShareCaptureAndPreserveEachStartupAndPeriod(int period)
    {
        int previous = CpuParallelSettings.MaxDegreeOfParallelism;
        try
        {
            double[] prices = [-0d, 0d, 1, -1, .5, -.5, 1, 1];
            int count = period == int.MaxValue ? 65 : 8193;
            var bars = Enumerable.Range(0, count).Select(i => new Bar(default, prices[i % 8],
                prices[(i + 1) % 8], prices[(i + 3) % 8], prices[(i + 5) % 8], 1)).ToArray();
            IIndicator[] indicators = [new LowestLow(period), new HighestHigh(3), new Wma(period),
                new WilliamsR(7), new RollingPriceSum(period), new EngulfingPattern(),
                new WindowRangeMidpoint(period), new WindowRangeMidpoint(7, true)];
            foreach (int workers in new[] { 1, 2, 8 })
            {
                CpuParallelSettings.MaxDegreeOfParallelism = workers;
                await CompareRoutes(bars, indicators);
                await CompareRoutes(bars, indicators.Reverse().ToArray());
            }
            foreach (int size in new[] { 0, 1, 2, 3, 7 })
                await CompareRoutes(bars.Take(size).ToArray(), indicators);
            await CompareRoutes(bars, [indicators[0], indicators[0]]);
            var builder = Build(bars, indicators);
            using var saved = await builder.BuildAsync();
            var expected = indicators.Select(i => saved[i].ToArray()).ToArray();
            var original = bars.ToArray();
            Array.Clear(bars);
            using var later = await builder.BuildAsync();
            for (int i = 0; i < indicators.Length; i++) Assert.Equal(expected[i], saved[indicators[i]].ToArray());
            int position = 0;
            await foreach (var snapshot in saved) Assert.Equal(original[position++], snapshot.Bar);
        }
        finally { CpuParallelSettings.MaxDegreeOfParallelism = previous; }
    }

    [Fact]
    public async Task MixedBoundedFailuresRetainInputThenIndicatorThenIndexOrdering()
    {
        int previous = CpuParallelSettings.MaxDegreeOfParallelism;
        try
        {
            CpuParallelSettings.MaxDegreeOfParallelism = 8;
            var bars = Enumerable.Repeat(new Bar(default, 0, 1, 0, .5, 1), 8193).ToArray();
            var sum = new RollingPriceSum(2);
            var range = new WilliamsR(1);
            // Williams overflows first in time; sum wins when it is the first
            // configured output, even though its overflow is in a later worker.
            bars[0] = new Bar(default, 0, double.Epsilon, 0, 1, 1);
            bars[2047] = bars[2048] = new Bar(default, 0, double.MaxValue, 0, double.MaxValue, 1);
            await CompareRoutes(bars, [sum, range]);
            await CompareRoutes(bars, [range, sum]);
            foreach (int index in new[] { 1022, 1023, 1024, 8192 })
            {
                var original = bars[index];
                bars[index] = new Bar(default, 0, 1, 0, 0, double.NaN);
                await CompareRoutes(bars, [sum, range]);
                bars[index] = original;
            }
            using var cancel = new CancellationTokenSource(); cancel.Cancel();
            var builder = Build(bars, [sum, range]);
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => builder.BuildAsync(cancel.Token));
            Assert.Null(builder.LastExecution);
            await Assert.ThrowsAsync<NotSupportedException>(() => builder.ConfigureExecution(IndicatorExecutionBackend.Gpu).BuildAsync());
            Assert.Null(builder.LastExecution);
        }
        finally { CpuParallelSettings.MaxDegreeOfParallelism = previous; }
    }

    [Theory]
    [InlineData(1)] [InlineData(2)] [InlineData(20)] [InlineData(64)] [InlineData(65)]
    public async Task ExtremaPreserveMonotonicPlateauAndSignedZeroWindowsAcrossWorkers(int period)
    {
        int previous = CpuParallelSettings.MaxDegreeOfParallelism;
        try
        {
            foreach (int shape in Enumerable.Range(0, 4))
            {
                double[] extremes = [double.MaxValue, -double.MaxValue, double.Epsilon, -double.Epsilon, -0d, 0d];
                var bars = Enumerable.Range(0, 8193).Select(i =>
                {
                    double price = shape switch
                    {
                        0 => i, 1 => -i, 2 => i % 2 == 0 ? -0d : 0d,
                        _ => extremes[(i / (period + 1)) % extremes.Length]
                    };
                    return new Bar(default, price, price, price, price, 1);
                }).ToArray();
                IIndicator[] pair = [new HighestHigh(period), new LowestLow(period)];
                foreach (int workers in new[] { 1, 8 })
                {
                    CpuParallelSettings.MaxDegreeOfParallelism = workers;
                    await CompareRoutes(bars, pair);
                    foreach (var indicator in pair) await CompareRoutes(bars, [indicator]);
                }
            }
        }
        finally { CpuParallelSettings.MaxDegreeOfParallelism = previous; }
    }

    [Fact]
    public async Task MidpointWorkerThresholdPreservesStandaloneAndMixedBoundaries()
    {
        int previous = CpuParallelSettings.MaxDegreeOfParallelism;
        try
        {
            CpuParallelSettings.MaxDegreeOfParallelism = 8;
            foreach (int count in new[] { 511, 512, 513 })
            {
                var bars = Enumerable.Range(0, count).Select(i => new Bar(default, 0,
                    (i % 13 - 6) / 8d, -(i % 11) / 8d, (i % 7 - 3) / 8d, 1)).ToArray();
                await CompareRoutes(bars, [new WindowRangeMidpoint(20)]);
                await CompareRoutes(bars, [new WindowRangeMidpoint(20, true)]);
                await CompareRoutes(bars, [new WindowRangeMidpoint(20), new WindowRangeMidpoint(7, true)]);
                bars[256] = new Bar(default, 0, double.NaN, 0, 0, 1);
                await CompareRoutes(bars, [new WindowRangeMidpoint(20)]);
            }
        }
        finally { CpuParallelSettings.MaxDegreeOfParallelism = previous; }
    }

    [Fact]
    public async Task CloseExtremaPreserveWideValuesMixedSeedsAndExcludePositiveDecay()
    {
        int previous = CpuParallelSettings.MaxDegreeOfParallelism;
        try
        {
            CpuParallelSettings.MaxDegreeOfParallelism = 8;
            double[] values = [-0d, 0d, double.MaxValue, -double.MaxValue, double.Epsilon, -double.Epsilon, 1];
            foreach (int count in new[] { 0, 1, 8193 })
            {
                var bars = Enumerable.Range(0, count).Select(i => new Bar(default, 0, 1, -1, values[i % values.Length], 1)).ToArray();
                await CompareRoutes(bars, [new DecayingWindowExtreme(20), new DecayingWindowExtreme(7, true)]);
                if (count > 1)
                {
                    bars[1023] = new Bar(default, 0, 1, -1, 1, double.NaN);
                    await CompareRoutes(bars, [new DecayingWindowExtreme(20), new DecayingWindowExtreme(7, true)]);
                }
            }
            var ordinary = Enumerable.Range(0, 8193).Select(i => new Bar(default, 0, 1, -1, (i % 101 - 50) / 8d, 1)).ToArray();
            // Nonzero decay remembers pre-window state: never restart it at worker boundaries.
            foreach (bool maximum in new[] { false, true })
            {
                await CompareRoutes(ordinary, [new DecayingWindowExtreme(20, maximum, .5)]);
                await CompareRoutes(ordinary, [new DecayingWindowExtreme(20, maximum, .5), new DecayingWindowExtreme(7, !maximum)]);
            }
        }
        finally { CpuParallelSettings.MaxDegreeOfParallelism = previous; }
    }

    [Fact]
    public async Task IndexWorkersKeepAbsoluteNewestTiesAndDifferentLengthSharedSeeds()
    {
        int previous = CpuParallelSettings.MaxDegreeOfParallelism;
        try
        {
            CpuParallelSettings.MaxDegreeOfParallelism = 8;
            foreach (bool tied in new[] { false, true })
            {
                var bars = Enumerable.Range(0, 8193).Select(i => new Bar(default, 0, 1, -1,
                    tied ? i % 2 == 0 ? -0d : 0d : (i % 53 - 26) / 8d, 1)).ToArray();
                IIndicator[] indices = [new WindowExtremeIndex(3), new WindowExtremeIndex(20, true)];
                await CompareRoutes(bars, indices);
                await CompareRoutes(bars, [indices[0], indices[1], new DecayingWindowExtreme(31)]);
                using var run = await Build(bars, indices).BuildAsync();
                foreach (WindowExtremeIndex index in indices)
                    Assert.Equal(WindowExtremeReference.Indices(bars, index.Period, index.Maximum), run[index].ToArray());
                bars[1023] = new Bar(default, 0, 1, -1, 0, double.NaN);
                await CompareRoutes(bars, indices);
            }
            await CompareRoutes([], [new WindowExtremeIndex(3)]);
        }
        finally { CpuParallelSettings.MaxDegreeOfParallelism = previous; }
    }

    [Theory]
    [InlineData(1)] [InlineData(2)] [InlineData(20)] [InlineData(32)] [InlineData(33)]
    public async Task IndexExpiryRescansPreserveMonotoneAndRepeatedWinners(int period)
    {
        int previous = CpuParallelSettings.MaxDegreeOfParallelism;
        try
        {
            CpuParallelSettings.MaxDegreeOfParallelism = 8;
            foreach (int shape in new[] { 0, 1, 2 })
            {
                var bars = Enumerable.Range(0, 8193).Select(i => new Bar(default, 0, 1, -1,
                    shape == 0 ? i : shape == 1 ? -i : (i / 2) % (period + 1), 1)).ToArray();
                IIndicator[] indices = [new WindowExtremeIndex(period), new WindowExtremeIndex(period, true)];
                foreach (var index in indices) await CompareRoutes(bars, [index]);
                // The shared seed is longer than either small index window.
                await CompareRoutes(bars, [indices[0], indices[1], new WindowExtremeIndex(65)]);
                using var run = await Build(bars, indices).BuildAsync();
                foreach (WindowExtremeIndex index in indices)
                    Assert.Equal(WindowExtremeReference.Indices(bars, period, index.Maximum), run[index].ToArray());
            }
        }
        finally { CpuParallelSettings.MaxDegreeOfParallelism = previous; }
    }

    [Theory]
    [InlineData(511, false)] [InlineData(512, false)] [InlineData(513, false)] [InlineData(1025, false)]
    [InlineData(511, true)] [InlineData(512, true)] [InlineData(513, true)] [InlineData(1025, true)]
    public async Task WeightedAndWilliamsWorkerThresholdPreservesSingleMixedAndFailureRoutes(int count, bool weighted)
    {
        int previous = CpuParallelSettings.MaxDegreeOfParallelism;
        try
        {
            var bars = Enumerable.Range(0, count).Select(i => new Bar(default, 0,
                i % 31 - 14, i % 31 - 16, i % 31 - 15, 1)).ToArray();
            foreach (int workers in new[] { 1, 8 })
            {
                CpuParallelSettings.MaxDegreeOfParallelism = workers;
                foreach (int period in new[] { 1, 20 })
                {
                    IIndicator indicator = weighted ? new Wma(period) : new WilliamsR(period);
                    await CompareRoutes(bars, [indicator]);
                    await CompareRoutes(bars, [indicator, new HighestHigh(7)]);
                }
                var saved = bars[0];
                bars[0] = new Bar(default, 0, double.Epsilon, 0, double.MaxValue, 1);
                IIndicator extreme = weighted ? new Wma(20) : new WilliamsR(20);
                await CompareRoutes(bars, [extreme]);
                bars[^1] = new Bar(default, 0, 1, 0, 1, double.NaN);
                await CompareRoutes(bars, [extreme, new HighestHigh(7)]);
                bars[0] = saved;
                int last = bars.Length - 1;
                bars[last] = new Bar(default, 0, last % 31 - 14, last % 31 - 16, last % 31 - 15, 1);
            }
        }
        finally { CpuParallelSettings.MaxDegreeOfParallelism = previous; }
    }

    [Theory]
    [InlineData(1)] [InlineData(19)] [InlineData(20)] [InlineData(21)]
    [InlineData(63)] [InlineData(64)] [InlineData(65)] [InlineData(512)]
    public async Task WeightedCertificatePromotesPriorExactTotalsBeforeFirstUnsupportedValue(int transition)
    {
        int previous = CpuParallelSettings.MaxDegreeOfParallelism;
        try
        {
            foreach (int workers in new[] { 1, 8 })
            foreach (double rejected in new[] { .1, double.Epsilon, double.MaxValue, -double.MaxValue })
            {
                CpuParallelSettings.MaxDegreeOfParallelism = workers;
                var bars = Enumerable.Range(0, 1025).Select(i => new Bar(default, 0, 1, -1,
                    i % 7 == 0 ? -0d : (i % 31 - 15) / 8d, 1)).ToArray();
                bars[transition] = new Bar(default, 0, 1, -1, rejected, 1);
                await CompareRoutes(bars, [new Wma(20)]);
                await CompareRoutes(bars, [new Wma(20), new HighestHigh(7)]);
            }
        }
        finally { CpuParallelSettings.MaxDegreeOfParallelism = previous; }
    }

    [Theory]
    [InlineData(2)] [InlineData(65535)] [InlineData(65536)]
    public async Task WeightedCertificateHandlesExponentAndPeriodBoundaries(int period)
    {
        double[] values = [Math.ScaleB(1, -512), -Math.ScaleB(1, -512), Math.ScaleB(1, -513),
            Math.ScaleB(1, 500), Math.ScaleB(1, 501), 0, -0d];
        foreach (double value in values)
        {
            var bars = Enumerable.Range(0, 65).Select(i => new Bar(default, 0, 1, -1,
                i % 2 == 0 ? value : -value, 1)).ToArray();
            await CompareRoutes(bars, [new Wma(period)]);
        }
    }

    private static async Task CompareRoutes(Bar[] bars, IIndicator[] indicators)
    {
        foreach (var history in Enum.GetValues<IndicatorHistoryMode>())
        {
            IIndicatorRun? expected = null, actual = null;
            var expectedError = await Record.ExceptionAsync(async () => expected =
                await Build(bars, indicators).ConfigureHistory(history).ConfigureBehavior(_ => { }).BuildAsync());
            var builder = Build(bars, indicators).ConfigureHistory(history);
            var actualError = await Record.ExceptionAsync(async () => actual = await builder.BuildAsync());
            using (expected) using (actual)
            {
                if (expectedError is not null)
                {
                    Assert.NotNull(actualError);
                    Assert.Equal(expectedError.GetType(), actualError.GetType());
                    Assert.Equal(expectedError.Message, actualError.Message);
                    Assert.Null(builder.LastExecution);
                }
                else
                {
                    Assert.Null(actualError);
                    foreach (var indicator in indicators) Compare(expected!, actual!, indicator);
                }
            }
        }
    }
    private static StockIndicatorBuilder Build(Bar[] bars, IIndicator[] indicators) =>
        new StockIndicatorBuilder().ConfigureSource(Bars.From(bars)).ConfigureIndicators(indicators);
    private static void Compare(IIndicatorRun expected, IIndicatorRun actual, IIndicator indicator) => Assert.Equal(
        expected[indicator].ToArray().Select(BitConverter.DoubleToInt64Bits),
        actual[indicator].ToArray().Select(BitConverter.DoubleToInt64Bits));
}

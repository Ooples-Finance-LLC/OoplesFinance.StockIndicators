using OoplesFinance.StockIndicators.Core;
using OoplesFinance.StockIndicators.Validation;

namespace OoplesFinance.StockIndicators.Tests.Unit.ValidationTests;

public sealed class SmaCpuKernelTests
{
    [Theory]
    [InlineData(2, 257)] [InlineData(20, 1025)] [InlineData(127, 513)]
    [InlineData(256, 257)] [InlineData(255, 256)]
    public void PrefixSmaMatchesIndependentRationalWindows(int period, int count)
    {
        var input = Enumerable.Range(0, count).Select(i => (i % 31 - 15) / 16d).ToArray();
        input[0] = -0d;
        SmaCpuKernel.Summarize(input, out var grid, out _, default);
        var actual = input.ToArray();
        var consumer = new SmaCpuKernel.MeanIdentity();
        bool used = SmaCpuKernel.TryProcessPrefixInPlace(actual, period, grid, ref consumer, default);
        Assert.Equal(System.Runtime.Intrinsics.Vector256.IsHardwareAccelerated, used);
        if (!used) { Assert.Equal(input, actual); return; }
        for (int i = 0; i < count; i++)
        {
            var sum = new ReferenceFraction(0);
            if (i >= period - 1)
                for (int j = i - period + 1; j <= i; j++) sum += ReferenceFraction.FromDouble(input[j]);
            double expected = (sum / new ReferenceFraction(period)).ToDouble();
            Assert.Equal(BitConverter.DoubleToInt64Bits(expected), BitConverter.DoubleToInt64Bits(actual[i]));
        }
    }

    [Fact]
    public void PrefixProofMustCoverWholeHistoryAndCancellationCannotMutateInput()
    {
        var input = Enumerable.Repeat(1d + Math.Pow(2, -48), 257).ToArray();
        SmaCpuKernel.Summarize(input, out var grid, out _, default);
        Assert.True(grid.Certifies(2));
        Assert.False(grid.Certifies(input.Length));
        var actual = input.ToArray();
        var consumer = new SmaCpuKernel.MeanIdentity();
        Assert.False(SmaCpuKernel.TryProcessPrefixInPlace(actual, 2, grid, ref consumer, default));
        Assert.Equal(input, actual);
        if (!System.Runtime.Intrinsics.Vector256.IsHardwareAccelerated) return;
        Array.Fill(input, .25d);
        actual = input.ToArray();
        SmaCpuKernel.Summarize(input, out grid, out _, default);
        using var cancellation = new CancellationTokenSource(); cancellation.Cancel();
        Assert.ThrowsAny<OperationCanceledException>(() => SmaCpuKernel.TryProcessPrefixInPlace(actual, 2, grid, ref consumer, cancellation.Token));
        Assert.Equal(input, actual);
    }

    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(2)]
    public void PartitionedCertificatesEqualWholeSequenceCertificates(int mode)
    {
        var input = Enumerable.Range(0, 65537).Select(i => mode == 0 ? (i % 31 - 15) / 16d
            : mode == 1 ? 100 + i % 19 / 100d : i == 32768 ? double.Epsilon : .25d).ToArray();
        SmaCpuKernel.Summarize(input, out var expectedGrid, out var expectedPositive, default);
        var grid = new SmaCpuKernel.GridSummary();
        var positive = new SmaCpuKernel.PositiveRangeSummary();
        for (int chunk = 0; chunk < 4; chunk++)
        {
            int start = input.Length * chunk / 4, end = input.Length * (chunk + 1) / 4;
            SmaCpuKernel.Summarize(input.AsSpan(start, end - start), out var partGrid, out var partPositive, default);
            grid.Merge(partGrid); positive.Merge(partPositive);
        }
        foreach (int period in new[] { 1, 2, 20, 4096, 65537, int.MaxValue })
        {
            Assert.Equal(expectedGrid.Certifies(period), grid.Certifies(period));
            Assert.Equal(expectedPositive.Certifies(period), positive.Certifies(period));
        }
        Assert.Equal(expectedGrid.CanRefine, grid.CanRefine);
    }

    private struct IndexedMeanConsumer(int[] visits) : SmaCpuKernel.IMeanConsumer
    {
        internal int Calls;
        public double Consume(double mean, int index)
        {
            Calls++;
            visits[index]++;
            return mean + index + .25;
        }
    }

    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(2)]
    public void InPlaceConsumersPreserveEveryIndexAndReturnUpdatedState(int mode)
    {
        foreach (int count in new[] { 0, 1, 19, 137 })
        foreach (int period in new[] { 1, 3, 20, 4096 })
        {
            var input = Enumerable.Range(0, count).Select(i => mode == 0 ? (i % 17 - 8) / 16d
                : mode == 1 ? .25 + i % 19 / 100d : (i % 17 - 8) / 10d).ToArray();
            var expected = input.ToArray();
            bool certified = mode == 0;
            bool bounded = mode == 1;
            SmaCpuKernel.ProcessInPlace(expected, period, certified, default, bounded);
            var visits = new int[count];
            var consumer = new IndexedMeanConsumer(visits);
            SmaCpuKernel.ProcessInPlace(input, period, certified, ref consumer, default, bounded);
            Assert.Equal(count, consumer.Calls);
            Assert.All(visits, n => Assert.Equal(1, n));
            for (int i = 0; i < count; i++)
                Assert.Equal(BitConverter.DoubleToInt64Bits(expected[i] + i + .25), BitConverter.DoubleToInt64Bits(input[i]));
        }
    }

    private struct Deviation : SmaCpuKernel.IConsumer
    {
        internal int Calls;
        public double Consume(double input, double mean, int index)
        {
            Calls++;
            return input - mean;
        }
    }

    private readonly struct DoubleReader : SmaCpuKernel.IReader<double>
    {
        public double Read(in double value) => value;
    }

    [Theory]
    [InlineData(1, 1)]
    [InlineData(2, 7)]
    [InlineData(20, 1)]
    [InlineData(20, 31)]
    [InlineData(int.MaxValue, 64)]
    public void FusedConsumerMatchesIndependentWindowsAcrossBlocks(int period, int blockSize)
    {
        var input = Enumerable.Range(0, 129).Select(i => (i % 17 - 8) / 4d).ToArray();
        input[0] = -0d;
        var owned = new double[input.Length];
        var output = new double[input.Length];
        var state = new SmaCpuKernel.State(period, input.Length);
        var reader = new DoubleReader();
        var consumer = new Deviation();
        for (var offset = 0; offset < input.Length; offset += blockSize)
        {
            var size = Math.Min(blockSize, input.Length - offset);
            SmaCpuKernel.Process<double, DoubleReader, Deviation>(input.AsSpan(offset, size),
                owned.AsSpan(offset, size), output.AsSpan(offset, size), ref state, ref reader, ref consumer);
        }
        Assert.True(state.Certified);
        Assert.Equal(input.Length, consumer.Calls);
        Assert.Equal(input, owned);
        for (var i = 0; i < input.Length; i++)
        {
            var sum = new ReferenceFraction(0);
            if (i >= period - 1)
                for (var j = i - period + 1; j <= i; j++) sum += ReferenceFraction.FromDouble(input[j]);
            var mean = period == 1 ? input[i] : (sum / new ReferenceFraction(period)).ToDouble();
            Assert.Equal(BitConverter.DoubleToInt64Bits(input[i] - mean), BitConverter.DoubleToInt64Bits(output[i]));
        }
        if (period > 1)
        {
            var contiguous = new double[input.Length];
            consumer = new Deviation();
            Assert.True(SmaCpuKernel.TryProcess(input, contiguous, period, ref consumer));
            Assert.Equal(output, contiguous);
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void GuardedConsumerRunsOncePerBarWithoutIntermediateMeanArray(bool extreme)
    {
        var input = Enumerable.Range(0, 2051).Select(i => .1 + i % 19 / 100d).ToArray();
        if (extreme) { input[1024] = 1e100; input[1025] = -1e100; input[^1] = double.Epsilon; }
        var expected = new double[input.Length];
        MovingAverageCore.SimpleMovingAverage(input, expected, 20);
        var consumer = new CapturedMean(input.Length);
        var reader = new DoubleReader();
        SmaCpuKernel.ProcessGuarded<double, DoubleReader, CapturedMean>(input, Span<double>.Empty,
            20, ref reader, ref consumer);
        Assert.Equal(input.Length, consumer.Calls);
        Assert.Equal(expected.Select(BitConverter.DoubleToInt64Bits), consumer.Values.Select(BitConverter.DoubleToInt64Bits));
    }

    public static IEnumerable<object[]> InPlaceCases =>
        new[] { 1, 2, 3, 20, 64, 129, 130, int.MaxValue }
            .SelectMany(period => Enumerable.Range(0, 4).Select(mode => new object[] { period, mode }));

    [Theory, MemberData(nameof(InPlaceCases))]
    public void InPlacePreservesGuardedAndCertifiedResultsAcrossWindowBoundaries(int period, int mode)
    {
        var input = Enumerable.Range(0, 129).Select(i => mode == 0
            ? (i % 17 - 8) / 4d : 100 + i % 19 / 100d).ToArray();
        input[0] = -0d;
        if (mode == 2) { input[21] = 1e100; input[22] = -1e100; input[^1] = double.Epsilon; }
        if (mode == 3) { input[19] = double.MaxValue; input[20] = -double.MaxValue; }
        var expected = new double[input.Length];
        MovingAverageCore.SimpleMovingAverage(input, expected, period);
        var actual = (double[])input.Clone();
        SmaCpuKernel.ProcessInPlace(actual, period, mode == 0, default);
        Assert.Equal(expected.Select(BitConverter.DoubleToInt64Bits), actual.Select(BitConverter.DoubleToInt64Bits));
    }

    [Fact]
    public void InPlaceCancellationDoesNotPublishOrMutateInput()
    {
        var input = new[] { 1d, 2d, 3d };
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        Assert.ThrowsAny<OperationCanceledException>(() => SmaCpuKernel.ProcessInPlace(input, 2, true, cancellation.Token));
        Assert.Equal(new[] { 1d, 2d, 3d }, input);
    }

    public static IEnumerable<object[]> PositiveCases => new[] { 2, 3, 20, 127, 1000, 4096 }
        .SelectMany(period => new[] { -256, -1, 0, 255 }.Select(exponent => new object[] { period, exponent }));

    [Theory, MemberData(nameof(PositiveCases))]
    public void ParallelPositivePreservesGuardedBitsAtPartitionBoundaries(int period, int exponent)
    {
        foreach (int participants in new[] { 1, 2, 3, 4, 6, 8 })
        foreach (int tail in new[] { -1, 0, 1 })
        {
            var random = new Random(793);
            var input = Enumerable.Range(0, period * 10 + tail)
                .Select(_ => Math.ScaleB(1 + random.NextDouble(), exponent)).ToArray();
            var expected = new double[input.Length];
            var reader = new SmaCpuKernel.DoubleReader();
            var identity = new SmaCpuKernel.Identity();
            SmaCpuKernel.ProcessGuarded<double, SmaCpuKernel.DoubleReader, SmaCpuKernel.Identity>(
                input, expected, period, ref reader, ref identity);
            var actual = input.ToArray();
            SmaCpuKernel.ProcessRebasedParallel(actual, period, participants, default);
            Assert.Equal(expected.Select(BitConverter.DoubleToInt64Bits), actual.Select(BitConverter.DoubleToInt64Bits));
        }
    }

    [Fact]
    public void ParallelPositiveRejectsInvalidPartitionsAndPreCancellationWithoutMutation()
    {
        var input = Enumerable.Repeat(1.1, 100).ToArray();
        var original = input.ToArray();
        Assert.Throws<ArgumentOutOfRangeException>(() => SmaCpuKernel.ProcessRebasedParallel(input, 0, 4, default));
        Assert.Throws<ArgumentOutOfRangeException>(() => SmaCpuKernel.ProcessRebasedParallel(input, 20, 5, default));
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        Assert.ThrowsAny<OperationCanceledException>(() => SmaCpuKernel.ProcessRebasedParallel(input, 20, 4, cancellation.Token));
        Assert.Equal(original, input);
    }

    [Theory]
    [InlineData(2)] [InlineData(20)] [InlineData(127)] [InlineData(4096)]
    public void ParallelCertifiedSignedGridPreservesExactWindows(int period)
    {
        foreach (int participants in new[] { 1, 2, 3, 4, 6, 8 })
        {
            var input = Enumerable.Range(0, period * 9 + 1).Select(i => (i % 31 - 15) / 16d).ToArray();
            input[0] = -0d;
            SmaCpuKernel.Summarize(input, out var grid, out _, default);
            Assert.True(grid.Certifies(period));
            var actual = input.ToArray();
            SmaCpuKernel.ProcessRebasedParallel(actual, period, participants, default);
            long units = 0;
            for (int i = 0; i < input.Length; i++)
            {
                units += (long)(input[i] * 16);
                if (i >= period) units -= (long)(input[i - period] * 16);
                double expected = i < period - 1 ? 0 : (units / 16d) / period;
                Assert.Equal(BitConverter.DoubleToInt64Bits(expected), BitConverter.DoubleToInt64Bits(actual[i]));
            }
        }
    }

    [Theory, MemberData(nameof(PositiveCases))]
    public void PositiveRangeProofPreservesEveryGuardedBit(int period, int exponent)
    {
        var random = new Random(173);
        var scale = Math.Pow(2, exponent);
        var input = Enumerable.Range(0, period * 9 + 17).Select(i =>
            scale * (i % 7 == 0 ? 1 : i % 7 == 1 ? 2 : 1 + random.NextDouble())).ToArray();
        var proof = new SmaCpuKernel.PositiveRangeSummary();
        foreach (var value in input) proof.Include(value);
        Assert.True(proof.Certifies(period));
        var expected = new double[input.Length];
        var reader = new SmaCpuKernel.DoubleReader();
        var identity = new SmaCpuKernel.Identity();
        SmaCpuKernel.ProcessGuarded<double, SmaCpuKernel.DoubleReader, SmaCpuKernel.Identity>(
            input, expected, period, ref reader, ref identity);
        var actual = (double[])input.Clone();
        SmaCpuKernel.ProcessInPlace(actual, period, false, default, boundedPositive: true);
        Assert.Equal(expected.Select(BitConverter.DoubleToInt64Bits), actual.Select(BitConverter.DoubleToInt64Bits));
    }

    [Theory]
    [InlineData(-1, 1, 20)] [InlineData(0, 1, 20)]
    [InlineData(1, 2.0000000000000004, 20)] [InlineData(1e-100, 1e-100, 20)]
    [InlineData(1e100, 1e100, 20)] [InlineData(1, 2, 4097)]
    [InlineData(1, 2, 1)] [InlineData(double.NaN, 1, 20)]
    [InlineData(1, double.PositiveInfinity, 20)]
    public void PositiveRangeProofRejectsInputsOutsideItsBound(double first, double last, int period)
    {
        var proof = new SmaCpuKernel.PositiveRangeSummary();
        proof.Include(first); proof.Include(last);
        Assert.False(proof.Certifies(period));
    }

    [Theory]
    [InlineData(2)] [InlineData(3)] [InlineData(20)] [InlineData(127)]
    public void CompactPositiveResultsPreserveFirstWindowAndRebuildBoundaries(int period)
    {
        foreach (int count in new[] { 0, 1, period - 1, period, period + 1,
                     2 * period - 1, 2 * period, 2 * period + 1, 3 * period + 1,
                     5 * period - 1, 5 * period, 5 * period + 1,
                     9 * period - 1, 9 * period, 9 * period + 1 })
        {
            var input = Enumerable.Range(0, count).Select(i => 100 + i % 19 / 100d).ToArray();
            var expected = new double[count];
            var reader = new SmaCpuKernel.DoubleReader();
            var identity = new SmaCpuKernel.Identity();
            SmaCpuKernel.ProcessGuarded<double, SmaCpuKernel.DoubleReader, SmaCpuKernel.Identity>(
                input, expected, period, ref reader, ref identity);
            var actual = (double[])input.Clone();
            SmaCpuKernel.ProcessInPlace(actual, period, false, default, boundedPositive: true);
            Assert.Equal(expected.Select(BitConverter.DoubleToInt64Bits), actual.Select(BitConverter.DoubleToInt64Bits));
        }
    }

    [Fact]
    public void BatchedCertificationMatchesScalarProofsAcrossLanesAndTails()
    {
        double[] edges = { 0d, -0d, double.Epsilon, -double.Epsilon, double.MaxValue,
            -double.MaxValue, 1, -1, .1, 2, Math.BitIncrement(2), Math.Pow(2, -512),
            Math.Pow(2, 500), Math.Pow(2, -256), Math.Pow(2, 256), double.NaN,
            double.PositiveInfinity, double.NegativeInfinity };
        var random = new Random(317);
        for (int count = 0; count <= 65; count++)
        {
            Check(Enumerable.Repeat(0d, count).ToArray());
            Check(Enumerable.Repeat(-0d, count).ToArray());
            Check(Enumerable.Range(0, count).Select(i => (i % 17 - 8) / 4d).ToArray());
            Check(Enumerable.Range(0, count).Select(i => 100 + i % 19 / 100d).ToArray());
            Check(Enumerable.Range(0, count).Select(_ =>
                BitConverter.Int64BitsToDouble(random.NextInt64())).ToArray());
            foreach (double edge in edges)
            for (int position = 0; position < count; position++)
            {
                var values = Enumerable.Repeat(1d, count).ToArray();
                values[position] = edge;
                Check(values);
            }
        }

        static void Check(double[] values)
        {
            var expectedGrid = new SmaCpuKernel.GridSummary();
            var expectedPositive = new SmaCpuKernel.PositiveRangeSummary();
            foreach (double value in values) { expectedGrid.Include(value); expectedPositive.Include(value); }
            SmaCpuKernel.Summarize(values, out var grid, out var positive, default);
            Assert.Equal(expectedGrid.CanRefine, grid.CanRefine);
            foreach (int period in new[] { 1, 2, 3, 20, 4096, 4097, int.MaxValue })
            {
                Assert.Equal(expectedGrid.Certifies(period), grid.Certifies(period));
                Assert.Equal(expectedPositive.Certifies(period), positive.Certifies(period));
            }
        }
    }

    [Fact]
    public void BatchedCertificationObservesCancellationEvenForEmptyInput()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        Assert.ThrowsAny<OperationCanceledException>(() =>
            SmaCpuKernel.Summarize(Array.Empty<double>(), out _, out _, cancellation.Token));
    }

    private struct CapturedMean(int count) : SmaCpuKernel.IConsumer
    {
        internal readonly double[] Values = new double[count];
        internal int Calls;
        public double Consume(double input, double mean, int index) { Calls++; Values[index] = mean; return mean; }
    }

    [Fact]
    public void RejectedBatchCannotInvokeConsumerOrModifyOutput()
    {
        var input = Enumerable.Repeat(1d, 128).Append(double.Epsilon).ToArray();
        var output = Enumerable.Repeat(123d, input.Length).ToArray();
        var consumer = new Deviation();
        Assert.False(SmaCpuKernel.TryProcess(input, output, 20, ref consumer));
        Assert.Equal(0, consumer.Calls);
        Assert.All(output, value => Assert.Equal(123d, value));
        Assert.False(SmaCpuKernel.TryProcess(input, input, 20, ref consumer));
        Assert.Equal(0, consumer.Calls);
    }
}

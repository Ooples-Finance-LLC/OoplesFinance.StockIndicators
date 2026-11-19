using System.Reflection;
using System.Reflection.Emit;
using AiDotNet.Tensors.Helpers;
using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Validation;

namespace OoplesFinance.StockIndicators.Tests.Unit.ModelsTests;

[Collection("IndicatorValuesDispatch")]
public sealed class CandleTopologyReplayTests
{
    private static IndicatorBase Create(int kind, int first = 10, int second = 10) => kind switch
    {
        0 => new StalledCandle(first, second, first, second),
        1 => new IdenticalThreeCrowsCandle(first, second),
        _ => new ThreeStarsInSouthCandle(first, second, first)
    };

    private static Bar Background(int kind) => kind switch
    {
        0 => new(default, 99, 105, 95, 100, 1),
        1 => new(default, 100, 103, 97, 100, 1),
        _ => new(default, 100, 105, 95, 99, 1)
    };

    private static Bar[] Signal(int kind) => kind switch
    {
        0 => [new(default, 100, 104.5, 99, 104, 1), new(default, 104, 109.05, 103, 109, 1), new(default, 109.1, 110, 108, 109.2, 1)],
        1 => [new(default, 100, 103, 98.99, 99, 1), new(default, 99, 102, 97.99, 98, 1), new(default, 98, 101, 96.99, 97, 1)],
        _ => [new(default, 100, 101, 90, 96, 1), new(default, 97, 98, 92, 95, 1), new(default, 95.5, 95.6, 95.35, 95.4, 1)]
    };

    private static Bar[] Data(int kind, int count)
    {
        var bars = Enumerable.Repeat(Background(kind), count).ToArray();
        foreach (int end in new[] { 14, 31, 32, 33, 63, 64, 65, 127, 128, 129, 255, 511, 512, 513, count - 1 })
            if (end >= 2 && end < count) Signal(kind).CopyTo(bars, end - 2);
        return bars;
    }

    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(2)]
    public async Task SparseDenseSparseAndUnequalHalosMatchIndependentReference(int kind)
    {
        foreach (var (first, second) in new[] { (10, 10), (1, 1), (1, 2), (64, 3), (3, 64) })
        {
            var bars = Data(kind, 1025);
            for (int i = 300; i < 600; i++)
                bars[i] = kind == 1 ? new(default, 1000 - i, 1002 - i, 998.99 - i, 999 - i, 1) : Signal(kind)[i % 3];
            await Check(bars, Create(kind, first, second));
        }
        var positive = Enumerable.Repeat(Background(kind), 80).Concat(Signal(kind)).ToArray();
        var state = (IIndicatorState)Create(kind).CreateState();
        Assert.Contains(positive.Select(b => state.Update(b)), x => x != 0);
        foreach (int count in new[] { 0, 1, 31, 32, 33, 127, 128, 129, 511, 512, 513 })
            await Check(Data(kind, count), Create(kind));
        // Exercise actual worker regions, their captured halos, and partial tails.
        await Check(Data(kind, 10001), Create(kind, 64, 3));
    }

    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(2)]
    public async Task UnorderedSignedAndOverflowRangesRetainOriginalFallbacks(int kind)
    {
        var random = new Random(20261119);
        var bars = Enumerable.Range(0, 1025).Select(_ => new Bar(default,
            random.Next(-256, 257) / 8d, random.Next(-256, 257) / 8d,
            random.Next(-256, 257) / 8d, random.Next(-256, 257) / 8d, 1)).ToArray();
        await Check(bars, Create(kind));
        var extreme = new Bar[]
        {
            new(default, double.MaxValue, double.MaxValue, -double.MaxValue, -double.MaxValue, 1),
            new(default, -double.MaxValue, double.MaxValue, -double.MaxValue, double.MaxValue, 1),
            new(default, 0, double.Epsilon, -double.Epsilon, -0d, 1),
            new(default, -0d, 0, -0d, 0, 1),
            new(default, double.Epsilon, double.Epsilon, -double.Epsilon, -double.Epsilon, 1)
        };
        for (int i = 0; i < bars.Length; i++) bars[i] = extreme[i % extreme.Length];
        Signal(kind).CopyTo(bars, 511);
        await Check(bars, Create(kind));
        await Check(Data(kind, 129), Create(kind, 65, 1));
        await Check(Data(kind, 129), Create(kind, int.MaxValue - 2, 1));
        Assert.Throws<ArgumentOutOfRangeException>(() => Create(kind, 0, 1));
    }

    [Fact]
    public async Task InclusiveEqualOpenAndStrictShadowTiesKeepOriginalInequalities()
    {
        foreach (int direction in new[] { -1, 0, 1 })
        {
            double secondOpen = direction < 0 ? Math.BitDecrement(100d) : direction > 0 ? Math.BitIncrement(100d) : 100d;
            double thirdOpen = direction < 0 ? Math.BitDecrement(99d) : direction > 0 ? Math.BitIncrement(99d) : 99d;
            var bars = Enumerable.Repeat(new Bar(default, 100, 110, 90, 100, 1), 1025).ToArray();
            foreach (int start in new[] { 80, 510, 990 })
            {
                bars[start] = new(default, 101, 118.9, 98.9, 99, 1);
                bars[start + 1] = new(default, secondOpen, 117.9, 97.9, 98, 1);
                bars[start + 2] = new(default, thirdOpen, 116.9, 96.9, 97, 1);
            }
            await Check(bars, Create(1));
        }
        foreach (double scale in new[] { 1d, Math.ScaleB(1d, -1022), Math.ScaleB(1d, -1000), Math.ScaleB(1d, 900) })
        foreach (double high in new[] { Math.BitDecrement(116d), 116d, Math.BitIncrement(116d) })
        {
            var bars = Enumerable.Repeat(new Bar(default, 99, 110, 90, 100, 1), 1025).ToArray();
            bars[510] = new(default, 100, 119, 99, 104, 1);
            bars[511] = new(default, 108, high, 96, 114, 1);
            bars[512] = new(default, 114, 120, 100, 114.1, 1);
            bars = bars.Select(b => new Bar(b.Time, b.Open * scale, b.High * scale, b.Low * scale, b.Close * scale, b.Volume)).ToArray();
            await Check(bars, Create(0));
        }
    }

    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(2)]
    public void ResetAndWarmReplayRestoreLatentWindowStateAndFutureSignals(int kind)
    {
        foreach (var (first, second) in new[] { (1, 2), (10, 10), (64, 3), (3, 64) })
        {
            var owner = Create(kind, first, second);
            var bars = Data(kind, 1025);
            var serial = (IIndicatorState)owner.CreateState();
            var replay = (IIndicatorState)owner.CreateState();
            for (int row = 0; row < bars.Length; row++)
            {
                if (row >= owner.WarmupBars && row % 31 == 0)
                {
                    replay.Reset();
                    for (int i = row - owner.WarmupBars; i < row; i++) replay.Update(in bars[i]);
                    AssertLatentState(serial, replay);
                }
                Assert.Equal(BitConverter.DoubleToInt64Bits(serial.Update(in bars[row])),
                    BitConverter.DoubleToInt64Bits(replay.Update(in bars[row])));
            }
        }
    }

    private static void AssertLatentState(IIndicatorState expected, IIndicatorState actual)
    {
        foreach (var field in expected.GetType().GetFields(BindingFlags.Instance | BindingFlags.NonPublic))
        {
            object? left = field.GetValue(expected), right = field.GetValue(actual);
            if (left is ExhaustionWindow window)
            {
                var other = Assert.IsType<ExhaustionWindow>(right);
                for (int lag = 0; lag < 3; lag++) AssertExactEqual(window.At(lag), other.At(lag));
                var history = typeof(ExhaustionWindow).GetField("_history", BindingFlags.Instance | BindingFlags.NonPublic)!;
                Assert.Equal(((Queue<Bar>)history.GetValue(window)!).ToArray(), ((Queue<Bar>)history.GetValue(other)!).ToArray());
            }
            else if (left is ExactMeanAccumulator sum)
                AssertExactEqual(sum, Assert.IsType<ExactMeanAccumulator>(right));
            else if (left is Queue<Bar> queue)
                Assert.Equal(queue.ToArray(), Assert.IsType<Queue<Bar>>(right).ToArray());
            else if (field.Name is "_seen" or "_before" or "_first" or "_second") Assert.Equal(left, right);
        }
    }

    private static void AssertExactEqual(ExactMeanAccumulator expected, ExactMeanAccumulator actual)
    {
        expected.Subtract(actual);
        Assert.True(expected.IsExactlyZero);
    }

    [Fact]
    public void CompiledEnclosuresAndSignedWindowThresholdsContainIndependentExactFractions()
    {
        Func<double, double, double> Bind(string name) => typeof(ValuesBarExecution)
            .GetMethod(name, BindingFlags.Static | BindingFlags.NonPublic)!.CreateDelegate<Func<double, double, double>>();
        var lower = Bind("TopologyLowerDifference");
        var upper = Bind("TopologyUpperDifference");
        var absoluteLower = Bind("TopologyAbsoluteLowerDifference");
        var threshold = Bind("TopologyUpperThreshold");
        var random = new Random(934510);
        double[] special = [0, -0d, double.Epsilon, -double.Epsilon, double.MaxValue, -double.MaxValue,
            Math.BitDecrement(1d), 1d, Math.BitIncrement(1d), Math.ScaleB(1d, -1022), -Math.ScaleB(1d, 900)];
        var pool = special.Concat(Enumerable.Range(0, 256).Select(_ => Math.ScaleB(random.NextDouble() * 2 - 1, random.Next(-1073, 1024)))).ToArray();
        int overflow = 0, finite = 0;
        void Check(double left, double right)
        {
            var exact = ReferenceFraction.FromDouble(left) - ReferenceFraction.FromDouble(right);
            double lo = lower(left, right), hi = upper(left, right);
            Assert.False(double.IsNaN(lo)); Assert.False(double.IsNaN(hi));
            if (double.IsFinite(lo)) Assert.True(ReferenceFraction.FromDouble(lo).CompareTo(exact) <= 0);
            else Assert.Equal(double.NegativeInfinity, lo);
            if (double.IsFinite(hi)) Assert.True(ReferenceFraction.FromDouble(hi).CompareTo(exact) >= 0);
            else Assert.Equal(double.PositiveInfinity, hi);
            Assert.True(ReferenceFraction.FromDouble(absoluteLower(left, right)).CompareTo(exact.Abs()) <= 0);
            if (!double.IsFinite(left - right)) overflow++; else finite++;
        }
        foreach (double left in special) foreach (double right in special) Check(left, right);
        for (int i = 0; i < 3000; i++) Check(pool[random.Next(pool.Length)], pool[random.Next(pool.Length)]);
        foreach (int period in new[] { 1, 2, 5, 10, 64 })
        for (int repetition = 0; repetition < 80; repetition++)
        {
            var exactSum = new ReferenceFraction(0);
            double maximum = double.NegativeInfinity;
            for (int i = 0; i < period; i++)
            {
                double left = pool[random.Next(pool.Length)], right = pool[random.Next(pool.Length)];
                exactSum += ReferenceFraction.FromDouble(left) - ReferenceFraction.FromDouble(right);
                maximum = Math.Max(maximum, upper(left, right));
            }
            foreach (int factor in new[] { 5, 10, 20 })
            {
                double bound = threshold(maximum, factor);
                if (double.IsFinite(bound))
                    Assert.True(ReferenceFraction.FromDouble(bound).CompareTo(exactSum / new ReferenceFraction(period * factor)) >= 0);
                else Assert.Equal(double.PositiveInfinity, bound);
            }
        }
        Assert.True(overflow > 0); Assert.True(finite > 0);
    }

    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(2)]
    public async Task DispatchBoundarySizesKeepExactOutputsAndOwnedHistory(int kind)
    {
        foreach (int count in new[] { 8191, 8192, 8193 }) await Check(Data(kind, count), Create(kind));
    }

    private delegate void ReplayRegion(Bar[] source, double[] output, OwnedBarBuffer? owned,
        int start, int end, int window, ReadOnlySpan<Bar> seed, ReadOnlySpan<Bar> tail,
        CancellationToken cancellation, IIndicator indicator);

    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(2)]
    public void CapturedSeedAndTailOverrideMutatedSourceAndRemainOwned(int kind)
    {
        // Spans cannot be boxed for MethodInfo.Invoke. This test-only bridge
        // invokes the actual private method and discards its private region type.
        var method = typeof(ValuesBarExecution).GetMethod("ComputeCandleTopologyReplay", BindingFlags.Static | BindingFlags.NonPublic)!;
        var bridge = new DynamicMethod("InvokeCapturedCandleRegion", typeof(void),
            method.GetParameters().Select(p => p.ParameterType).ToArray(), typeof(ValuesBarExecution), true);
        var il = bridge.GetILGenerator();
        for (short i = 0; i < method.GetParameters().Length; i++) il.Emit(OpCodes.Ldarg, i);
        il.Emit(OpCodes.Call, method); il.Emit(OpCodes.Pop); il.Emit(OpCodes.Ret);
        var replay = bridge.CreateDelegate<ReplayRegion>();
        var owner = Create(kind, 64, 3);
        var original = Data(kind, 1025);
        // Put a real possible pattern inside the captured tail; the poisoned
        // array slots must never be used for either filtering or state replay.
        Signal(kind).CopyTo(original, original.Length - 3);
        var serial = (IIndicatorState)owner.CreateState();
        var expected = original.Select(b => serial.Update(b)).ToArray();
        const int start = 511;
        int lookback = owner.WarmupBars;
        var seed = original.AsSpan(start - lookback, lookback).ToArray();
        var tail = original.AsSpan(original.Length - lookback, lookback).ToArray();
        var source = original.ToArray();
        var poisoned = new Bar(default, double.NaN, double.NaN, double.NaN, double.NaN, double.NaN);
        Array.Fill(source, poisoned, start - lookback, lookback);
        Array.Fill(source, poisoned, source.Length - lookback, lookback);
        var output = Enumerable.Repeat(double.NaN, original.Length).ToArray();
        var owned = new OwnedBarBuffer(original.Length);
        replay(source, output, owned, start, source.Length, lookback + 1, seed, tail, CancellationToken.None, owner);
        Array.Clear(source); Array.Clear(seed); Array.Clear(tail);
        Assert.Equal(expected.Skip(start).Select(BitConverter.DoubleToInt64Bits), output.Skip(start).Select(BitConverter.DoubleToInt64Bits));
        Assert.All(output.Take(start), value => Assert.True(double.IsNaN(value)));
        for (int row = start; row < original.Length; row++) Assert.Equal(original[row], owned[row]);
    }

    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(2)]
    public async Task ProvenZeroRowsStillValidateEveryFieldAndObserveCancellation(int kind)
    {
        var bars = Enumerable.Repeat(new Bar(default, 5, 10, 0, 5, 1), 1025).ToArray();
        var owner = Create(kind);
        var builder = new StockIndicatorBuilder().ConfigureSource(Bars.From(bars)).ConfigureIndicators(owner);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => builder.BuildAsync(new CancellationToken(true)));
        foreach (int index in new[] { 0, 31, 32, 33, 511, 512, 700, 1024 })
        foreach (int field in Enumerable.Range(0, 5))
        {
            var original = bars[index];
            double[] values = [original.Open, original.High, original.Low, original.Close, original.Volume];
            values[field] = field % 2 == 0 ? double.NaN : double.PositiveInfinity;
            bars[index] = new(original.Time, values[0], values[1], values[2], values[3], values[4]);
            await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => builder.BuildAsync());
            bars[index] = original;
        }
        await Check(bars, owner);
    }

    private static async Task Check(Bar[] bars, IndicatorBase owner)
    {
        var original = (IIndicatorState)owner.CreateState();
        var expected = bars.Select(b => original.Update(b)).ToArray();
        foreach (var rule in ((IIndicatorValidationContract)owner).ValidationRules)
            rule.Check(new IndicatorValidationContext("Candle topology replay independent reference", bars, [expected], 0));
        int prior = CpuParallelSettings.MaxDegreeOfParallelism;
        try
        {
            foreach (int workers in new[] { 1, 2, 8 })
            foreach (var mode in Enum.GetValues<IndicatorHistoryMode>())
            {
                CpuParallelSettings.MaxDegreeOfParallelism = workers;
                var input = bars.ToArray();
                using var run = await new StockIndicatorBuilder().ConfigureSource(Bars.From(input)).ConfigureIndicators(owner).ConfigureHistory(mode).BuildAsync();
                Array.Clear(input); run.Dispose();
                Assert.Equal(expected.Select(BitConverter.DoubleToInt64Bits), run[owner].ToArray().Select(BitConverter.DoubleToInt64Bits));
                if (bars.Length != 0) Assert.Equal(bars[^1], run.Latest.Bar);
                if (mode == IndicatorHistoryMode.Full)
                {
                    int row = 0; await foreach (var snapshot in run) Assert.Equal(bars[row++], snapshot.Bar);
                    Assert.Equal(bars.Length, row);
                }
            }
        }
        finally { CpuParallelSettings.MaxDegreeOfParallelism = prior; }
    }
}

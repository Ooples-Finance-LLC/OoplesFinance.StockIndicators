using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using AiDotNet.Tensors.Helpers;
using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Validation;

namespace OoplesFinance.StockIndicators.Tests.Unit.ModelsTests;

[Collection("IndicatorValuesDispatch")]
public sealed class GapSideReplayTests
{
    private static readonly BindingFlags Flags = BindingFlags.Static | BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    private static Bar Background(int index) => new(new DateTime(2020, 1, 1).AddTicks(index), 0, 10, 0, 2, index + 0.25);
    private static Bar[] Signal(bool negative = false) =>
    [new(default, negative ? 14 : 4, negative ? 20 : 10, negative ? 10 : 0, negative ? 16 : 6, 1),
     new(default, 8, 20, 0, 10, 1), new(default, 8, 20, 0, 12, 1)];

    private static void Inject(Bar[] bars, int start, bool negative = false)
    {
        var signal = Signal(negative);
        for (int i = 0; i < 3 && start + i < bars.Length; i++)
        {
            var prior = bars[start + i]; var value = signal[i];
            bars[start + i] = new(prior.Time, value.Open, value.High, value.Low, value.Close, prior.Volume);
        }
    }

    private static Bar[] Data(int count, bool dense = false)
    {
        var bars = Enumerable.Range(0, count).Select(Background).ToArray();
        foreach (int end in new[] { 68, 95, 96, 97, 127, 128, 129, 511, 512, 513, 2047, 4095, 8191, count - 1 })
            if (end >= 2 && end < count) Inject(bars, end - 2, (end & 1) == 0);
        if (dense) for (int i = 300; i + 2 < Math.Min(1600, count); i += 3) Inject(bars, i, (i & 1) == 0);
        return bars;
    }

    [Fact]
    public async Task SparseDenseAndUnequalPeriodHalosMatchIndependentReference()
    {
        foreach (var (near, equal) in new[] { (1, 1), (5, 5), (64, 3), (3, 64), (64, 64) })
            await Check(Data(10001, true), new GapSideBySideWhiteLinesCandle(near, equal));
        var bars = Enumerable.Range(0, 83).Select(Background).ToArray();
        Inject(bars, 80); var state = (IIndicatorState)new GapSideBySideWhiteLinesCandle().CreateState();
        Assert.Contains(bars.Select(b => state.Update(b)), x => x == 100);
        Inject(bars, 80, true); state.Reset();
        Assert.Contains(bars.Select(b => state.Update(b)), x => x == -100);
    }

    [Fact]
    public async Task SizeAndUnsupportedPeriodGuardsKeepOriginalFallbacks()
    {
        var guard = typeof(ValuesBarExecution).GetMethod("UsesGapSideReplay", Flags)!.CreateDelegate<Func<int, IIndicator, bool>>();
        foreach (int count in new[] { 0, 1, 31, 32, 33, 8191, 8192, 8193 })
        {
            Assert.Equal(count >= 8192, guard(count, new GapSideBySideWhiteLinesCandle()));
            await Check(Data(count), new GapSideBySideWhiteLinesCandle());
        }
        foreach (var (near, equal) in new[] { (65, 5), (5, 65), (int.MaxValue - 2, 1) })
        {
            var owner = new GapSideBySideWhiteLinesCandle(near, equal);
            Assert.False(guard(10001, owner)); await Check(Data(8193), owner);
        }
        foreach (IIndicator other in new IIndicator[] { new CounterattackCandle(), new SeparatingLinesCandle(), new IdenticalThreeCrowsCandle(), new StalledCandle() })
            Assert.False(guard(10001, other));
    }

    [Fact]
    public async Task StrictGapInclusiveOpenBodyAndWhiteDojiBoundariesRemainExact()
    {
        foreach (int boundary in new[] { -1, 0, 1 })
        {
            double open = boundary < 0 ? Math.BitDecrement(8.5) : boundary > 0 ? Math.BitIncrement(8.5) : 8.5;
            double close = boundary < 0 ? Math.BitDecrement(12d) : boundary > 0 ? Math.BitIncrement(12d) : 12;
            var bars = Enumerable.Range(0, 8193).Select(Background).ToArray();
            Inject(bars, 80); bars[82] = new(bars[82].Time, open, 20, 0, open + 2, bars[82].Volume);
            Inject(bars, 510, true); bars[512] = new(bars[512].Time, 8, 20, 0, close, bars[512].Volume);
            await Check(bars, new GapSideBySideWhiteLinesCandle());
        }
        var tied = Enumerable.Range(0, 8193).Select(Background).ToArray();
        Inject(tied, 80); tied[82] = new(tied[82].Time, 8, 20, 0, 8, tied[82].Volume);
        Inject(tied, 510); tied[511] = new(tied[511].Time, 6, 20, 0, 8, tied[511].Volume);
        tied[512] = new(tied[512].Time, 6, 20, 0, 8, tied[512].Volume);
        var expected = Original(tied, new GapSideBySideWhiteLinesCandle());
        Assert.Equal(100, expected[82]); Assert.Equal(0, expected[512]);
        await Check(tied, new GapSideBySideWhiteLinesCandle());
    }

    [Fact]
    public async Task SignedUnorderedWideSubnormalAndSignedZeroRetainOriginalArithmetic()
    {
        var random = new Random(20261119);
        var bars = Enumerable.Range(0, 8193).Select(i => new Bar(Background(i).Time,
            random.Next(-256, 257) / 8d, random.Next(-256, 257) / 8d,
            random.Next(-256, 257) / 8d, random.Next(-256, 257) / 8d, i)).ToArray();
        await Check(bars, new GapSideBySideWhiteLinesCandle(64, 3));
        Bar[] extremes = [new(default, double.MaxValue, double.MaxValue, -double.MaxValue, double.MaxValue, 1),
            new(default, -double.MaxValue, double.MaxValue, -double.MaxValue, -double.MaxValue, 1),
            new(default, -double.Epsilon, double.Epsilon, -double.Epsilon, 0, 1),
            new(default, -0d, 0, -0d, 0, 1), new(default, double.Epsilon, double.Epsilon, -double.Epsilon, double.Epsilon, 1)];
        for (int i = 0; i < bars.Length; i++) bars[i] = extremes[i % extremes.Length];
        Inject(bars, 510); await Check(bars, new GapSideBySideWhiteLinesCandle());
    }

    [Fact]
    public void ResetReplayRestoresEveryLogicalQueueLagAndAnchor()
    {
        var bars = Data(8193, true);
        foreach (var (near, equal) in new[] { (1, 1), (5, 5), (64, 3), (3, 64), (64, 64) })
        {
            var owner = new GapSideBySideWhiteLinesCandle(near, equal);
            var serial = (IIndicatorState)owner.CreateState();
            var replay = (IIndicatorState)owner.CreateState();
            for (int row = 0; row < bars.Length; row++)
            {
                if (row >= owner.WarmupBars && row % 31 == 0)
                {
                    replay.Reset();
                    for (int i = row - owner.WarmupBars; i < row; i++) replay.Update(in bars[i]);
                    AssertLatent(serial, replay);
                }
                Assert.Equal(BitConverter.DoubleToInt64Bits(serial.Update(in bars[row])), BitConverter.DoubleToInt64Bits(replay.Update(in bars[row])));
            }
        }
    }

    private static void AssertLatent(IIndicatorState expected, IIndicatorState actual)
    {
        foreach (var field in expected.GetType().GetFields(BindingFlags.Instance | BindingFlags.NonPublic))
        {
            object? left = field.GetValue(expected), right = field.GetValue(actual);
            if (left is ExactMeanAccumulator sum)
            {
                sum.Subtract(Assert.IsType<ExactMeanAccumulator>(right)); Assert.True(sum.IsExactlyZero);
            }
            else if (left is Queue<Bar> queue) Assert.Equal(queue.ToArray(), Assert.IsType<Queue<Bar>>(right).ToArray());
            else if (field.Name is "_seen" or "_first" or "_previous") Assert.Equal(left, right);
        }
    }

    private delegate object ReplayRegion(Bar[] source, double[] output, OwnedBarBuffer? owned, int start, int end,
        int window, ReadOnlySpan<Bar> seed, ReadOnlySpan<Bar> tail, CancellationToken cancellation, IIndicator indicator);
    private delegate object OriginalRegion(Bar[] source, double[][] output, OwnedBarBuffer? owned, int start, int end,
        int window, ReadOnlySpan<Bar> seed, ReadOnlySpan<Bar> tail, CancellationToken cancellation, IIndicator indicator);

    private static T BindRegion<T>(MethodInfo method) where T : Delegate
    {
        // Test-only span bridge; no production hooks or observer code.
        var bridge = new DynamicMethod("GapSideRegionBridge", typeof(object), method.GetParameters().Select(p => p.ParameterType).ToArray(), typeof(ValuesBarExecution), true);
        var il = bridge.GetILGenerator();
        for (short i = 0; i < method.GetParameters().Length; i++) il.Emit(OpCodes.Ldarg, i);
        il.Emit(OpCodes.Call, method); il.Emit(OpCodes.Box, method.ReturnType); il.Emit(OpCodes.Ret);
        return bridge.CreateDelegate<T>();
    }

    private static ReplayRegion Replay() => BindRegion<ReplayRegion>(typeof(ValuesBarExecution).GetMethod("ComputeGapSideReplay", Flags)!);

    [Fact]
    public void PoisonedBuffersSparseDenseSparseProduceExplicitPositiveZeroAndOriginalFlags()
    {
        var replay = Replay(); var owner = new GapSideBySideWhiteLinesCandle();
        var output = Enumerable.Repeat(double.NaN, 8193).ToArray();
        foreach (bool dense in new[] { false, true, false })
        {
            var bars = dense ? Data(output.Length, true) : Enumerable.Range(0, output.Length).Select(Background).ToArray();
            var expected = Original(bars, owner);
            var region = replay(bars, output, null, 0, bars.Length, owner.WarmupBars + 1, [], [], CancellationToken.None, owner);
            Assert.Equal(expected.Select(BitConverter.DoubleToInt64Bits), output.Select(BitConverter.DoubleToInt64Bits));
            Assert.False((bool)region.GetType().GetField("InvalidInput", Flags)!.GetValue(region)!);
            Assert.Equal(-1, (int)region.GetType().GetField("InvalidOutputIndex", Flags)!.GetValue(region)!);
            Assert.False((bool)region.GetType().GetField("HasUndefined", Flags)!.GetValue(region)!);
            if (!dense) Assert.All(output, value => Assert.Equal(0L, BitConverter.DoubleToInt64Bits(value)));
        }
    }

    [Fact]
    public void CapturedSeedTailAndOwnedRawBarsSurviveSourceMutation()
    {
        var owner = new GapSideBySideWhiteLinesCandle(64, 3); var original = Data(1025);
        Inject(original, original.Length - 3); var expected = Original(original, owner);
        const int start = 511; int halo = owner.WarmupBars;
        var seed = original.AsSpan(start - halo, halo).ToArray(); var tail = original.AsSpan(original.Length - halo, halo).ToArray();
        var source = original.ToArray(); var poisoned = new Bar(default, double.NaN, double.NaN, double.NaN, double.NaN, double.NaN);
        Array.Fill(source, poisoned, start - halo, halo); Array.Fill(source, poisoned, source.Length - halo, halo);
        var output = Enumerable.Repeat(double.NaN, original.Length).ToArray(); var owned = new OwnedBarBuffer(original.Length);
        Replay()(source, output, owned, start, source.Length, halo + 1, seed, tail, CancellationToken.None, owner);
        Array.Clear(source); Array.Clear(seed); Array.Clear(tail);
        Assert.Equal(expected.Skip(start).Select(BitConverter.DoubleToInt64Bits), output.Skip(start).Select(BitConverter.DoubleToInt64Bits));
        Assert.All(output.Take(start), value => Assert.True(double.IsNaN(value)));
        for (int row = start; row < original.Length; row++) Assert.Equal(original[row], owned[row]);
    }

    [Fact]
    public void InvalidInputRegionFlagsMatchUnchangedSmallBatchKernel()
    {
        var owner = new GapSideBySideWhiteLinesCandle(); var replay = Replay();
        var kernel = typeof(ValuesBarExecution).GetNestedType("MatchedLinesKernel", BindingFlags.NonPublic)!;
        var original = BindRegion<OriginalRegion>(typeof(ValuesBarExecution).GetMethod("ComputeBoundedRegion", Flags)!.MakeGenericMethod(kernel));
        foreach (int row in new[] { 0, 31, 32, 511, 1024 })
        foreach (int field in Enumerable.Range(0, 5))
        {
            var bars = Enumerable.Range(0, 1025).Select(Background).ToArray(); var b = bars[row];
            double[] values = [b.Open, b.High, b.Low, b.Close, b.Volume]; values[field] = double.NaN;
            bars[row] = new(b.Time, values[0], values[1], values[2], values[3], values[4]);
            object before = original(bars, [new double[bars.Length]], null, 0, bars.Length, owner.WarmupBars + 1, [], [], CancellationToken.None, owner);
            object after = replay(bars, new double[bars.Length], null, 0, bars.Length, owner.WarmupBars + 1, [], [], CancellationToken.None, owner);
            foreach (var flag in before.GetType().GetFields(Flags)) Assert.Equal(flag.GetValue(before), flag.GetValue(after));
        }
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void CapturedSeedAndTailStillValidateTheirOwnRawFields(bool seedFailure)
    {
        var owner = new GapSideBySideWhiteLinesCandle(); var bars = Data(1025); const int start = 511;
        var seed = bars.AsSpan(start - owner.WarmupBars, owner.WarmupBars).ToArray();
        var tail = bars.AsSpan(bars.Length - owner.WarmupBars, owner.WarmupBars).ToArray();
        var selected = seedFailure ? seed : tail; var b = selected[3];
        selected[3] = new(b.Time, b.Open, b.High, b.Low, b.Close, double.NaN);
        var kernel = typeof(ValuesBarExecution).GetNestedType("MatchedLinesKernel", BindingFlags.NonPublic)!;
        var original = BindRegion<OriginalRegion>(typeof(ValuesBarExecution).GetMethod("ComputeBoundedRegion", Flags)!.MakeGenericMethod(kernel));
        var before = original(bars, [new double[bars.Length]], null, start, bars.Length, owner.WarmupBars + 1, seed, tail, CancellationToken.None, owner);
        var after = Replay()(bars, new double[bars.Length], null, start, bars.Length, owner.WarmupBars + 1, seed, tail, CancellationToken.None, owner);
        foreach (var field in before.GetType().GetFields(Flags)) Assert.Equal(field.GetValue(before), field.GetValue(after));
        Assert.True((bool)after.GetType().GetField("InvalidInput", Flags)!.GetValue(after)!);
    }

    [Fact]
    public async Task ZeroSupportStillValidatesEveryFieldAndDefersFailureUntilExecution()
    {
        var bars = Enumerable.Range(0, 8193).Select(Background).ToArray(); var owner = new GapSideBySideWhiteLinesCandle();
        foreach (int row in new[] { 0, 31, 32, 511, 8192 })
        foreach (int field in Enumerable.Range(0, 5))
        {
            var b = bars[row]; double[] values = [b.Open, b.High, b.Low, b.Close, b.Volume];
            values[field] = field % 2 == 0 ? double.NaN : double.PositiveInfinity;
            bars[row] = new(b.Time, values[0], values[1], values[2], values[3], values[4]);
            // Configuring the source/indicator must not eagerly inspect invalid bars.
            var builder = new StockIndicatorBuilder().ConfigureSource(Bars.From(bars)).ConfigureIndicators(owner);
            await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => builder.BuildAsync()); bars[row] = b;
        }
        var valid = new StockIndicatorBuilder().ConfigureSource(Bars.From(bars)).ConfigureIndicators(owner);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => valid.BuildAsync(new CancellationToken(true)));
        await Check(bars, owner);
    }

    [Fact]
    public async Task SourceCancellationDoesNotPublishCompletedHistoryAndLaterRunRecovers()
    {
        var bars = Data(8193, true); var owner = new GapSideBySideWhiteLinesCandle(); using var cancellation = new CancellationTokenSource();
        var builder = new StockIndicatorBuilder().ConfigureSource(new CancellingSource(bars, cancellation)).ConfigureIndicators(owner);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => builder.BuildAsync(cancellation.Token));
        await Check(bars, owner);
    }

    private sealed class CancellingSource(Bar[] bars, CancellationTokenSource cancellation) : IBarSource
    {
        public bool IsFinite => true;
        public async IAsyncEnumerable<Bar> ReadAsync([EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            for (int i = 0; i < bars.Length; i++)
            {
                if (i == 128) cancellation.Cancel();
                cancellationToken.ThrowIfCancellationRequested();
                yield return bars[i]; await Task.Yield();
            }
        }
        public async IAsyncEnumerable<Bar> ReadWarmupAsync([EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            await Task.CompletedTask; yield break;
        }
    }

    // This test-only owner is passed directly to the private span bridge. It
    // cancels deterministically through existing getters/state calls, without
    // production hooks, sleeps or timing-dependent races. Its inner arithmetic
    // is the unchanged original GapSide state, and dispatch rejects this owner.
    private sealed class CancellationProbe(CancellationTokenSource cancellation, bool afterCapture) : IndicatorBase
    {
        internal int Creates, Resets, Updates;
        public override int WarmupBars
        {
            get { if (afterCapture) cancellation.Cancel(); return 7; }
        }
        protected internal override object CreateState()
        {
            Creates++;
            return new State(this, (IIndicatorState)new GapSideBySideWhiteLinesCandle().CreateState());
        }
        private void Cancel() => cancellation.Cancel();
        private sealed class State(CancellationProbe owner, IIndicatorState original) : IIndicatorState
        {
            public void Reset() { owner.Resets++; original.Reset(); }
            public double Update(in Bar bar)
            {
                double value = original.Update(in bar); owner.Updates++;
                owner.Cancel(); return value;
            }
        }
    }

    [Fact]
    public void ActualReplayPollsCancellationAfterCaptureAndDuringOriginalStateReplay()
    {
        var bars = Data(1025); var replay = Replay();
        foreach (bool afterCapture in new[] { true, false })
        {
            using var cancellation = new CancellationTokenSource(); var owner = new CancellationProbe(cancellation, afterCapture);
            var output = Enumerable.Repeat(double.NaN, bars.Length).ToArray(); var owned = new OwnedBarBuffer(bars.Length);
            var region = replay(bars, output, owned, 0, bars.Length, 8, [], [], cancellation.Token, owner);
            Assert.True(cancellation.IsCancellationRequested);
            Assert.ThrowsAny<OperationCanceledException>(() => cancellation.Token.ThrowIfCancellationRequested());
            Assert.False((bool)region.GetType().GetField("InvalidInput", Flags)!.GetValue(region)!);
            if (afterCapture)
            {
                Assert.Equal(0, owner.Creates); Assert.Equal(0, owner.Updates);
                Assert.All(output.Take(32), value => Assert.Equal(0L, BitConverter.DoubleToInt64Bits(value)));
                Assert.All(output.Skip(32), value => Assert.True(double.IsNaN(value)));
                for (int row = 0; row < 32; row++) Assert.Equal(bars[row], owned[row]);
            }
            else
            {
                Assert.Equal(1, owner.Creates); Assert.Equal(1, owner.Resets); Assert.Equal(1, owner.Updates);
                Assert.NotEqual(0, Original(bars, new GapSideBySideWhiteLinesCandle())[68]);
                Assert.Equal(0L, BitConverter.DoubleToInt64Bits(output[68]));
                Assert.All(output.Skip(96), value => Assert.True(double.IsNaN(value)));
            }
        }
    }

    [Fact]
    public void EnclosuresBoundIndependentExactBodyAndSignedMeanFractions()
    {
        Func<double, double, double> Bind(string name) => typeof(ValuesBarExecution).GetMethod(name, Flags)!.CreateDelegate<Func<double, double, double>>();
        var lower = Bind("GapSideLowerDifference"); var upper = Bind("GapSideUpperDifference"); var threshold = Bind("GapSideUpperThreshold");
        double[] pool = [-double.MaxValue, -1, -double.Epsilon, -0d, 0, double.Epsilon, Math.BitDecrement(1d), 1, Math.BitIncrement(1d), double.MaxValue];
        foreach (double left in pool) foreach (double right in pool)
        {
            var exact = ReferenceFraction.FromDouble(left) - ReferenceFraction.FromDouble(right); double lo = lower(left, right), hi = upper(left, right);
            Assert.False(double.IsNaN(lo)); Assert.False(double.IsNaN(hi));
            if (double.IsFinite(lo)) Assert.True(ReferenceFraction.FromDouble(lo).CompareTo(exact) <= 0); else Assert.Equal(double.NegativeInfinity, lo);
            if (double.IsFinite(hi)) Assert.True(ReferenceFraction.FromDouble(hi).CompareTo(exact) >= 0); else Assert.Equal(double.PositiveInfinity, hi);
        }
        var random = new Random(389512);
        foreach (int period in new[] { 1, 3, 5, 64 }) for (int repeat = 0; repeat < 30; repeat++)
        {
            var sum = new ReferenceFraction(0); double maximum = double.NegativeInfinity;
            for (int i = 0; i < period; i++)
            {
                double left = pool[random.Next(pool.Length)], right = pool[random.Next(pool.Length)];
                sum += ReferenceFraction.FromDouble(left) - ReferenceFraction.FromDouble(right); maximum = Math.Max(maximum, upper(left, right));
            }
            foreach (int factor in new[] { 5, 20 })
            {
                double value = threshold(maximum, factor);
                if (double.IsFinite(value)) Assert.True(ReferenceFraction.FromDouble(value).CompareTo(sum / new ReferenceFraction(period * factor)) >= 0);
                else Assert.Equal(double.PositiveInfinity, value);
            }
        }
    }

    private delegate bool Possible(in Bar a, in Bar b, in Bar c, double near, double equal);

    [Fact]
    public void CompiledRejectionPredicateNeverDiscardsAnExactBoundAdmissibleFormation()
    {
        var possible = typeof(ValuesBarExecution).GetMethod("GapSidePossible", Flags)!.CreateDelegate<Possible>();
        double[] pool = [-double.MaxValue, -2, -double.Epsilon, -0d, 0, double.Epsilon, 0.5, 1, Math.BitIncrement(1d), 2, double.MaxValue];
        var random = new Random(517621); int checkedFormations = 0;
        for (int repetition = 0; repetition < 3000; repetition++)
        {
            double bo = pool[random.Next(pool.Length)], bc = pool[random.Next(pool.Length)];
            double co = pool[random.Next(pool.Length)], cc = pool[random.Next(pool.Length)];
            var b = new Bar(default, Math.Min(bo, bc), 0, 0, Math.Max(bo, bc), 1);
            var c = new Bar(default, Math.Min(co, cc), 0, 0, Math.Max(co, cc), 1);
            bool up = b.Open > -double.MaxValue && c.Open > -double.MaxValue;
            bool down = b.Close < double.MaxValue && c.Close < double.MaxValue;
            if (!up && !down) continue;
            var a = new Bar(default, up ? -double.MaxValue : double.MaxValue, 0, 0, up ? -double.MaxValue : double.MaxValue, 1);
            double near = Math.Abs(pool[random.Next(pool.Length)]), equal = Math.Abs(pool[random.Next(pool.Length)]);
            var opens = (ReferenceFraction.FromDouble(c.Open) - ReferenceFraction.FromDouble(b.Open)).Abs();
            var bodies = ((ReferenceFraction.FromDouble(c.Close) - ReferenceFraction.FromDouble(c.Open))
                - (ReferenceFraction.FromDouble(b.Close) - ReferenceFraction.FromDouble(b.Open))).Abs();
            if (opens.CompareTo(ReferenceFraction.FromDouble(equal)) <= 0 && bodies.CompareTo(ReferenceFraction.FromDouble(near)) <= 0)
            {
                Assert.True(possible(in a, in b, in c, near, equal)); checkedFormations++;
            }
        }
        Assert.True(checkedFormations > 0);
    }

    private static double[] Original(Bar[] bars, IndicatorBase owner)
    {
        var state = (IIndicatorState)owner.CreateState(); var expected = bars.Select(b => state.Update(b)).ToArray();
        foreach (var rule in ((IIndicatorValidationContract)owner).ValidationRules)
            rule.Check(new IndicatorValidationContext("GapSide independent exact reference", bars, [expected], 0));
        return expected;
    }

    private static async Task Check(Bar[] bars, IndicatorBase owner)
    {
        var expected = Original(bars, owner); int prior = CpuParallelSettings.MaxDegreeOfParallelism;
        try
        {
            foreach (int workers in new[] { 1, 2, 8 }) foreach (var mode in Enum.GetValues<IndicatorHistoryMode>())
            {
                CpuParallelSettings.MaxDegreeOfParallelism = workers; var source = bars.ToArray();
                using var run = await new StockIndicatorBuilder().ConfigureSource(Bars.From(source)).ConfigureIndicators(owner).ConfigureHistory(mode).BuildAsync();
                Array.Clear(source); run.Dispose();
                Assert.Equal(expected.Select(BitConverter.DoubleToInt64Bits), run[owner].ToArray().Select(BitConverter.DoubleToInt64Bits));
                if (bars.Length != 0) Assert.Equal(bars[^1], run.Latest.Bar);
                if (mode == IndicatorHistoryMode.Full)
                {
                    int row = 0; await foreach (var snapshot in run) Assert.Equal(bars[row++], snapshot.Bar); Assert.Equal(bars.Length, row);
                }
            }
        }
        finally { CpuParallelSettings.MaxDegreeOfParallelism = prior; }
    }
}

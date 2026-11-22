using OoplesFinance.StockIndicators.Validation;

namespace OoplesFinance.StockIndicators.Indicators.Fast;

internal static partial class FastKernels
{
    /// <summary>
    /// Wilder average. Seeded, it publishes the running mean of the first <paramref name="period"/> values
    /// and then smooths; unseeded, it starts from the first value.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(FastMath.Kernel)]
    internal static void WilderAverage(ReadOnlySpan<double> input, Span<double> output, int period, bool seedWithAverage)
    {
        var n = input.Length;
        output = output.Slice(0, n);
        if (n == 0) return;
        double value;
        int start;
        if (seedWithAverage)
        {
            start = Math.Min(period, n);
            double sum = 0;
            value = 0;
            for (var i = 0; i < start; i++)
            {
                sum += input[i];
                value = sum / (i + 1);
                output[i] = value;
            }
        }
        else
        {
            start = 1;
            value = input[0];
            output[0] = value;
        }
        var scale = 1.0 / period;
        var weight = (period - 1.0) / period;
        for (var i = start; i < n; i++)
        {
            value = FastMath.Blend(weight, value, scale * input[i]);
            output[i] = value;
        }
    }

    /// <summary>
    /// Double or triple exponential average whose stages start at zero and are divided by the weight they
    /// have accumulated so far, until that correction is too small to matter.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(FastMath.Kernel)]
    internal static void CompensatedExponential(ReadOnlySpan<double> input, Span<double> output, int period, int order)
    {
        var n = input.Length;
        output = output.Slice(0, n);
        var alpha = 2.0 / (period + 1.0);
        var decay = 1 - alpha;
        var triple = order == 3;
        double residual = 1, first = 0, second = 0, third = 0;
        for (var i = 0; i < n; i++)
        {
            residual = residual > 1e-10 ? decay * residual : 0;
            var compensation = residual > 1e-10 ? 1 / (1 - residual) : 1;
            first += alpha * (input[i] - first);
            var one = first * compensation;
            second += alpha * (one - second);
            var two = second * compensation;
            if (!triple)
            {
                output[i] = 2 * one - two;
                continue;
            }
            third += alpha * (two - third);
            output[i] = 3 * one - 3 * two + third * compensation;
        }
    }

    /// <summary>Sum, difference, product or quotient of two price fields; a zero divisor publishes nothing.</summary>
    [System.Runtime.CompilerServices.MethodImpl(FastMath.Kernel)]
    internal static void CandleArithmetic(FastColumns columns, FastOutputs outputs, CandleArithmetic owner)
    {
        // A sum, a difference or a product is finite only if both of its terms are, so checking the result
        // checks them. A quotient is not: a zero divisor hides what it divides.
        var checkedByResult = owner.Operation != CandleArithmeticOperation.Divide;
        ReadOnlySpan<double> left = checkedByResult ? columns.Raw(InputOf(owner.Left)) : Field(columns, owner.Left);
        ReadOnlySpan<double> right = checkedByResult ? columns.Raw(InputOf(owner.Right)) : Field(columns, owner.Right);
        var n = left.Length;
        Span<double> value = outputs[0].AsSpan(0, n);
        switch (owner.Operation)
        {
            case CandleArithmeticOperation.Add:
                FastMath.Add(left, right, value);
                break;
            case CandleArithmeticOperation.Subtract:
                FastMath.Subtract(left, right, value);
                break;
            case CandleArithmeticOperation.Multiply:
                for (var i = 0; i < n; i++) value[i] = left[i] * right[i];
                break;
            default:
                var undefined = 0;
                var at = 0;
                long bits = 0;
#if !NETFRAMEWORK
                if (System.Numerics.Vector.IsHardwareAccelerated)
                {
                    // A vector of quotients at a time. A zero divisor's lane is taken anyway and then zeroed,
                    // and a quotient that is not finite leaves bits in the difference it makes with itself.
                    var width = System.Numerics.Vector<double>.Count;
                    var none = System.Numerics.Vector<double>.Zero;
                    var zeros = System.Numerics.Vector<long>.Zero;
                    var unlike = System.Numerics.Vector<long>.Zero;
                    for (; at <= n - width; at += width)
                    {
                        var divisor = new System.Numerics.Vector<double>(right.Slice(at, width));
                        var zero = System.Numerics.Vector.Equals(divisor, none);
                        var quotient = System.Numerics.Vector.ConditionalSelect(zero,
                            none, new System.Numerics.Vector<double>(left.Slice(at, width)) / divisor);
                        quotient.CopyTo(value.Slice(at, width));
                        zeros |= zero;
                        unlike |= System.Numerics.Vector.AsVectorInt64(quotient - quotient); // NOSONAR x - x is the finite test
                    }
                    if (zeros != System.Numerics.Vector<long>.Zero) undefined++;
                    if (unlike != System.Numerics.Vector<long>.Zero) bits = 1;
                }
#endif
                for (; at < n; at++)
                {
                    var divisor = right[at];
                    if (divisor == 0) // NOSONAR an exact zero is the contract
                    {
                        value[at] = 0;
                        undefined++;
                    }
                    else
                    {
                        var quotient = left[at] / divisor;
                        value[at] = quotient;
                        bits |= FastMath.NonFiniteBits(quotient);
                    }
                }
                // Every quotient was seen to be finite as it was taken, so the series needs no second look.
                if (bits == 0) outputs.Verified(0);
                if (undefined == 0) break;
                // A zero divisor is rare enough that its flags are worked out only once one has been met.
                Span<double> defined = outputs[1].AsSpan(0, n);
                for (var i = 0; i < n; i++) defined[i] = right[i] == 0 ? 0 : 1;
                return;
        }
        outputs.Flag(1, 0);
        if (!checkedByResult) return;
        if (FastMath.AllFinite(value))
        {
            outputs.Verified(0);
            return;
        }
        _ = Field(columns, owner.Left);
        _ = Field(columns, owner.Right);
    }

    [System.Runtime.CompilerServices.MethodImpl(FastMath.Hot)]
    private static IndicatorInputFields InputOf(CandlePriceField field) => field switch
    {
        CandlePriceField.Open => IndicatorInputFields.Open,
        CandlePriceField.High => IndicatorInputFields.High,
        CandlePriceField.Low => IndicatorInputFields.Low,
        CandlePriceField.Close => IndicatorInputFields.Close,
        _ => IndicatorInputFields.Volume
    };

    [System.Runtime.CompilerServices.MethodImpl(FastMath.Hot)]
    private static double[] Field(FastColumns columns, CandlePriceField field) => field switch
    {
        CandlePriceField.Open => columns.Open,
        CandlePriceField.High => columns.High,
        CandlePriceField.Low => columns.Low,
        _ => columns.Close,
    };

    /// <summary>
    /// Bar index of the highest or lowest of the last <paramref name="period"/> values, over the available
    /// history while the window fills. Equal values resolve to the newest or the oldest of them.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(FastMath.Kernel)]
    internal static void RollingExtremeIndex(ReadOnlySpan<double> input, Span<double> index, long period, bool highest,
        bool newestTie)
    {
        var n = input.Length;
        index = index.Slice(0, n);
        if (n == 0) return;
        if (period > RescanLimit)
        {
            ExtremeIndexByDeque(input, index, period, highest, newestTie);
            return;
        }
        // One loop for each rule, so that the rule is compiled into the loop and not decided on every bar.
        var window = (int)period;
        if (highest && newestTie) ExtremeIndexByRescan<NewestHighest>(input, index, window);
        else if (highest) ExtremeIndexByRescan<OldestHighest>(input, index, window);
        else if (newestTie) ExtremeIndexByRescan<NewestLowest>(input, index, window);
        else ExtremeIndexByRescan<OldestLowest>(input, index, window);
    }

    // Which value is the extreme, and which of two equal ones.
    private interface IExtremeRule
    {
        bool Highest { get; }
        bool Newest { get; }
        bool Takes(double value, double best);
    }

    private struct NewestHighest : IExtremeRule
    {
        public bool Highest => true;
        public bool Newest => true;
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining | FastMath.Hot)]
        public bool Takes(double value, double best) => value >= best;
    }

    private struct OldestHighest : IExtremeRule
    {
        public bool Highest => true;
        public bool Newest => false;
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining | FastMath.Hot)]
        public bool Takes(double value, double best) => value > best;
    }

    private struct NewestLowest : IExtremeRule
    {
        public bool Highest => false;
        public bool Newest => true;
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining | FastMath.Hot)]
        public bool Takes(double value, double best) => value <= best;
    }

    private struct OldestLowest : IExtremeRule
    {
        public bool Highest => false;
        public bool Newest => false;
        [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining | FastMath.Hot)]
        public bool Takes(double value, double best) => value < best;
    }

    [System.Runtime.CompilerServices.MethodImpl(FastMath.Kernel)]
    private static void ExtremeIndexByRescan<TRule>(ReadOnlySpan<double> input, Span<double> index, int period)
        where TRule : struct, IExtremeRule
    {
        var rule = default(TRule);
        var best = input[0];
        var at = 0;
        index[0] = 0;
        for (var i = 1; i < index.Length; i++)
        {
            var value = input[i];
            // A bar sets a new extreme about one time in eight, which a branch follows well; the search that
            // follows an extreme leaving the window is taken a vector of bars at a time.
            if (at <= i - period)
            {
                var from = i - period + 1;
                at = from + ExtremeOffset(input.Slice(from, period), rule.Highest, rule.Newest);
                best = input[at];
            }
            else if (rule.Takes(value, best))
            {
                best = value;
                at = i;
            }
            index[i] = at;
        }
    }

    // Where in a window its highest or lowest value is, counted from the window's first bar: the newest bar
    // that holds it, or the oldest. A short window is compared a vector at a time against its own extreme
    // and the bar is read off the comparisons' bits, so the search has no branch that the data decides.
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining | FastMath.Hot)]
    private static int ExtremeOffset(ReadOnlySpan<double> window, bool highest, bool newest)
    {
#if !NETFRAMEWORK
        if (System.Runtime.Intrinsics.X86.Avx.IsSupported && window.Length >= 8 && window.Length <= 64)
        {
            // The last vector overlaps the one before it wherever the window is not a whole number of them.
            // Two running extremes, so that neither waits on every vector of the window.
            var tail = System.Runtime.Intrinsics.Vector256.Create<double>(window.Slice(window.Length - 4, 4));
            System.Runtime.Intrinsics.Vector256<double> bests = tail, others = tail;
            var j = 0;
            for (; j <= window.Length - 8; j += 8)
            {
                System.Runtime.Intrinsics.Vector256<double> block = System.Runtime.Intrinsics.Vector256.Create<double>(window.Slice(j, 4)),
                    next = System.Runtime.Intrinsics.Vector256.Create<double>(window.Slice(j + 4, 4));
                bests = highest ? System.Runtime.Intrinsics.X86.Avx.Max(bests, block) : System.Runtime.Intrinsics.X86.Avx.Min(bests, block);
                others = highest ? System.Runtime.Intrinsics.X86.Avx.Max(others, next) : System.Runtime.Intrinsics.X86.Avx.Min(others, next);
            }
            if (j <= window.Length - 4)
            {
                var block = System.Runtime.Intrinsics.Vector256.Create<double>(window.Slice(j, 4));
                bests = highest ? System.Runtime.Intrinsics.X86.Avx.Max(bests, block) : System.Runtime.Intrinsics.X86.Avx.Min(bests, block);
            }
            bests = highest ? System.Runtime.Intrinsics.X86.Avx.Max(bests, others) : System.Runtime.Intrinsics.X86.Avx.Min(bests, others);
            System.Runtime.Intrinsics.Vector128<double> lower = System.Runtime.Intrinsics.Vector256.GetLower(bests), upper = System.Runtime.Intrinsics.Vector256.GetUpper(bests);
            var half = highest ? System.Runtime.Intrinsics.X86.Sse2.Max(lower, upper) : System.Runtime.Intrinsics.X86.Sse2.Min(lower, upper);
            var other = System.Runtime.Intrinsics.X86.Sse2.UnpackHigh(half, half);
            var extreme = System.Runtime.Intrinsics.Vector256.Create(System.Runtime.Intrinsics.Vector128.ToScalar(highest ? System.Runtime.Intrinsics.X86.Sse2.MaxScalar(half, other) : System.Runtime.Intrinsics.X86.Sse2.MinScalar(half, other)));
            ulong equal = 0;
            for (j = 0; j <= window.Length - 4; j += 4)
                equal |= (ulong)(uint)System.Runtime.Intrinsics.X86.Avx.MoveMask(System.Runtime.Intrinsics.X86.Avx.CompareEqual(System.Runtime.Intrinsics.Vector256.Create<double>(window.Slice(j, 4)), extreme)) << j;
            equal |= (ulong)(uint)System.Runtime.Intrinsics.X86.Avx.MoveMask(System.Runtime.Intrinsics.X86.Avx.CompareEqual(tail, extreme)) << (window.Length - 4);
            return newest ? 63 - System.Numerics.BitOperations.LeadingZeroCount(equal) : System.Numerics.BitOperations.TrailingZeroCount(equal);
        }
#endif
        var best = window[0];
        var at = 0;
        for (var j = 1; j < window.Length; j++)
        {
            var value = window[j];
            at = FastMath.Choose(Replaces(value, best, highest, newest), j, at);
            best = highest ? FastMath.Larger(best, value) : FastMath.Smaller(best, value);
        }
        return at;
    }

    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining | FastMath.Hot)]
    private static bool Replaces(double value, double best, bool highest, bool newestTie) =>
        highest ? (newestTie ? value >= best : value > best) : (newestTie ? value <= best : value < best);

    [System.Runtime.CompilerServices.MethodImpl(FastMath.Hot)]
    private static void ExtremeIndexByDeque(ReadOnlySpan<double> input, Span<double> index, long period, bool highest,
        bool newestTie)
    {
        // Candidates in bar order, in a queue as long as the series: every bar enters once, so neither end
        // ever wraps and no index needs a remainder.
        var queue = System.Buffers.ArrayPool<int>.Shared.Rent(input.Length);
        int head = 0, tail = 0;
        for (var i = 0; i < input.Length; i++)
        {
            var value = input[i];
            // A candidate the new value replaces can never be the extreme again.
            while (tail > head && Replaces(value, input[queue[tail - 1]], highest, newestTie)) tail--;
            if (tail > head && queue[head] <= i - period) head++;
            queue[tail++] = i;
            index[i] = queue[head];
        }
        System.Buffers.ArrayPool<int>.Shared.Return(queue);
    }

    /// <summary>
    /// Aroon: how recently the window's high and low occurred, as percentages of the period, and their
    /// difference. Nothing is published until the window is full.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(FastMath.Kernel)]
    internal static void Aroon(FastColumns columns, FastOutputs outputs, WindowAroon owner)
    {
        ReadOnlySpan<double> high = columns.High, low = columns.Low;
        var n = high.Length;
        var period = owner.Period;
        var size = (long)period + (owner.IncludeExtraBar ? 1 : 0);
        Span<double> up = outputs[0].AsSpan(0, n), down = outputs[1].AsSpan(0, n), oscillator = outputs[2].AsSpan(0, n);
        var first = (int)Math.Min(size - 1, n);
        for (var slot = 3; slot < 6; slot++) outputs.Flag(slot, first);
        // Every value published is zero or a whole number of hundredths over the period: finite as written.
        for (var slot = 0; slot < 3; slot++) outputs.Verified(slot);
#if !NETFRAMEWORK
        if (size <= AroonVectorLimit && (owner.OldestTie
            ? VectorAroon<OldestHighest>(high, low, up, down, oscillator, period, (int)size, first)
            : VectorAroon<NewestHighest>(high, low, up, down, oscillator, period, (int)size, first)))
            return;
#endif
        if (size <= AroonFusedLimit && n > 0)
        {
            // One loop for each tie rule, so that the rule is compiled into the loop and not decided on every bar.
            if (owner.OldestTie) FusedAroon<OldestHighest, OldestLowest>(high, low, up, down, oscillator, period, (int)size, first);
            else FusedAroon<NewestHighest, NewestLowest>(high, low, up, down, oscillator, period, (int)size, first);
            return;
        }
        // The two outputs hold the extreme's bar index until the final pass turns it into a percentage.
        RollingExtremeIndex(high, up, size, true, !owner.OldestTie);
        RollingExtremeIndex(low, down, size, false, !owner.OldestTie);
        up.Slice(0, first).Clear();
        down.Slice(0, first).Clear();
        oscillator.Slice(0, first).Clear();
        if (period > AroonTableLimit)
        {
            for (var i = first; i < n; i++)
            {
                long highest = (long)up[i], lowest = (long)down[i];
                up[i] = WindowAroon.Percentage(period - (i - highest), period);
                down[i] = WindowAroon.Percentage(period - (i - lowest), period);
                oscillator[i] = WindowAroon.Percentage(highest - lowest, period);
            }
            return;
        }
        // An extreme is at most a window old, so every percentage is one of a few values: they are divided
        // once each instead of three times a bar. Entry k holds the percentage of k - period.
        var percentages = new double[2 * period + 3];
        for (var k = 0; k < percentages.Length; k++) percentages[k] = WindowAroon.Percentage(k - period - 1L, period);
        for (var i = first; i < n; i++)
        {
            int highest = (int)up[i], lowest = (int)down[i];
            up[i] = percentages[2 * period + 1 - (i - highest)];
            down[i] = percentages[2 * period + 1 - (i - lowest)];
            oscillator[i] = percentages[period + 1 + (highest - lowest)];
        }
    }

#if !NETFRAMEWORK
    // Up to this window, searching every window outright is cheaper than following the extremes.
    private const int AroonVectorLimit = 32;

    // Short windows, eight bars at a time: each bar's window is searched from its oldest bar to its newest,
    // neighbouring windows side by side. Nothing depends on what the prices do - no extreme to expire,
    // no branch to guess - so a trend, which costs the loop below a search on every bar, costs nothing here.
    [System.Runtime.CompilerServices.MethodImpl(FastMath.Kernel)]
    private static bool VectorAroon<TRule>(ReadOnlySpan<double> high, ReadOnlySpan<double> low, Span<double> up, Span<double> down,
        Span<double> oscillator, int period, int size, int first)
        where TRule : struct, IExtremeRule
    {
        var n = high.Length;
        if (!System.Runtime.Intrinsics.X86.Avx.IsSupported || n - first < 8) return false;
        up.Slice(0, first).Clear();
        down.Slice(0, first).Clear();
        oscillator.Slice(0, first).Clear();
        // Entry k holds the percentage of k - period - 1.
        var percentages = new double[2 * period + 3];
        for (var k = 0; k < percentages.Length; k++) percentages[k] = WindowAroon.Percentage(k - period - 1L, period);
        ref var highs = ref System.Runtime.InteropServices.MemoryMarshal.GetReference(high);
        ref var lows = ref System.Runtime.InteropServices.MemoryMarshal.GetReference(low);
        var one = System.Runtime.Intrinsics.Vector256.Create(1.0);
        var i = first;
        while (true)
        {
            // Two groups of four go through the search together: each waits on its own running extreme from
            // one bar of the window to the next, and the processor fills that wait with the other group.
            var oldest = i - size + 1;
            var highest = System.Runtime.Intrinsics.Vector256.LoadUnsafe(ref highs, (nuint)oldest);
            var lowest = System.Runtime.Intrinsics.Vector256.LoadUnsafe(ref lows, (nuint)oldest);
            var highestNext = System.Runtime.Intrinsics.Vector256.LoadUnsafe(ref highs, (nuint)(oldest + 4));
            var lowestNext = System.Runtime.Intrinsics.Vector256.LoadUnsafe(ref lows, (nuint)(oldest + 4));
            var age = System.Runtime.Intrinsics.Vector256.Create((double)(size - 1));
            System.Runtime.Intrinsics.Vector256<double> highAge = age, lowAge = age, highAgeNext = age, lowAgeNext = age;
            for (var k = 1; k < size; k++)
            {
                age -= one;
                var h = System.Runtime.Intrinsics.Vector256.LoadUnsafe(ref highs, (nuint)(oldest + k));
                var l = System.Runtime.Intrinsics.Vector256.LoadUnsafe(ref lows, (nuint)(oldest + k));
                var hNext = System.Runtime.Intrinsics.Vector256.LoadUnsafe(ref highs, (nuint)(oldest + k + 4));
                var lNext = System.Runtime.Intrinsics.Vector256.LoadUnsafe(ref lows, (nuint)(oldest + k + 4));
                // A later bar that only equals the extreme takes its place under one tie rule and not the other.
                var newest = default(TRule).Newest;
                highAge = System.Runtime.Intrinsics.X86.Avx.BlendVariable(highAge, age, newest ? System.Runtime.Intrinsics.X86.Avx.CompareGreaterThanOrEqual(h, highest) : System.Runtime.Intrinsics.X86.Avx.CompareGreaterThan(h, highest));
                lowAge = System.Runtime.Intrinsics.X86.Avx.BlendVariable(lowAge, age, newest ? System.Runtime.Intrinsics.X86.Avx.CompareLessThanOrEqual(l, lowest) : System.Runtime.Intrinsics.X86.Avx.CompareLessThan(l, lowest));
                highAgeNext = System.Runtime.Intrinsics.X86.Avx.BlendVariable(highAgeNext, age,
                    newest ? System.Runtime.Intrinsics.X86.Avx.CompareGreaterThanOrEqual(hNext, highestNext) : System.Runtime.Intrinsics.X86.Avx.CompareGreaterThan(hNext, highestNext));
                lowAgeNext = System.Runtime.Intrinsics.X86.Avx.BlendVariable(lowAgeNext, age,
                    newest ? System.Runtime.Intrinsics.X86.Avx.CompareLessThanOrEqual(lNext, lowestNext) : System.Runtime.Intrinsics.X86.Avx.CompareLessThan(lNext, lowestNext));
                highest = System.Runtime.Intrinsics.X86.Avx.Max(highest, h);
                lowest = System.Runtime.Intrinsics.X86.Avx.Min(lowest, l);
                highestNext = System.Runtime.Intrinsics.X86.Avx.Max(highestNext, hNext);
                lowestNext = System.Runtime.Intrinsics.X86.Avx.Min(lowestNext, lNext);
            }
            AroonGroup(System.Runtime.Intrinsics.X86.Avx.ConvertToVector128Int32WithTruncation(highAge), System.Runtime.Intrinsics.X86.Avx.ConvertToVector128Int32WithTruncation(lowAge),
                percentages, period, up.Slice(i, 4), down.Slice(i, 4), oscillator.Slice(i, 4));
            AroonGroup(System.Runtime.Intrinsics.X86.Avx.ConvertToVector128Int32WithTruncation(highAgeNext), System.Runtime.Intrinsics.X86.Avx.ConvertToVector128Int32WithTruncation(lowAgeNext),
                percentages, period, up.Slice(i + 4, 4), down.Slice(i + 4, 4), oscillator.Slice(i + 4, 4));
            // The last eight bars are taken as a step of their own, over whatever the step before covered.
            if (i == n - 8) break;
            i = Math.Min(i + 8, n - 8);
        }
        return true;
    }

    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private static void AroonGroup(System.Runtime.Intrinsics.Vector128<int> highAges, System.Runtime.Intrinsics.Vector128<int> lowAges,
        double[] percentages, int period, Span<double> up, Span<double> down, Span<double> oscillator)
    {
        for (var lane = 0; lane < 4; lane++)
        {
            int sinceHigh = System.Runtime.Intrinsics.Vector128.GetElement(highAges, lane),
                sinceLow = System.Runtime.Intrinsics.Vector128.GetElement(lowAges, lane);
            up[lane] = percentages[2 * period + 1 - sinceHigh];
            down[lane] = percentages[2 * period + 1 - sinceLow];
            oscillator[lane] = percentages[period + 1 + (sinceLow - sinceHigh)];
        }
    }
#endif

    private const int AroonTableLimit = 4096;

    // Up to this window a rescan when an extreme expires is cheaper than a deque, as in RollingExtremeIndex.
    private const int AroonFusedLimit = 32;

    // Short windows in one pass: both extremes are followed together and each bar's three percentages are
    // written as soon as they are known, so nothing is stored and read back.
    [System.Runtime.CompilerServices.MethodImpl(FastMath.Kernel)]
    private static void FusedAroon<THigh, TLow>(ReadOnlySpan<double> high, ReadOnlySpan<double> low, Span<double> up, Span<double> down,
        Span<double> oscillator, int period, int size, int first)
        where THigh : struct, IExtremeRule where TLow : struct, IExtremeRule
    {
        var higher = default(THigh);
        var lower = default(TLow);
        var n = high.Length;
        up.Slice(0, first).Clear();
        down.Slice(0, first).Clear();
        oscillator.Slice(0, first).Clear();
        // Entry k holds the percentage of k - period - 1.
        var percentages = new double[2 * period + 3];
        for (var k = 0; k < percentages.Length; k++) percentages[k] = WindowAroon.Percentage(k - period - 1L, period);
        double highest = high[0], lowest = low[0];
        int highestAt = 0, lowestAt = 0;
        for (var i = 0; i < n; i++)
        {
            double h = high[i], l = low[i];
            // A bar sets a new extreme about one time in eight, which a branch follows well; measured, the
            // branch beats replacing the extreme without one. The search that follows an extreme leaving
            // the window is the costly part, and that is taken a vector of bars at a time.
            if (highestAt <= i - size)
            {
                highestAt = i - size + 1 + ExtremeOffset(high.Slice(i - size + 1, size), true, higher.Newest);
                highest = high[highestAt];
            }
            else if (higher.Takes(h, highest))
            {
                highest = h;
                highestAt = i;
            }
            if (lowestAt <= i - size)
            {
                lowestAt = i - size + 1 + ExtremeOffset(low.Slice(i - size + 1, size), false, lower.Newest);
                lowest = low[lowestAt];
            }
            else if (lower.Takes(l, lowest))
            {
                lowest = l;
                lowestAt = i;
            }
            if (i < first) continue;
            up[i] = percentages[2 * period + 1 - (i - highestAt)];
            down[i] = percentages[2 * period + 1 - (i - lowestAt)];
            oscillator[i] = percentages[period + 1 + (highestAt - lowestAt)];
        }
    }

    /// <summary>Half the sum of the window's lowest and highest value.</summary>
    [System.Runtime.CompilerServices.MethodImpl(FastMath.Kernel)]
    internal static void RangeMidpoint(ReadOnlySpan<double> lows, ReadOnlySpan<double> highs, Span<double> output, int period)
    {
        var n = lows.Length;
        output = output.Slice(0, n);
        var highest = FastMath.Allocate(n);
        RollingExtreme(lows, output, period, false);
        RollingExtreme(highs, highest, period, true);
        FastMath.Add(output, highest, output);
        FastMath.Scale(output, 0.5, output);
    }

    /// <summary>Sum of the last <paramref name="period"/> values; zero until the window is full.</summary>
    [System.Runtime.CompilerServices.MethodImpl(FastMath.Kernel)]
    internal static void RollingSum(ReadOnlySpan<double> input, Span<double> output, int period)
    {
        var n = input.Length;
        output = output.Slice(0, n);
        double sum = 0;
        var filling = Math.Min(period, n);
        for (var i = 0; i < filling; i++)
        {
            sum += input[i];
            output[i] = i < period - 1 ? 0 : sum;
        }
        var at = Math.Max(filling, 0);
        while (at < n)
        {
            // Every so many bars the window is summed afresh, which sheds what the slides have rounded away.
            if ((at & RebuildMask) == 0)
            {
                sum = 0;
                for (var j = at - period + 1; j <= at; j++) sum += input[j];
                output[at++] = sum;
                continue;
            }
            // Up to the next fresh sum the window only slides, with nothing else to decide on any bar.
            var count = Math.Min(n, (at | RebuildMask) + 1) - at;
            ReadOnlySpan<double> entering = input.Slice(at, count), leaving = input.Slice(at - period, count);
            var tail = output.Slice(at, count);
            for (var k = 0; k < tail.Length; k++)
            {
                sum += entering[k] - leaving[k];
                tail[k] = sum;
            }
            at += count;
        }
    }

    /// <summary>
    /// The Wilder recurrence run backwards: the running mean while it seeds, then the previous value moved
    /// away from the new one by a period-th of their distance.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(FastMath.Kernel)]
    internal static void ReverseWilder(ReadOnlySpan<double> input, Span<double> output, int period)
    {
        var n = input.Length;
        output = output.Slice(0, n);
        if (n == 0) return;
        var value = input[0];
        output[0] = value;
        var seeded = Math.Min(period, n);
        for (var i = 1; i < seeded; i++)
        {
            // Written as a step from the previous mean, so equal values stay exactly equal.
            value += (input[i] - value) / (i + 1);
            output[i] = value;
        }
        var scale = 1.0 / period;
        for (var i = Math.Max(seeded, 1); i < n; i++)
        {
            value += (value - input[i]) * scale;
            output[i] = value;
        }
    }


    /// <summary>
    /// Regularized exponential average: the usual step toward the close plus a pull along the previous
    /// slope, with the period growing to its setting over the first bars. The first two bars are the close.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(FastMath.Kernel)]
    internal static void RegularizedEma(ReadOnlySpan<double> close, Span<double> output, int period, double lambda)
    {
        var n = close.Length;
        output = output.Slice(0, n);
        double previous = 0, older = 0;
        for (var i = 0; i < n; i++)
        {
            var result = close[i];
            if (i >= 2)
            {
                double effective = Math.Min(i + 1, period), mass = effective + 1;
                result = (previous * (effective - 1) + 2 * result + lambda * (2 * previous - older) * mass)
                    / ((1 + lambda) * mass);
            }
            older = previous;
            previous = result;
            output[i] = result;
        }
    }

    /// <summary>
    /// Zero-lag exponential average: an average of the close pushed ahead by its change over half the
    /// period, with the period growing to its setting over the first bars.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(FastMath.Kernel)]
    internal static void ZeroLagEma(ReadOnlySpan<double> close, Span<double> output, int period)
    {
        var n = close.Length;
        output = output.Slice(0, n);
        var lag = (period - 1) / 2;
        double previous = 0;
        for (var i = 0; i < n; i++)
        {
            double count = Math.Min(i + 1, period);
            previous = (4 * close[i] - 2 * close[Math.Max(0, i - lag)] + previous * (count - 1)) / (count + 1);
            output[i] = previous;
        }
    }

    /// <summary>
    /// On-balance volume: volume added on a rising close and removed on a falling one, starting from the
    /// first bar's volume or from zero, with an optional window mean that starts at the second bar.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(FastMath.Kernel)]
    internal static void OnBalanceVolume(FastColumns columns, FastOutputs outputs, SeededOnBalanceVolume owner)
    {
        // A close that is not a number compares as neither above nor below, which would hide it, so the
        // closes are checked as they are read. A volume that is not finite stays in the total for good.
        ReadOnlySpan<double> close = columns.Raw(IndicatorInputFields.Close), volume = columns.Raw(IndicatorInputFields.Volume);
        var n = close.Length;
        long suspect = n > 0 ? FastMath.NonFiniteBits(close[0]) | FastMath.NonFiniteBits(volume[0]) : 0;
        Span<double> value = outputs[0].AsSpan(0, n);
        // Without an average, or before one exists, its series is zero throughout and its flag never turns on.
        var averaged = owner.AveragePeriod is int length && n > length;
        if (!averaged)
        {
            outputs.Flag(1, n);
            outputs.Flag(2, n);
        }
        if (n == 0) return;
        var total = owner.Seed == ObvSeed.FirstVolume ? volume[0] : 0;
        value[0] = total;
        // Bars before this one hold their signed volume, and those from the other one on still wait for the
        // running total.
        int signed = 1, totalled = 1;
#if !NETFRAMEWORK
        if (System.Runtime.Intrinsics.X86.Avx2.IsSupported && n >= 9)
        {
            // Four bars at a time, start to finish: each bar's volume signed by which way its close moved,
            // the four totalled among themselves, and their totals added to the running one - so a bar is
            // read once and written once, and the running total is waited on once in four bars.
            System.Runtime.Intrinsics.Vector256<double> zero = System.Runtime.Intrinsics.Vector256<double>.Zero, one = System.Runtime.Intrinsics.Vector256.Create(1.0);
            var running = System.Runtime.Intrinsics.Vector256.Create(total);
            var seen = System.Runtime.Intrinsics.Vector256<long>.Zero;
            for (; signed <= n - 4; signed += 4)
            {
                var now = System.Runtime.Intrinsics.Vector256.Create<double>(close.Slice(signed, 4));
                var then = System.Runtime.Intrinsics.Vector256.Create<double>(close.Slice(signed - 1, 4));
                var direction = System.Runtime.Intrinsics.X86.Avx.And(System.Runtime.Intrinsics.X86.Avx.CompareGreaterThan(now, then), one)
                    - System.Runtime.Intrinsics.X86.Avx.And(System.Runtime.Intrinsics.X86.Avx.CompareLessThan(now, then), one);
                seen |= System.Runtime.Intrinsics.Vector256.AsInt64(now - now); // NOSONAR x - x is the finite test
                var block = direction * System.Runtime.Intrinsics.Vector256.Create<double>(volume.Slice(signed, 4));
                // [a, b, c, d] becomes [a, a + b, b + c, c + d] and then [a, a + b, a + b + c, a + b + c + d].
                var pairs = block + System.Runtime.Intrinsics.X86.Avx.Blend(System.Runtime.Intrinsics.X86.Avx2.Permute4x64(block, 0x90), zero, 0x01);
                var totals = pairs + System.Runtime.Intrinsics.X86.Avx.Permute2x128(pairs, pairs, 0x08);
                System.Runtime.Intrinsics.Vector256.CopyTo(totals + running, value.Slice(signed, 4));
                running += System.Runtime.Intrinsics.X86.Avx2.Permute4x64(totals, 0xFF);
            }
            total = System.Runtime.Intrinsics.Vector256.ToScalar(running);
            if (seen != System.Runtime.Intrinsics.Vector256<long>.Zero) suspect = 1;
            // Nothing is left waiting for the running total.
            totalled = signed;
        }
        else if (System.Numerics.Vector.IsHardwareAccelerated)
        {
            // Each bar's volume, signed by which way its close moved, a vector of bars at a time; the
            // running total then adds the same terms in the same order.
            var width = System.Numerics.Vector<double>.Count;
            System.Numerics.Vector<double> zero = System.Numerics.Vector<double>.Zero, one = System.Numerics.Vector<double>.One;
            var seen = System.Numerics.Vector<long>.Zero;
            for (; signed <= n - width; signed += width)
            {
                var now = new System.Numerics.Vector<double>(close.Slice(signed, width));
                var then = new System.Numerics.Vector<double>(close.Slice(signed - 1, width));
                var direction = System.Numerics.Vector.ConditionalSelect(System.Numerics.Vector.GreaterThan(now, then), one, zero)
                    - System.Numerics.Vector.ConditionalSelect(System.Numerics.Vector.LessThan(now, then), one, zero);
                seen |= System.Numerics.Vector.AsVectorInt64(now - now); // NOSONAR x - x is the finite test
                (direction * new System.Numerics.Vector<double>(volume.Slice(signed, width))).CopyTo(value.Slice(signed, width));
            }
            if (!System.Numerics.Vector.EqualsAll(seen, System.Numerics.Vector<long>.Zero)) suspect = 1;
        }
#endif
        if (signed > totalled) total = FastMath.PrefixSum(value.Slice(totalled, signed - totalled), total);
        for (var i = signed; i < n; i++)
        {
            double current = close[i], previous = close[i - 1];
#if !NETFRAMEWORK
            // The direction of a bar is as good as random, so it is taken without a branch.
            bool up = current > previous, down = current < previous;
            total += (System.Runtime.CompilerServices.Unsafe.As<bool, byte>(ref up)
                - System.Runtime.CompilerServices.Unsafe.As<bool, byte>(ref down)) * volume[i];
#else
            if (current > previous) total += volume[i];
            else if (current < previous) total -= volume[i];
            suspect |= FastMath.NonFiniteBits(volume[i]);
#endif
            suspect |= FastMath.NonFiniteBits(current);
            value[i] = total;
        }
        if (suspect == 0 && FastMath.NonFiniteBits(total) == 0) outputs.Verified(0);
        else
        {
            _ = columns.Close;
            _ = columns.Volume;
        }
        if (!averaged || owner.AveragePeriod is not int period) return;
        Span<double> average = outputs[1].AsSpan(0, n);
        average.Clear();
        RebuiltMean(value.Slice(1), average.Slice(1), period);
        outputs.Flag(2, period);
    }


    /// <summary>
    /// Four-stage Laguerre filter started from zero: each stage follows the one before it with the same
    /// damping, and the output weights the stages 1, 2, 2, 1.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(FastMath.Kernel)]
    internal static void LaguerreFilter(ReadOnlySpan<double> close, Span<double> output, double gamma)
    {
        var n = close.Length;
        output = output.Slice(0, n);
        double first = 0, second = 0, third = 0, fourth = 0;
        for (var i = 0; i < n; i++)
        {
            var price = close[i];
            var nextFirst = price + gamma * (first - price);
            var nextSecond = first + gamma * (second - nextFirst);
            var nextThird = second + gamma * (third - nextSecond);
            var nextFourth = third + gamma * (fourth - nextThird);
            first = nextFirst;
            second = nextSecond;
            third = nextThird;
            fourth = nextFourth;
            output[i] = (first + 2 * second + 2 * third + fourth) / 6;
        }
    }


    // One exponential stage that divides by the weight it has accumulated until the unaccumulated remainder
    // is negligible, and is a plain exponential average after that.
    private struct MassNormalized(double alpha)
    {
        private double _mean, _mass;
        private double _residual = 1;

        [System.Runtime.CompilerServices.MethodImpl(FastMath.Hot)]
        internal double Next(double input)
        {
            if (_residual == 0) return _mean += alpha * (input - _mean);
            _residual = _residual > 1e-10 ? (1 - alpha) * _residual : 0;
            if (_mass == 0)
            {
                _mass = alpha;
                return _mean = input;
            }
            var previousWeight = (1 - alpha) * _mass;
            var weight = previousWeight + alpha;
            var numerator = previousWeight * _mean + input * alpha;
            _mean = _residual == 0 ? numerator : numerator / weight;
            _mass = _residual == 0 ? 1 : weight;
            return _mean;
        }
    }

    /// <summary>
    /// Quadruple exponential average of four chained mass-normalized stages, combined 4, -6, 4, -1.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(FastMath.Kernel)]
    internal static void QuadrupleEma(ReadOnlySpan<double> close, Span<double> output, MassNormalizedQuadrupleEma owner)
    {
        var n = close.Length;
        output = output.Slice(0, n);
        MassNormalized first = new(owner.Alpha1), second = new(owner.Alpha2), third = new(owner.Alpha3),
            fourth = new(owner.Alpha4);
        for (var i = 0; i < n; i++)
        {
            var one = first.Next(close[i]);
            var two = second.Next(one);
            var three = third.Next(two);
            output[i] = 4 * one - 6 * two + 4 * three - fourth.Next(three);
        }
    }


    /// <summary>
    /// McGinley dynamic: each step moves toward the price by the gap over factor * period, scaled by the
    /// fourth power of average over price, so it speeds up when the price falls below it. The delayed
    /// start restarts from a zero average and withholds the value for a period after that.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(FastMath.Kernel)]
    internal static void QuarticDynamic(ReadOnlySpan<double> close, Span<double> value, Span<double> defined,
        QuarticDynamicAverage owner)
    {
        var n = close.Length;
        value = value.Slice(0, n);
        defined = defined.Slice(0, n);
        value.Clear();
        defined.Clear();
        if (n == 0) return;
        var immediate = owner.Startup == McGinleyStartup.Immediate;
        var scale = owner.Factor * owner.Period;
        var period = owner.Period;
        long start = 1;
        var previous = close[0];
        // How far rounding can have moved the average so far: each step multiplies what it was handed by
        // the recurrence's own slope and adds its own. Where the price swings far enough that the slope is
        // steeper than one, that grows without limit, and the digits published would be the rounding's.
        double carried = 0;
        var first = 0;
        if (immediate)
        {
            value[0] = previous;
            defined[0] = 1;
            first = 1;
        }
        for (var i = first; i < n; i++)
        {
            var price = close[i];
            if (!immediate && previous == 0)
            {
                previous = price;
                carried = 0;
                start = (long)i + period;
                continue;
            }
            double next;
            if (previous == 0)
            {
                next = price / scale;
                carried = 0;
            }
            else if (price == 0) next = previous > 0 ? double.NegativeInfinity : double.PositiveInfinity;
            else
            {
                var ratio = previous / price;
                var square = ratio * ratio;
                next = previous + (price - previous) * (square * square) / scale;
                var slope = 1 + (4 * square * ratio - 5 * square * square) / scale;
                carried = Math.Abs(slope) * carried + 8 * Ulp * (Math.Abs(previous) + Math.Abs(next));
                if (carried > 1e-10 * Math.Max(Math.Abs(next), Math.Abs(price))) throw FastUndecidedException.Raise();
            }
            if (double.IsInfinity(next) || double.IsNaN(next))
            {
                // Published without a flag, which is what the output check rejects.
                value[i] = next;
                continue;
            }
            previous = next;
            if (!immediate && i < start) continue;
            value[i] = next;
            defined[i] = 1;
        }
    }


    /// <summary>
    /// Deviation-scaled average: an exponential average whose step grows with how large the smoothed
    /// distance of the price from the average is against its recent root mean square.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(FastMath.Kernel)]
    internal static void DeviationScaled(ReadOnlySpan<double> close, Span<double> output, int period, double scaleFactor)
    {
        var n = close.Length;
        output = output.Slice(0, n);
        if (n == 0) return;
        var a = Math.Exp(-1.414 * Math.PI / (.5 * period));
        var b = 2 * a * Math.Cos(1.414 * Math.PI / (.5 * period));
        var c = -a * a;
        var half = (1 - b - c) / 2;
        var squares = new double[period];
        // The filter itself, kept for the bars on which its squares are too small to hold.
        var filters = new double[period];
        var average = close[0];
        output[0] = average;
        double previousZero = 0, filter = 0, older = 0, sum = 0, largest = 0;
        // Consecutive bars whose filter is exactly zero: a window of them has a sum of exactly zero.
        int flat = 0, slot = 0, count = 0, slid = 0;
        for (var i = 1; i < n; i++)
        {
            var price = close[i];
            var zero = price - average;
            var next = half * (zero + previousZero) + b * filter + c * older;
            // A filter that has decayed into the subnormals reaches zero on a bar its last rounding decides,
            // and the average steps differently from that bar on: there it is rounded once, from the exact sum.
            if (Math.Abs(next) < FilterRoundedOnceBelow && (filter != 0 || older != 0 || zero + previousZero != 0))
                next = FilterRoundedOnce(half, zero, previousZero, b, filter, c, older);
            var square = next * next;
            if (count == period) sum -= squares[slot];
            else count++;
            squares[slot] = square;
            filters[slot] = next;
            slot = slot + 1 == period ? 0 : slot + 1;
            sum += square;
            largest = Math.Max(largest, square);
            flat = next == 0 ? flat + 1 : 0;
            // The running sum is rebuilt at intervals and whenever it has fallen far enough below what
            // passed through it that the rounding it kept could matter.
            if (++slid > RebuildMask || !(sum > largest * (1.0 / 1024)))
            {
                sum = 0;
                for (var j = 0; j < count; j++) sum += squares[j];
                largest = sum;
                slid = 0;
            }
            // Squares below this have lost digits to underflow, or all of them: the ratio is then taken of the
            // filter values against the largest of them, which is the same ratio.
            var normalized = flat >= count ? 0
                : sum < SquaresHoldAbove ? ScaledDeviationRatio(filters.AsSpan(0, count), next, period)
                : Math.Sqrt(square * period / sum);
            var gain = Math.Max(.1, Math.Min(1, scaleFactor * normalized * 5 / period));
            average = gain * price + (1 - gain) * average;
            previousZero = zero;
            older = filter;
            filter = next;
            output[i] = average;
        }
    }


    private const double FilterRoundedOnceBelow = 1e-290;
    private const double SquaresHoldAbove = 1e-250;

    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    private static double FilterRoundedOnce(double half, double zero, double previousZero, double b, double filter,
        double c, double older)
    {
        var exact = new ExactMeanAccumulator();
        exact.AddProduct(half, zero);
        exact.AddProduct(half, previousZero);
        exact.AddProduct(b, filter);
        exact.AddProduct(c, older);
        return exact.Mean(1);
    }

    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    private static double ScaledDeviationRatio(ReadOnlySpan<double> filters, double current, int period)
    {
        double largest = 0;
        foreach (var value in filters) largest = Math.Max(largest, Math.Abs(value));
        if (largest == 0) return 0;
        double sum = 0;
        foreach (var value in filters)
        {
            var scaled = value / largest;
            sum += scaled * scaled;
        }
        var ratio = current / largest;
        return Math.Sqrt(ratio * ratio * period / sum);
    }

    /// <summary>
    /// Price relative: the ratio of one price field to another, the mean of that ratio over a window, and
    /// the difference between the two fields' returns over a lag. Each value has its own defined flag.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(FastMath.Kernel)]
    internal static void PriceRelative(FastColumns columns, double[][] output, PriceRelativeStrength owner)
    {
        var evaluation = Field(columns, owner.Evaluation);
        var basis = Field(columns, owner.Basis);
        var n = columns.Close.Length;
        for (var slot = 0; slot < 6; slot++) output[slot].AsSpan(0, n).Clear();
        var meanPeriod = owner.MeanPeriod ?? 0;
        var lag = owner.Period ?? 0;
        // Bars whose ratio overflows are skipped by the windows, so both windows keep their own rings
        // instead of indexing back into the columns.
        var ratios = new double[meanPeriod];
        var pastEvaluation = new double[lag];
        var pastBasis = new double[lag];
        int ratioSlot = 0, ratioCount = 0, missing = 0, pastSlot = 0, pastCount = 0, slid = 0;
        double sum = 0, largest = 0;
        for (var i = 0; i < n; i++)
        {
            var e = evaluation[i];
            var b = basis[i];
            var has = b != 0;
            var ratio = has ? e / b : double.NaN;
            if (has)
            {
                output[0][i] = ratio;
                output[3][i] = 1;
                if (double.IsInfinity(ratio)) continue;
            }
            if (meanPeriod != 0)
            {
                if (ratioCount == meanPeriod)
                {
                    var old = ratios[ratioSlot];
                    if (double.IsNaN(old)) missing--;
                    else
                    {
                        sum -= old;
                        largest = Math.Max(largest, Math.Abs(old));
                    }
                }
                else ratioCount++;
                ratios[ratioSlot] = ratio;
                ratioSlot = ratioSlot + 1 == meanPeriod ? 0 : ratioSlot + 1;
                if (has)
                {
                    sum += ratio;
                    largest = Math.Max(largest, Math.Abs(ratio));
                }
                else missing++;
                // Rebuilt at intervals, and whenever the sum has fallen far below what passed through it.
                if (++slid > RebuildMask || !(Math.Abs(sum) > largest * (1.0 / 1024)))
                {
                    sum = 0;
                    double carry = 0;
                    for (var j = 0; j < ratioCount; j++)
                    {
                        var term = ratios[j];
                        if (double.IsNaN(term)) continue;
                        var total = sum + term;
                        carry += Math.Abs(sum) >= Math.Abs(term) ? sum - total + term : term - total + sum;
                        sum = total;
                    }
                    sum += carry;
                    largest = Math.Abs(sum);
                    slid = 0;
                }
                if (ratioCount == meanPeriod && missing == 0)
                {
                    output[1][i] = meanPeriod == 1 ? ratio : sum / meanPeriod;
                    output[4][i] = 1;
                }
            }
            if (lag == 0) continue;
            if (pastCount == lag)
            {
                var oldEvaluation = pastEvaluation[pastSlot];
                var oldBasis = pastBasis[pastSlot];
                if (oldEvaluation != 0 && oldBasis != 0)
                {
                    output[2][i] = ReturnGap(e, b, oldEvaluation, oldBasis);
                    output[5][i] = 1;
                }
            }
            else pastCount++;
            pastEvaluation[pastSlot] = e;
            pastBasis[pastSlot] = b;
            pastSlot = pastSlot + 1 == lag ? 0 : pastSlot + 1;
        }
    }

    // e / oldE - b / oldB as one quotient, with the products' rounding carried, so the two returns
    // cancelling does not leave only their rounding behind.
    [System.Runtime.CompilerServices.MethodImpl(FastMath.Hot)]
    private static double ReturnGap(double e, double b, double oldE, double oldB)
    {
        var left = e * oldB;
        var right = b * oldE;
        var divisor = oldE * oldB;
        var plain = (left - right) / divisor;
        if (double.IsInfinity(left) || double.IsInfinity(right) || double.IsInfinity(divisor) || divisor == 0
            || Math.Abs(left) < 1e-270 || Math.Abs(right) < 1e-270)
            return e / oldE - b / oldB;
        var residual = ProductResidual(e, oldB, left) - ProductResidual(b, oldE, right);
        var refined = (left - right + residual) / divisor;
        return double.IsNaN(refined) || double.IsInfinity(refined) ? plain : refined;
    }

    // What a * b lost to rounding, by splitting each factor into two halves whose products are exact.
    [System.Runtime.CompilerServices.MethodImpl(FastMath.Hot)]
    private static double ProductResidual(double a, double b, double product)
    {
        const double Splitter = 134217729;
        var scaledA = a * Splitter;
        var highA = scaledA - (scaledA - a);
        var lowA = a - highA;
        var scaledB = b * Splitter;
        var highB = scaledB - (scaledB - b);
        var lowB = b - highB;
        return lowA * lowB - (product - highA * highB - lowA * highB - highA * lowB);
    }


    /// <summary>
    /// Fractal adaptive average: the mean of the bars so far until a window fills, then an exponential
    /// average whose step comes from the fractal dimension, the ranges of the window's two halves against
    /// the range of the whole window.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(FastMath.Kernel)]
    internal static void FractalAdaptive(ReadOnlySpan<double> close, Span<double> output, int period)
    {
        var n = close.Length;
        output = output.Slice(0, n);
        if (n == 0) return;
        var half = period / 2;
        var rest = period - half;
        double sum = 0, average = 0;
        var filling = Math.Min(n, period - 1);
        for (var i = 0; i < filling; i++)
        {
            sum += close[i];
            average = i == 0 ? close[0] : sum / (i + 1);
            output[i] = average;
        }
        if (n < period) return;
        double[] wholeHigh = FastMath.Allocate(n), wholeLow = FastMath.Allocate(n), restHigh = FastMath.Allocate(n),
            restLow = FastMath.Allocate(n), halfHigh = FastMath.Allocate(n), halfLow = FastMath.Allocate(n);
        RollingExtreme(close, wholeHigh, period, true);
        RollingExtreme(close, wholeLow, period, false);
        RollingExtreme(close, restHigh, rest, true);
        RollingExtreme(close, restLow, rest, false);
        RollingExtreme(close, halfHigh, half, true);
        RollingExtreme(close, halfLow, half, false);
        var scale = (double)period / half;
        var inverseLog = 1 / Math.Log(2);
        for (var i = period - 1; i < n; i++)
        {
            var whole = wholeHigh[i] - wholeLow[i];
            var split = halfHigh[i - rest] - halfLow[i - rest] + (restHigh[i] - restLow[i]);
            // Flat halves leave a ratio of one unit against the whole range, or exactly one: a full step.
            double gain = 1;
            if (split != 0)
            {
                var dimension = Math.Log(split * scale / whole) * inverseLog;
                gain = Math.Max(.01, Math.Min(1, Math.Exp(-4.6 * (dimension - 1))));
            }
            average = gain * close[i] + (1 - gain) * average;
            output[i] = average;
        }
    }


    /// <summary>
    /// Pearson correlation of two price columns over the window; a window where either column is flat has
    /// no correlation. The five sums slide, measured from anchor prices so they do not lose digits to the
    /// price level, and are taken afresh on a schedule and whenever a variance has all but cancelled.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(FastMath.Kernel)]
    internal static void Correlation(FastColumns columns, Span<double> value, FastOutputs outputs, WindowCorrelation owner)
    {
        ReadOnlySpan<double> left = Field(columns, owner.Left), right = Field(columns, owner.Right);
        var n = columns.Count;
        var period = owner.Period;
        value = value.Slice(0, n);
        if (n < period)
        {
            value.Clear();
            outputs.Flag(1, n);
            return;
        }
        value.Slice(0, period - 1).Clear();
        // Every bar from the first whole window on is defined unless a flat window is to be withheld, so the
        // flags are declared, and written out only for a series that has such a window.
        var withheld = false;
        int leftRun = 0, rightRun = 0;
        for (var i = 0; i < period - 1; i++)
        {
            leftRun = i > 0 && left[i] == left[i - 1] ? leftRun + 1 : 1; // NOSONAR an exact tie is the contract
            rightRun = i > 0 && right[i] == right[i - 1] ? rightRun + 1 : 1; // NOSONAR an exact tie is the contract
        }
        var block = Math.Max(64, period);
        // The loop that slides the sums leaves each bar as a numerator here and a product of two variances
        // there; the square root and the division, the slowest steps of all and tied to nothing before
        // them, are then taken a vector of bars at a time.
        var rented = System.Buffers.ArrayPool<double>.Shared.Rent(n);
        var products = rented.AsSpan(0, n);
        // One division for the window instead of three a bar.
        var inverse = 1.0 / period;
        var at = period - 1;
        // Whether the bar a block starts on already has its runs counted: it does when the block before it
        // stopped there to sum the window afresh.
        var counted = false;
        while (at < n)
        {
            // A block starts on a fresh sum of its first window, around that window's newest pair.
            if (!counted)
            {
                leftRun = at > 0 && left[at] == left[at - 1] ? leftRun + 1 : 1; // NOSONAR an exact tie is the contract
                rightRun = at > 0 && right[at] == right[at - 1] ? rightRun + 1 : 1; // NOSONAR an exact tie is the contract
            }
            counted = false;
            var from = at - period + 1;
            var sums = PairSums.Of(left.Slice(from, period), right.Slice(from, period));
            products[at] = 1;
            if (leftRun >= period || rightRun >= period)
            {
                value[at] = 0;
                withheld = true;
            }
            else
            {
                double a = sums.Xx - sums.X * sums.X * inverse, b = sums.Yy - sums.Y * sums.Y * inverse;
                // Where a variance is a small remainder even of a fresh sum of squares, the window is summed
                // around its own means instead.
                if (!(a > 1e-4 * sums.Xx) || !(b > 1e-4 * sums.Yy))
                    value[at] = CenteredCorrelation(left.Slice(from, period), right.Slice(from, period));
                else
                {
                    value[at] = sums.Xy - sums.X * sums.Y * inverse;
                    products[at] = a * b;
                }
            }
            at++;
            // The rest of the block slides those sums, held where the compiler can keep them in registers.
            double anchorX = sums.AnchorX, anchorY = sums.AnchorY, sumX = sums.X, sumY = sums.Y;
            double sumXx = sums.Xx, sumYy = sums.Yy, sumXy = sums.Xy;
            var end = (int)Math.Min((long)at + block - 1, n);
            for (; at < end; at++)
            {
                leftRun = left[at] == left[at - 1] ? leftRun + 1 : 1; // NOSONAR an exact tie is the contract
                rightRun = right[at] == right[at - 1] ? rightRun + 1 : 1; // NOSONAR an exact tie is the contract
                double x = left[at] - anchorX, y = right[at] - anchorY;
                double oldX = left[at - period] - anchorX, oldY = right[at - period] - anchorY;
                sumX += x - oldX;
                sumY += y - oldY;
                sumXx += x * x - oldX * oldX;
                sumYy += y * y - oldY * oldY;
                sumXy += x * y - oldX * oldY;
                if (leftRun >= period || rightRun >= period)
                {
                    value[at] = 0;
                    products[at] = 1;
                    withheld = true;
                    continue;
                }
                double a = sumXx - sumX * sumX * inverse, b = sumYy - sumY * sumY * inverse;
                // Where a variance is a small remainder of its sum of squares, the block stops and the next
                // one starts here, on a fresh sum.
                if (!(a > 1e-4 * sumXx) || !(b > 1e-4 * sumYy))
                {
                    counted = true;
                    break;
                }
                value[at] = sumXy - sumX * sumY * inverse;
                products[at] = a * b;
            }
        }
        var finished = period - 1;
        var width = System.Numerics.Vector<double>.Count;
        var numerators = System.Runtime.InteropServices.MemoryMarshal.Cast<double, System.Numerics.Vector<double>>(value.Slice(finished));
        var variances = System.Runtime.InteropServices.MemoryMarshal.Cast<double, System.Numerics.Vector<double>>(products.Slice(finished));
        for (var group = 0; group < numerators.Length; group++)
            numerators[group] /= System.Numerics.Vector.SquareRoot(variances[group]);
        finished += numerators.Length * width;
        for (; finished < n; finished++) value[finished] /= Math.Sqrt(products[finished]);
        System.Buffers.ArrayPool<double>.Shared.Return(rented);
        if (!withheld || owner.FlatZero)
        {
            outputs.Flag(1, period - 1);
            return;
        }
        Span<double> defined = outputs[1].AsSpan(0, n);
        defined.Slice(0, period - 1).Clear();
        int leftFlat = 0, rightFlat = 0;
        for (var i = 0; i < n; i++)
        {
            leftFlat = i > 0 && left[i] == left[i - 1] ? leftFlat + 1 : 1; // NOSONAR an exact tie is the contract
            rightFlat = i > 0 && right[i] == right[i - 1] ? rightFlat + 1 : 1; // NOSONAR an exact tie is the contract
            if (i >= period - 1) defined[i] = leftFlat >= period || rightFlat >= period ? 0 : 1;
        }
    }

    // Sums of two columns over a window, each measured from that column's anchor price.
    private struct PairSums
    {
        internal double AnchorX, AnchorY, X, Y, Xx, Yy, Xy;

        // Sums the window around its newest pair, which becomes the anchor.
        [System.Runtime.CompilerServices.MethodImpl(FastMath.Hot)]
        internal static PairSums Of(ReadOnlySpan<double> left, ReadOnlySpan<double> right)
        {
            var sums = new PairSums { AnchorX = left[left.Length - 1], AnchorY = right[right.Length - 1] };
            for (var j = 0; j < left.Length; j++)
            {
                double x = left[j] - sums.AnchorX, y = right[j] - sums.AnchorY;
                sums.X += x;
                sums.Y += y;
                sums.Xx += x * x;
                sums.Yy += y * y;
                sums.Xy += x * y;
            }
            return sums;
        }
    }

    // The correlation of one window, summed around its own means.
    [System.Runtime.CompilerServices.MethodImpl(FastMath.Hot)]
    private static double CenteredCorrelation(ReadOnlySpan<double> left, ReadOnlySpan<double> right)
    {
        var period = left.Length;
        double leftAnchor = left[period - 1], rightAnchor = right[period - 1], leftSum = 0, rightSum = 0;
        for (var j = 0; j < period; j++)
        {
            leftSum += left[j] - leftAnchor;
            rightSum += right[j] - rightAnchor;
        }
        double leftMean = leftSum / period, rightMean = rightSum / period, a = 0, b = 0, c = 0;
        for (var j = 0; j < period; j++)
        {
            var x = left[j] - leftAnchor - leftMean;
            var y = right[j] - rightAnchor - rightMean;
            a += x * x;
            b += y * y;
            c += x * y;
        }
        return c / Math.Sqrt(a * b);
    }

    /// <summary>
    /// Holt-Winter forecast: a level, a velocity and an acceleration, each corrected by a share of the
    /// last forecast's error, published as the forecast for the next bar.
    /// </summary>
    [System.Runtime.CompilerServices.MethodImpl(FastMath.Kernel)]
    internal static void HoltWinter(ReadOnlySpan<double> close, Span<double> output, HoltWinterForecast owner)
    {
        var n = close.Length;
        output = output.Slice(0, n);
        if (n == 0) return;
        if (owner.Period == 1)
        {
            close.CopyTo(output);
            return;
        }
        double a = owner.LevelFactor, b = owner.TrendFactor, c = owner.AccelerationFactor;
        double level = close[0], velocity = 0, acceleration = 0;
        output[0] = level;
        for (var i = 1; i < n; i++)
        {
            var forecast = level + velocity + 0.5 * acceleration;
            var next = forecast + a * (close[i] - forecast);
            var trend = velocity + acceleration;
            var speed = trend + b * (next - level - trend);
            acceleration += c * (speed - velocity - acceleration);
            level = next;
            velocity = speed;
            output[i] = level + velocity + 0.5 * acceleration;
        }
    }
}

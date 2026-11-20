namespace OoplesFinance.StockIndicators.Indicators.Fast;

internal static partial class FastKernels
{
    /// <summary>Whether <see cref="ClassicAverage"/> covers a method; the others keep the exact route.</summary>
    internal static bool SupportsClassic(ClassicAverageMethod method) => method is ClassicAverageMethod.Sma
        or ClassicAverageMethod.Ema or ClassicAverageMethod.Wma or ClassicAverageMethod.Dema
        or ClassicAverageMethod.Tema or ClassicAverageMethod.Trima;

    /// <summary>Marks an output as defined from <paramref name="first"/> onward.</summary>
    internal static void DefinedFrom(Span<double> flags, long first)
    {
        var start = (int)Math.Min(first, flags.Length);
        flags.Slice(0, start).Clear();
        flags.Slice(start).Fill(1);
    }

    /// <summary>
    /// A classic moving average, zero before <paramref name="first"/>. <paramref name="first"/> is the
    /// average's own first defined bar, or a later bar when a composite aligns it to a slower sibling.
    /// </summary>
    internal static void ClassicAverage(ReadOnlySpan<double> input, Span<double> average, ClassicMovingAverage owner, long first)
    {
        var n = input.Length;
        average = average.Slice(0, n);
        var start = (int)Math.Min(first, n);
        var period = owner.Period;
        if (period == 1)
        {
            average.Slice(0, start).Clear();
            input.Slice(start).CopyTo(average.Slice(start));
            return;
        }
        switch (owner.Method)
        {
            case ClassicAverageMethod.Ema:
            case ClassicAverageMethod.Dema:
            case ClassicAverageMethod.Tema:
                ClassicExponential(input, average, period, owner.Order, owner.FirstPriceSeed, owner.Suppression,
                    first - owner.First, first);
                return;
            case ClassicAverageMethod.Wma:
                WeightedWindow(input, average, period);
                break;
            case ClassicAverageMethod.Trima:
                TriangularWindow(input, average, period);
                break;
            default:
                Sma(input, average, period);
                break;
        }
        average.Slice(0, start).Clear();
    }

    /// <summary>Linearly weighted mean of the last <paramref name="period"/> values, newest heaviest.</summary>
    internal static void WeightedWindow(ReadOnlySpan<double> input, Span<double> output, int period)
    {
        var n = input.Length;
        output.Slice(0, Math.Min(period - 1, n)).Clear();
        if (period > n) return;
        double sum = 0, weighted = 0;
        for (var i = 0; i < period; i++)
        {
            sum += input[i];
            weighted += (i + 1.0) * input[i];
        }
        var reciprocal = 2.0 / ((double)period * (period + 1.0));
        output[period - 1] = weighted * reciprocal;
        for (var i = period; i < n; i++)
        {
            // Every retained weight drops by one, then the new value enters at full weight.
            weighted += period * input[i] - sum;
            sum += input[i] - input[i - period];
            output[i] = weighted * reciprocal;
        }
    }

    /// <summary>Triangular mean: a simple mean of simple means whose lengths sum to period + 1.</summary>
    internal static void TriangularWindow(ReadOnlySpan<double> input, Span<double> output, int period)
    {
        var n = input.Length;
        output.Slice(0, Math.Min(period - 1, n)).Clear();
        if (period > n) return;
        var firstLength = (int)((period + 1L) / 2);
        var secondLength = period - firstLength + 1;
        var reciprocal = 1.0 / ((double)firstLength * secondLength);
        // lag is the inner sum published secondLength bars ago, which is the one leaving the outer sum.
        double inner = 0, outer = 0, lag = 0;
        for (var i = 0; i < n; i++)
        {
            inner += input[i];
            if (i >= firstLength) inner -= input[i - firstLength];
            var trailing = i - secondLength;
            if (trailing >= 0)
            {
                lag += input[trailing];
                if (trailing >= firstLength) lag -= input[trailing - firstLength];
            }
            if (i < firstLength - 1) continue;
            outer += inner;
            if (i >= period) outer -= lag;
            if (i >= period - 1) output[i] = outer * reciprocal;
        }
    }

    private static void ClassicExponential(ReadOnlySpan<double> input, Span<double> output, int period, int order,
        bool firstSeed, int suppression, long offset, long first)
    {
        var n = input.Length;
        var alpha = 2.0 / (period + 1.0);
        var weight = 1.0 - alpha;
        var mature = (long)period + suppression;
        double v0 = 0, v1 = 0, v2 = 0, s0 = 0, s1 = 0, s2 = 0;
        long c0 = 0, c1 = 0, c2 = 0;
        var i = 0;
        // Seeding: each stage waits for its start, seeds from the first value or a simple mean, and is
        // withheld until it has matured. Runs until every stage is in its plain recurrence.
        for (; i < n; i++)
        {
            output[i] = 0;
            var value = input[i];
            if (!firstSeed && i < offset) continue;
            if (!Stage(ref c0, ref s0, ref v0, value, period, firstSeed, alpha, weight, mature)) continue;
            if (order >= 2)
            {
                if (i < offset + (period - 1L + suppression)) continue;
                if (!Stage(ref c1, ref s1, ref v1, v0, period, firstSeed, alpha, weight, mature)) continue;
            }
            if (order == 3)
            {
                if (i < offset + 2 * (period - 1L + suppression)) continue;
                if (!Stage(ref c2, ref s2, ref v2, v1, period, firstSeed, alpha, weight, mature)) continue;
            }
            if (i < first) continue;
            output[i] = order == 1 ? v0 : order == 2 ? 2 * v0 - v1 : 3 * (v0 - v1) + v2;
            var last = order == 1 ? c0 : order == 2 ? c1 : c2;
            if (last > period) { i++; break; }
        }
        // Steady state: each stage is a plain exponential average of the one before, so each runs over the
        // whole remainder in turn, four bars at a time, and the stages are combined afterwards.
        if (order == 1)
        {
            FastMath.BlendRun(weight, alpha, input, output, i, v0);
            return;
        }
        var count = n - i;
        if (count <= 0) return;
        var pool = System.Buffers.ArrayPool<double>.Shared;
        double[] rented0 = pool.Rent(count), rented1 = pool.Rent(count);
        var rented2 = order == 3 ? pool.Rent(count) : null;
        try
        {
            Span<double> stage0 = rented0.AsSpan(0, count), stage1 = rented1.AsSpan(0, count);
            var target = output.Slice(i, count);
            FastMath.BlendRun(weight, alpha, input.Slice(i, count), stage0, 0, v0);
            FastMath.BlendRun(weight, alpha, stage0, stage1, 0, v1);
            if (rented2 is null)
            {
                for (var k = 0; k < count; k++) target[k] = 2 * stage0[k] - stage1[k];
                return;
            }
            var stage2 = rented2.AsSpan(0, count);
            FastMath.BlendRun(weight, alpha, stage1, stage2, 0, v2);
            for (var k = 0; k < count; k++) target[k] = 3 * (stage0[k] - stage1[k]) + stage2[k];
        }
        finally
        {
            pool.Return(rented0);
            pool.Return(rented1);
            if (rented2 is not null) pool.Return(rented2);
        }
    }

    // One seeded exponential stage. Returns whether the stage has matured and may feed the next one.
    private static bool Stage(ref long count, ref double sum, ref double value, double input, int period, bool firstSeed,
        double alpha, double weight, long mature)
    {
        count++;
        if (firstSeed) value = count == 1 ? input : FastMath.Blend(weight, value, alpha * input);
        else if (count <= period)
        {
            sum += input;
            if (count == period) value = sum / period;
        }
        else value = FastMath.Blend(weight, value, alpha * input);
        return count >= mature;
    }

    /// <summary>Fast minus slow classic average, absolute or as a percentage of the slow one.</summary>
    internal static void PriceOscillator(ReadOnlySpan<double> input, Span<double> value, Span<double> defined,
        ClassicPriceOscillator owner)
    {
        var n = input.Length;
        var fast = FastMath.Allocate(n);
        ClassicAverage(input, fast, owner.Fast, owner.Fast.First);
        ClassicAverage(input, value, owner.Slow, owner.Slow.First);
        var start = (int)Math.Min(Math.Max(owner.Fast.First, owner.Slow.First), n);
        value.Slice(0, start).Clear();
        var slow = value.Slice(start, n - start);
        var quick = fast.AsSpan(start, n - start);
        if (owner.Percentage)
            for (var i = 0; i < slow.Length; i++)
                slow[i] = slow[i] == 0 ? 0 : 100 * (quick[i] - slow[i]) / slow[i];
        else FastMath.Subtract(quick, slow, slow);
        DefinedFrom(defined.Slice(0, n), start);
    }

    /// <summary>MACD over two aligned classic averages with a classic average of the oscillator as signal.</summary>
    internal static void ClassicMacd(ReadOnlySpan<double> input, double[][] outputs, ClassicMacd owner)
    {
        var n = input.Length;
        var oscillatorFirst = owner.OscillatorFirst;
        var start = (int)Math.Min(oscillatorFirst, n);
        var signalStart = (int)Math.Min(oscillatorFirst + owner.SignalAverage.First, n);
        var fast = FastMath.Allocate(n);
        var slow = FastMath.Allocate(n);
        ClassicAverage(input, fast, owner.FastAverage, oscillatorFirst);
        ClassicAverage(input, slow, owner.SlowAverage, oscillatorFirst);
        // The signal sees only the bars on which the oscillator exists, so it runs over that tail.
        var oscillator = fast.AsSpan(start, n - start);
        FastMath.Subtract(oscillator, slow.AsSpan(start, n - start), oscillator);
        var signal = slow.AsSpan(start, n - start);
        ClassicAverage(oscillator, signal, owner.SignalAverage, owner.SignalAverage.First);
        Span<double> line = outputs[0], average = outputs[1], histogram = outputs[2];
        line.Slice(0, signalStart).Clear();
        average.Slice(0, signalStart).Clear();
        histogram.Slice(0, signalStart).Clear();
        var count = n - signalStart;
        var skipped = signalStart - start;
        oscillator.Slice(skipped, count).CopyTo(line.Slice(signalStart));
        signal.Slice(skipped, count).CopyTo(average.Slice(signalStart));
        FastMath.Subtract(oscillator.Slice(skipped, count), signal.Slice(skipped, count), histogram.Slice(signalStart, count));
        for (var slot = 3; slot < 6; slot++) DefinedFrom(outputs[slot], signalStart);
    }

    /// <summary>MACD whose fast average starts late so both exponential averages seed on the same bar.</summary>
    internal static void AlignedMacd(ReadOnlySpan<double> input, double[][] outputs, AlignedEmaMacd owner)
    {
        var n = input.Length;
        for (var slot = 0; slot < 6; slot++) outputs[slot].AsSpan().Clear();
        var firstPrice = owner.FirstPrice;
        var fast = new Seeded(owner.FastPeriod, firstPrice, owner.FixedCoefficients ? .15 : 2.0 / (owner.FastPeriod + 1.0));
        var slow = new Seeded(owner.SlowPeriod, firstPrice, owner.FixedCoefficients ? .075 : 2.0 / (owner.SlowPeriod + 1.0));
        var signal = new Seeded(owner.SignalPeriod, firstPrice, 2.0 / (owner.SignalPeriod + 1.0));
        var delay = owner.SlowPeriod - owner.FastPeriod;
        var start = (long)owner.SlowPeriod - 1 + owner.Suppression;
        var publish = start + owner.SignalPeriod - 1L + owner.Suppression;
        var selection = (int)owner.Selection;
        bool wantLine = (selection & 1) != 0, wantSignal = (selection & 2) != 0, wantHistogram = (selection & 4) != 0;
        for (var i = 0; i < n; i++)
        {
            var price = input[i];
            slow.Add(price);
            if (firstPrice || i >= delay) fast.Add(price);
            if (i < start) continue;
            var difference = fast.Value - slow.Value;
            signal.Add(difference);
            if (i < publish) continue;
            if (wantLine) { outputs[0][i] = difference; outputs[3][i] = 1; }
            if (wantSignal) { outputs[1][i] = signal.Value; outputs[4][i] = 1; }
            if (wantHistogram) { outputs[2][i] = difference - signal.Value; outputs[5][i] = 1; }
        }
    }

    /// <summary>An exponential average seeded by its first value or by a simple mean of its first period.</summary>
    private struct Seeded(int period, bool first, double alpha)
    {
        private readonly int _seed = first ? 1 : period;
        private readonly double _weight = 1.0 - alpha;
        private int _count;
        private double _sum;
        internal double Value;

        internal void Add(double input)
        {
            if (_count < _seed)
            {
                _sum += input;
                if (++_count == _seed) Value = _sum / _seed;
                return;
            }
            Value = FastMath.Blend(_weight, Value, alpha * input);
        }
    }
}

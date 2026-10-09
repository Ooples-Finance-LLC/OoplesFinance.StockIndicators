using System.Numerics;
using System.Reflection;
using System.Runtime.Intrinsics;
using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

// Experimental code only; never dispatched by the production builder.
internal static class CpuFeasibilityPrototypes
{
    internal delegate void SmaCore(ReadOnlySpan<double> input, Span<double> output, int period);
    internal static readonly SmaCore CurrentSma = typeof(IIndicator).Assembly
        .GetType("OoplesFinance.StockIndicators.Core.MovingAverageCore", true)!
        .GetMethod("SimpleMovingAverage", BindingFlags.Static | BindingFlags.NonPublic)! // NOSONAR: S3011 - fixed diagnostic binding, outside timing.
        .CreateDelegate<SmaCore>();

    internal static void AsinUnrolled(ReadOnlySpan<double> input, Span<double> output)
    {
        var i = 0;
        for (; i <= input.Length - 4; i += 4)
        {
            output[i] = Math.Asin(input[i]);
            output[i + 1] = Math.Asin(input[i + 1]);
            output[i + 2] = Math.Asin(input[i + 2]);
            output[i + 3] = Math.Asin(input[i + 3]);
        }
        for (; i < input.Length; i++) output[i] = Math.Asin(input[i]);
    }

    internal static void AsinDefined(ReadOnlySpan<double> input, Span<double> output, Span<double> presence)
    {
        for (var i = 0; i < input.Length; i++)
        {
            var defined = input[i] is >= -1 and <= 1;
            output[i] = defined ? Math.Asin(input[i]) : 0;
            presence[i] = defined ? 1 : 0;
        }
    }

    internal static void AsinParallel(double[] input, double[] output)
    {
        // Independent calls preserve Math.Asin's result; scheduling allocations
        // and launch cost remain inside timing, including for tiny inputs.
        var workers = Math.Min(4, Environment.ProcessorCount);
        Parallel.For(0, workers, worker =>
        {
            var start = (int)((long)input.Length * worker / workers);
            var end = (int)((long)input.Length * (worker + 1) / workers);
            AsinUnrolled(input.AsSpan(start, end - start), output.AsSpan(start, end - start));
        });
    }

    internal static Bar[][] IngestArray(Bar[] input, CancellationToken cancellation = default, bool direct = false)
    {
        cancellation.ThrowIfCancellationRequested();
        var history = new Bar[(input.Length + 1023L) / 1024][];
        for (var chunk = 0; chunk < history.Length; chunk++)
        {
            var start = chunk * 1024;
            // Validate the owned copy, never claim validation of mutable source
            // memory remains valid when a later copy observes different values.
            var length = Math.Min(1024, input.Length - start);
            var owned = direct ? GC.AllocateUninitializedArray<Bar>(length) : new Bar[length];
            input.AsSpan(start, length).CopyTo(owned);
            for (var i = 0; i < owned.Length; i++)
            {
                cancellation.ThrowIfCancellationRequested();
                ref readonly var bar = ref owned[i];
                if (!direct || !double.IsFinite(bar.Open) || !double.IsFinite(bar.High)
                    || !double.IsFinite(bar.Low) || !double.IsFinite(bar.Close) || !double.IsFinite(bar.Volume))
                    OoplesFinance.StockIndicators.Validation.IndicatorInputDomain.Finite.Validate(in bar);
            }
            history[chunk] = owned;
        }
        return history;
    }

    internal static (Bar[][] History, double[] Values, double[] Presence) AsinOwnedPipeline(Bar[] input, bool parallel, bool direct = false)
    {
        var history = IngestArray(input, direct: direct);
        var values = new double[input.Length]; var presence = new double[input.Length];
        var workers = parallel ? Math.Min(4, Environment.ProcessorCount) : 1;
        void Compute(int worker)
        {
            for (var chunk = worker; chunk < history.Length; chunk += workers)
            {
                var bars = history[chunk];
                for (var i = 0; i < bars.Length; i++)
                {
                    var close = bars[i].Close;
                    var offset = chunk * 1024 + i;
                    if (close is >= -1 and <= 1)
                    {
                        values[offset] = Math.Asin(close);
                        presence[offset] = 1;
                    }
                }
            }
        }
        if (parallel) Parallel.For(0, workers, Compute);
        else Compute(0);
        return (history, values, presence);
    }

    internal static void SmaVector(ReadOnlySpan<double> input, Span<double> output, int period)
    {
        if (!TrySmaVector(input, output, period)) CurrentSma(input, output, period);
    }

    internal static bool TrySmaVector(ReadOnlySpan<double> input, Span<double> output, int period)
    {
        if (!Vector256.IsHardwareAccelerated || period < 2 || output.Length < input.Length || input.Overlaps(output)) return false;
        var grid = int.MaxValue;
        var largest = int.MinValue;
        // Four deltas each have two terms. Bound window + eight terms, including
        // all prefix-scan intermediates, to 53 significant binary digits.
        var termsBits = BitOperations.Log2((ulong)period + 8) + 1;
        foreach (var value in input)
        {
            var bits = (ulong)(BitConverter.DoubleToInt64Bits(value) & long.MaxValue);
            if (bits == 0) continue;
            var exponent = (int)(bits >> 52);
            if (exponent is 0 or 2047) return false;
            var significand = (bits & 0xfffffffffffffUL) | (1UL << 52);
            grid = Math.Min(grid, exponent - 1075 + BitOperations.TrailingZeroCount(significand));
            largest = Math.Max(largest, exponent - 1023);
            if (grid < -512 || largest > 500 || largest - grid + termsBits > 52) return false;
        }
        output.Slice(0, Math.Min(period - 1, input.Length)).Clear();
        if (input.Length < period) return true;
        double sum = 0;
        for (var j = 0; j < period; j++) sum += input[j];
        output[period - 1] = sum / period;
        var divisor = Vector256.Create((double)period);
        var i = period;
        for (; i <= input.Length - 4; i += 4)
        {
            var delta = Vector256.LoadUnsafe(ref System.Runtime.InteropServices.MemoryMarshal.GetReference(input), (nuint)i)
                - Vector256.LoadUnsafe(ref System.Runtime.InteropServices.MemoryMarshal.GetReference(input), (nuint)(i - period));
            delta += Vector256.Shuffle(delta, Vector256.Create(-1L, 0L, 1L, 2L));
            delta += Vector256.Shuffle(delta, Vector256.Create(-1L, -1L, 0L, 1L));
            var totals = Vector256.Create(sum) + delta;
            (totals / divisor).StoreUnsafe(ref System.Runtime.InteropServices.MemoryMarshal.GetReference(output), (nuint)i);
            sum = totals.GetElement(3);
        }
        for (; i < input.Length; i++)
        {
            sum += input[i];
            sum -= input[i - period];
            output[i] = sum / period;
        }
        return true;
    }
}

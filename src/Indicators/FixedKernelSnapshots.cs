using System.Numerics;
using OoplesFinance.StockIndicators.Helpers;

namespace OoplesFinance.StockIndicators.Indicators;

/// <summary>Exact Fibonacci weighting, newest first, normalized over available observations.</summary>
/// <remarks>Weights are F(period), F(period-1), ... with F(1)=F(2)=1.
/// Integer coefficients avoid floating Fibonacci overflow. The complete weighted
/// ratio rounds once. Two exact rolling sums avoid rescanning the window.</remarks>
public static class FibonacciWeightedSnapshot
{
    /// <summary>Calculates positive-period Fibonacci weights; period one is the identity.</summary>
    public static IReadOnlyList<double> Calculate(IReadOnlyList<Bar> bars, int period = 14)
    {
        FixedKernelSnapshotInput.Validate(bars);
        if (period < 1)
            throw new ArgumentOutOfRangeException(nameof(period));
        if (bars.Count == 0)
            return Array.Empty<double>();
#if !NETFRAMEWORK
        if (TryCalculateAligned(bars, period, out var aligned)) return aligned;
        if (period <= 78) return CalculateCompact(bars, period);
#endif
        BigInteger newest = 1, previous = 0;
        for (int i = 1; i < period; i++)
            (newest, previous) = (newest + previous, newest);
        BigInteger weighted = 0, shifted = 0, mass = 0;
        var nextWeight = newest;
        var followingWeight = previous;
        var output = new double[bars.Count];
        for (int i = 0; i < bars.Count; i++)
        {
            var value = ExactVarianceWindow.Units(bars[i].Close);
            var next = shifted + newest * value;
            var nextShifted = weighted - shifted + previous * value;
            if (i >= period)
                nextShifted -= ExactVarianceWindow.Units(bars[i - period].Close);
            else
            {
                mass += nextWeight;
                (nextWeight, followingWeight) = (followingWeight, nextWeight - followingWeight);
            }
            weighted = next;
            shifted = nextShifted;
            output[i] = ExactMeanAccumulator.UnitRatio(weighted, mass);
        }
        return output;
    }

#if !NETFRAMEWORK
    // Short batches can finish before tiered compilation optimizes this loop.
    // Keep the exact arithmetic, but compile this bounded path optimized immediately.
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveOptimization)]
    private static bool TryCalculateAligned(IReadOnlyList<Bar> bars, int period, out double[] output)
    {
        output = Array.Empty<double>();
        // Through period 44, the full coefficient mass is F(46)-1 < Int32.MaxValue.
        // A common grid spanning eight binary exponents has at most 61 input bits.
        // Weighted sums need fewer than 92 magnitude bits; recurrence intermediates
        // need fewer than 94. Every operation below is exact inside Int128.
        if (period > 44) return false;
        int minimum = 2047, maximum = 0;
        for (int i = 0; i < bars.Count; i++)
        {
            double value = bars[i].Close;
            if (value == 0) continue;
            int exponent = (int)((BitConverter.DoubleToInt64Bits(value) >> 52) & 2047);
            if (exponent is 0 or 2047) return false;
            minimum = Math.Min(minimum, exponent);
            maximum = Math.Max(maximum, exponent);
            if (maximum - minimum > 8) return false;
        }
        output = new double[bars.Count];
        if (maximum == 0) return true;
        long newest = 1, previous = 0;
        for (int i = 1; i < period; i++)
            (newest, previous) = (newest + previous, newest);
        long nextWeight = newest, followingWeight = previous, mass = 0;
        Int128 weighted = 0, shifted = 0;
        int scale = minimum - 1075;
        for (int i = 0; i < bars.Count; i++)
        {
            long value = AlignedUnits(bars[i].Close, minimum);
            Int128 next = shifted + (Int128)newest * value;
            Int128 nextShifted = weighted - shifted + (Int128)previous * value;
            if (i >= period) nextShifted -= AlignedUnits(bars[i - period].Close, minimum);
            else
            {
                mass += nextWeight;
                (nextWeight, followingWeight) = (followingWeight, nextWeight - followingWeight);
            }
            weighted = next;
            shifted = nextShifted;
            // Publish the complete exact ratio once, including subnormal results
            // caused by cancellation. No rounded quotient is scaled afterwards.
            output[i] = JurikDyadic.FromBinaryProduct(weighted, scale).Mean((int)mass);
        }
        return true;
    }

    private static long AlignedUnits(double value, int minimum)
    {
        if (value == 0) return 0;
        long bits = BitConverter.DoubleToInt64Bits(value);
        int exponent = (int)((bits >> 52) & 2047);
        long units = ((bits & 0xfffffffffffffL) | (1L << 52)) << (exponent - minimum);
        return bits < 0 ? -units : units;
    }

    private static double[] CalculateCompact(IReadOnlyList<Bar> bars, int period)
    {
        // F(78) is the last Fibonacci number below 2^53. These coefficient
        // conversions are exact; dyadic sums retain every bit and widen on demand.
        long newest = 1, previous = 0;
        for (int i = 1; i < period; i++)
            (newest, previous) = (newest + previous, newest);
        long nextWeight = newest, followingWeight = previous;
        JurikDyadic weighted = default, shifted = default, mass = default;
        var output = new double[bars.Count];
        for (int i = 0; i < bars.Count; i++)
        {
            var value = JurikDyadic.FromDouble(bars[i].Close);
            var next = value;
            next.Multiply(newest);
            next.AddExact(shifted);
            var nextShifted = weighted;
            nextShifted.Subtract(shifted);
            if (i >= period) nextShifted.Subtract(JurikDyadic.FromDouble(bars[i - period].Close));
            else
            {
                mass.Add(nextWeight);
                (nextWeight, followingWeight) = (followingWeight, nextWeight - followingWeight);
            }
            value.Multiply(previous);
            nextShifted.AddExact(value);
            weighted = next;
            shifted = nextShifted;
            output[i] = weighted.Ratio(mass);
        }
        return output;
    }
#endif
}

/// <summary>Window functions for the oldest-first sinc filter.</summary>
public enum SincWindow
{
    /// <summary>Constant window.</summary>
    Rectangular,

    /// <summary>0.50 - 0.50*cos(angle).</summary>
    Hann,

    /// <summary>0.54 - 0.46*cos(angle).</summary>
    Hamming,

    /// <summary>Three-term Blackman window.</summary>
    Blackman,

    /// <summary>Four-term Blackman-Harris window.</summary>
    BlackmanHarris,
}

/// <summary>Oldest-first windowed-sinc filtering with newest-value extension during startup.</summary>
/// <remarks>Corresponds to Afirma's published windowed-sinc response; its unused
/// cubic extrapolation does not affect the output. Binary64 trigonometric values
/// define coefficients. Their complete weighted ratio rounds once; zero or
/// nonfinite kernels are rejected. Coefficients require storage proportional to taps.</remarks>
public static class WindowedSincSnapshot
{
    /// <summary>Calculates a positive-period, positive-tap sinc response.</summary>
    public static IReadOnlyList<double> Calculate(
        IReadOnlyList<Bar> bars,
        int period = 14,
        int taps = 21,
        SincWindow window = SincWindow.Hann
    )
    {
        FixedKernelSnapshotInput.Validate(bars);
        ValidateParameters(period, taps, window);
        if (bars.Count == 0) return Array.Empty<double>();
        var weights = CreateWeights(period, taps, window, out var mass);
        var output = new double[bars.Count];
        for (var i = 0; i < bars.Count; i++)
        {
            var sum = new ExactMeanAccumulator();
            var start = Math.Max(0, i - taps + 1);
            for (var k = 0; k < taps; k++)
                sum.AddProduct(bars[Math.Min(i, start + k)].Close, weights[k]);
            output[i] = sum.Ratio(mass);
            if (!FrameworkCompatibility.IsFinite(output[i]))
                throw new OverflowException("Sinc output is not representable.");
        }
        return output;
    }

    internal static void ValidateParameters(int period, int taps, SincWindow window)
    {
        if (period < 1)
            throw new ArgumentOutOfRangeException(nameof(period));
        if (
            taps < 1
            || taps == 1 && window != SincWindow.Rectangular
            || taps == 2 && window == SincWindow.Hann
        )
            throw new ArgumentOutOfRangeException(
                nameof(taps),
                "The selected window must define a nonzero kernel."
            );
        if (!Enum.IsDefined(window.GetType(), window))
            throw new ArgumentOutOfRangeException(nameof(window));
    }

    internal static double[] CreateWeights(int period, int taps, SincWindow window, out ExactMeanAccumulator mass)
    {
        var weights = new double[taps];
        mass = new ExactMeanAccumulator();
        var center = (taps - 1) / 2d;
        for (var k = 0; k < taps; k++)
        {
            var w = window switch
            {
                SincWindow.Rectangular => 1,
                SincWindow.Hann => .50 - .50 * Math.Cos(2 * Math.PI * k / (taps - 1)),
                SincWindow.Hamming => .54 - .46 * Math.Cos(2 * Math.PI * k / (taps - 1)),
                SincWindow.Blackman => .42
                    - .50 * Math.Cos(2 * Math.PI * k / (taps - 1))
                    + .08 * Math.Cos(4 * Math.PI * k / (taps - 1)),
                _ => .35875
                    - .48829 * Math.Cos(2 * Math.PI * k / (taps - 1))
                    + .14128 * Math.Cos(4 * Math.PI * k / (taps - 1))
                    - .01168 * Math.Cos(6 * Math.PI * k / (taps - 1)),
            };
            var x = Math.PI * (k - center) / period;
            weights[k] = w * (k == center ? 1 : Math.Sin(x) / x); // NOSONAR: S1244 - Integer/half-integer tap positions identify the exact sinc center.
            if (!FrameworkCompatibility.IsFinite(weights[k]))
                throw new ArgumentException("The selected kernel is undefined.");
            mass.Add(weights[k]);
        }
        if (mass.IsExactlyZero)
            throw new ArgumentException("The selected kernel has zero mass.");
        return weights;
    }

}

internal static class FixedKernelSnapshotInput
{
    internal static void Validate(IReadOnlyList<Bar> bars)
    {
        if (bars is null) throw new ArgumentNullException(nameof(bars));
        foreach (var bar in bars)
            if (!FrameworkCompatibility.IsFinite(bar.Close))
                throw new ArgumentOutOfRangeException(nameof(bars));
    }
}

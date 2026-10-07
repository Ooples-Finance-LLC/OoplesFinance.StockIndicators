using System.Numerics;
using OoplesFinance.StockIndicators.Helpers;

namespace OoplesFinance.StockIndicators.Indicators;

/// <summary>Exact Fibonacci weighting, newest first, normalized over available observations.</summary>
/// <remarks>Weights are F(period), F(period-1), ... with F(1)=F(2)=1.
/// Integer coefficients avoid floating Fibonacci overflow. The complete weighted
/// ratio rounds once. Coefficient storage grows with the requested period.</remarks>
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
        var weights = new BigInteger[period];
        weights[0] = 1;
        if (period > 1)
            weights[1] = 1;
        for (var i = 2; i < period; i++)
            weights[i] = weights[i - 1] + weights[i - 2];
        var output = new double[bars.Count];
        for (var i = 0; i < bars.Count; i++)
        {
            var total = BigInteger.Zero;
            var mass = BigInteger.Zero;
            for (var lag = 0; lag < Math.Min(i + 1, period); lag++)
            {
                var weight = weights[period - 1 - lag];
                total += weight * ExactVarianceWindow.Units(bars[i - lag].Close);
                mass += weight;
            }
            output[i] = ExactMeanAccumulator.UnitRatio(total, mass);
        }
        return output;
    }
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
        if (bars.Count == 0)
            return Array.Empty<double>();
        var weights = new double[taps];
        var mass = new ExactMeanAccumulator();
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

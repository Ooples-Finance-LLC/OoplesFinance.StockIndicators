namespace OoplesFinance.StockIndicators.Validation;

/// <summary>Explicit startup representation. Infinity is never an unavailable-value marker.</summary>
public enum IndicatorStartupPolicy
{
    Finite,
    NaN,
    FiniteOrNaN
}

/// <summary>Optional per-output startup policy; the default is finite on every bar.</summary>
public interface IIndicatorStartupContract
{
    IndicatorStartupPolicy StartupPolicy(int outputSlot);
}

/// <summary>An output-specific comparison budget, not a proof of an error bound.</summary>
public sealed class IndicatorErrorBudget
{
    public IndicatorErrorBudget(double absoluteTolerance, double relativeTolerance, bool requireSameSign = false)
    {
        if (!Finite(absoluteTolerance) || absoluteTolerance < 0)
            throw new ArgumentOutOfRangeException(nameof(absoluteTolerance));
        if (!Finite(relativeTolerance) || relativeTolerance < 0 || relativeTolerance >= 1)
            throw new ArgumentOutOfRangeException(nameof(relativeTolerance));
        AbsoluteTolerance = absoluteTolerance;
        RelativeTolerance = relativeTolerance;
        RequireSameSign = requireSameSign;
    }

    public double AbsoluteTolerance { get; }
    public double RelativeTolerance { get; }
    /// <summary>Also rejects erasing a nonzero signal to zero, regardless of the magnitude tolerance.</summary>
    public bool RequireSameSign { get; }
    public static IndicatorErrorBudget Exact { get; } = new(0, 0, true);

    public bool Accepts(double expected, double actual)
    {
        if (!Finite(expected) || !Finite(actual)) return false;
        if (RequireSameSign && Math.Sign(expected) != Math.Sign(actual)) return false;
        if (expected == actual) return true; // NOSONAR: S1244 - Exact equality is the fast path; configured tolerances are evaluated below.
        // Normalize to avoid overflow both in subtraction and in the allowed error.
        var scale = Math.Max(Math.Abs(expected), Math.Abs(actual));
        return Math.Abs(expected / scale - actual / scale)
            <= AbsoluteTolerance / scale + RelativeTolerance * (Math.Abs(expected) / scale);
    }

    private static bool Finite(double value) => !double.IsNaN(value) && !double.IsInfinity(value);
}

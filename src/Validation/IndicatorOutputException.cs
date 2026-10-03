using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.Validation;

/// <summary>A calculation produced a value outside its declared output/startup policy.</summary>
public sealed class IndicatorOutputException : ArithmeticException
{
    internal IndicatorOutputException(IIndicator indicator, int slot, int index, double value, IndicatorStartupPolicy policy)
        : base($"{indicator.GetType().FullName}: Output {slot}, bar {index}: {value:R}; required policy {policy}.")
    { IndicatorType = indicator.GetType(); OutputSlot = slot; BarIndex = index; Value = value; Policy = policy; }
    public Type IndicatorType { get; }
    public int OutputSlot { get; }
    public int BarIndex { get; }
    public double Value { get; }
    public IndicatorStartupPolicy Policy { get; }
}

internal static class IndicatorOutputPolicy
{
    internal static void Validate(IIndicator indicator, int slot, int index, double value)
    {
        var policy = index < indicator.WarmupBars && indicator is IIndicatorStartupContract startup
            ? startup.StartupPolicy(slot) : IndicatorStartupPolicy.Finite;
        if (policy is not (IndicatorStartupPolicy.Finite or IndicatorStartupPolicy.NaN or IndicatorStartupPolicy.FiniteOrNaN))
            throw new InvalidOperationException("Unknown startup policy for " + indicator.GetType().FullName);
        if (double.IsInfinity(value) || (double.IsNaN(value) ? policy == IndicatorStartupPolicy.Finite : policy == IndicatorStartupPolicy.NaN))
            throw new IndicatorOutputException(indicator, slot, index, value, policy);
    }
}

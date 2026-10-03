namespace OoplesFinance.StockIndicators.Builder.Specs;

/// <summary>Confirmed five-bar fractal support and resistance.</summary>
public sealed class FractalChaosBandsSpecOptions : IIndicatorSpecOptions { }

/// <summary>Recursive bands with gain no greater than one half, preserving band order.</summary>
public sealed class ExtendedRecursiveBandsSpecOptions : IIndicatorSpecOptions
{
    public ExtendedRecursiveBandsSpecOptions(int length = 100) => Length = Math.Max(3, length);
    public int Length { get; }
}

/// <summary>Window of the repeated detrending and extrapolation regressions.</summary>
public sealed class ZeroLagSmoothedCycleSpecOptions : IIndicatorSpecOptions
{
    public ZeroLagSmoothedCycleSpecOptions(int length = 100) => Length = Math.Max(1, length);
    public int Length { get; }
}

/// <summary>Window of dispersion used for envelope decay and trend-biased levels.</summary>
public sealed class FlaggingBandsSpecOptions : IIndicatorSpecOptions
{
    public FlaggingBandsSpecOptions(int length = 14) => Length = Math.Max(1, length);
    public int Length { get; }
}

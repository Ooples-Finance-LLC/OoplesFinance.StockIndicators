namespace OoplesFinance.StockIndicators.Indicators.Fast;

/// <summary>
/// Raised by a kernel that has met a value its own rounding would decide: where a series sits in a range
/// no wider than the rounding it carries, or whether something grew by less than that. Floating point has
/// no answer there, and the exact route does, so the build is handed to it. Prices that move do not come
/// here; a series that stands still, or repeats exactly, can.
/// </summary>
internal sealed class FastUndecidedException : Exception
{
    private FastUndecidedException() : base("The fast route left this build to the exact route.") { }

    // A range is held when it is at least this fraction of the values it lies between. The series under it
    // carry rounding of a few hundred units in the last place, and a position in the range is published to
    // nine digits, so a narrower range would publish its rounding.
    internal const double RangeHeld = 1.0 / (1 << 16);

    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    internal static FastUndecidedException Raise() => new();
}

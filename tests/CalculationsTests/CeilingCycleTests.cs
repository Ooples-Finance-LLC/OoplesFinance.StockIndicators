using OoplesFinance.StockIndicators.Helpers;

namespace OoplesFinance.StockIndicators.Tests.Unit.CalculationsTests;

/// <summary>
/// A measured cycle becomes a whole number of bars without float noise choosing the window.
/// </summary>
public sealed class CeilingCycleTests
{
    [Theory]
    [InlineData(29.0, 29)]
    // One ulp above 29: the value the streaming periodogram produced where the batch produced 29 exactly,
    // which made Ehlers ACCI V2 average over 30 bars in one engine and 29 in the other.
    [InlineData(29.000000000000004, 29)]
    [InlineData(28.999999999999996, 29)]
    [InlineData(29.2, 30)]
    [InlineData(29.000001, 30)]
    [InlineData(10.5, 11)]
    [InlineData(0.0, 0)]
    public void RoundsUpUnlessWithinNoiseOfAnInteger(double cycle, int expected)
    {
        MathHelper.CeilingCycle(cycle).Should().Be(expected);
    }
}

using OoplesFinance.StockIndicators.Helpers;

namespace OoplesFinance.StockIndicators.Tests.Unit.CalculationsTests;

/// <summary>
/// The swing index is Wilder's, from New Concepts in Technical Trading Systems (1978).
/// </summary>
/// <remarks>
/// Worked by hand from his definition for one bar: yesterday opened at 10 and closed at 11; today opened at
/// 11.5, ranged 12.5 to 10.5 and closed at 12. The moves from yesterday's close are |H - Cy| = 1.5 and
/// |L - Cy| = 0.5, and today's range of 2 is the largest, so R = 2 + 0.25 * |Cy - Oy| = 2.25 and K = 1.5. The
/// numerator (C - Cy) + 0.5 * (C - O) + 0.25 * (Cy - Oy) is 1 + 0.25 + 0.25 = 1.5.
/// </remarks>
public sealed class WilderSwingIndexTests
{
    private const double Tolerance = 1e-12;

    [Fact]
    public void WithoutALimitMoveTheBarRangeStandsInForIt()
    {
        // 50 * (1.5 / 2.25) * (1.5 / 2)
        WilderSwingIndex.Compute(11.5, 12.5, 10.5, 12, 10, 11, limitMove: 0).Should().BeApproximately(25, Tolerance);
    }

    [Fact]
    public void ALimitMoveScalesTheIndex()
    {
        // 50 * (1.5 / 2.25) * (1.5 / 3)
        WilderSwingIndex.Compute(11.5, 12.5, 10.5, 12, 10, 11, limitMove: 3).Should().BeApproximately(50d / 3, Tolerance);
    }

    [Fact]
    public void AMoveDownIsNegative()
    {
        // The mirror of the bar above about yesterday's close: every price difference changes sign, the sizes
        // do not, so the index is -25.
        WilderSwingIndex.Compute(10.5, 11.5, 9.5, 10, 12, 11, limitMove: 0).Should().BeApproximately(-25, Tolerance);
    }

    [Fact]
    public void RIsTakenFromTheLargestMove()
    {
        // A gap up: |H - Cy| = 4 is the largest, so R = 4 - 0.5 * |L - Cy| + 0.25 * |Cy - Oy| = 4 - 1 + 0.25 = 3.25.
        // K = 4, T = range = 2, numerator = (14 - 11) + 0.5 * (14 - 13) + 0.25 * (11 - 10) = 3.75.
        WilderSwingIndex.Compute(13, 15, 13, 14, 10, 11, limitMove: 0).Should().BeApproximately(50 * (3.75 / 3.25) * (4d / 2), Tolerance);
    }
}

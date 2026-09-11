using OoplesFinance.StockIndicators.Helpers;
using static OoplesFinance.StockIndicators.Tests.Unit.StreamingTests.IndicatorRunner;

namespace OoplesFinance.StockIndicators.Tests.Unit.CalculationsTests;

/// <summary>
/// Every moving-average type means one average: the fast path computes exactly what the indicator of the
/// same name computes.
/// </summary>
/// <remarks>
/// <para>
/// Indicators take a <see cref="MovingAvgType"/> and smooth through <c>GetMovingAverageList</c>, which for
/// most types runs a span-based fast path rather than the indicator itself. Nothing held the two together:
/// the variable moving average's fast path was an EMA weighted by sd / (sd + 0.001), VIDYA's used a fixed
/// 9-bar CMO and a different seed, and McNicholl's returned ema + EMA(price - ema). Each looked like its
/// indicator only in name, and the streaming twins, written against the indicators, disagreed with every
/// batch indicator that smoothed through them.
/// </para>
/// <para>
/// Checked on every bar of the fixture and every type, with no list of exceptions.
/// </para>
/// </remarks>
public sealed class MovingAverageFastPathTests : GlobalTestData
{
    private const int Length = 14;

    public static IEnumerable<object[]> AllTypes =>
        Enum.GetValues(typeof(MovingAvgType)).Cast<MovingAvgType>().Select(type => new object[] { type });

    [Theory]
    [MemberData(nameof(AllTypes))]
    public void TheFastPathComputesTheIndicatorOfTheSameName(MovingAvgType type)
    {
        var fast = CalculationsHelper.GetMovingAverageList(new StockData(StockTestData), type, Length);
        var reference = CalculationsHelper.GetMovingAverageListByCalculation(new StockData(StockTestData), type, Length);

        if (reference.Count == 0)
        {
            // No indicator of this name (TrueRangeAdjustedExponentialMovingAverage exists only as a fast
            // path), so there is nothing to agree with - but the type must still compute, or a type both
            // routes fail to support would pass as "equal".
            fast.Should().HaveCount(StockTestData.Count, $"{type} has no indicator, so its fast path must compute it");
            return;
        }

        fast.Should().HaveCount(reference.Count);
        for (var i = 0; i < reference.Count; i++)
        {
            // The rule the engine parity tests use: 1e-9 of the larger magnitude plus a 1e-12 floor.
            IsClose(reference[i], fast[i]).Should().BeTrue($"{type} at bar {i}: fast path {fast[i]}, indicator {reference[i]}");
        }
    }
}

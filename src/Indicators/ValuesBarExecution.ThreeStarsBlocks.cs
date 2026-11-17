#if !NETFRAMEWORK
namespace OoplesFinance.StockIndicators.Indicators;

internal static partial class ValuesBarExecution
{
    // Only necessary comparisons from ExhaustionCandleState.Matches. A failed
    // conjunction proves zero; a possible match still runs the original state.
    private static bool ThreeStarsInSouthBlockGeometry(in Bar a, in Bar b, in Bar c) =>
        a.Close < a.Open
        && b.Close < b.Open
        && c.Close < c.Open
        && b.Open > a.Close
        && b.Open <= a.High
        && b.Low < a.Close
        && b.Low >= a.Low
        && c.Low > b.Low
        && c.High < b.High;
}
#endif

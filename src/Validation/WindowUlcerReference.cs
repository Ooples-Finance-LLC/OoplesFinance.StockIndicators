using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.Validation;

internal static class WindowUlcerReference
{
    internal static double?[] Calculate(IReadOnlyList<Bar> bars, int period)
    {
        var result = new double?[bars.Count];
        for (var i = period - 1; i < bars.Count; i++)
        {
            var squares = new ReferenceFraction(0);
            var defined = true;
            for (var j = i - period + 1; j <= i; j++)
            {
                var peak = Math.Max(
                    0,
                    bars.Skip(i - period + 1).Take(j - (i - period + 1) + 1).Max(b => b.Close)
                );
                if (peak == 0)
                {
                    defined = false;
                    break;
                } // NOSONAR: Zero is the formula's exact absent-peak boundary.
                var drawdown = (
                    new ReferenceFraction(100)
                    * (
                        ReferenceFraction.FromDouble(bars[j].Close)
                        - ReferenceFraction.FromDouble(peak)
                    )
                    / ReferenceFraction.FromDouble(peak)
                ).RoundExtendedBinary64();
                squares += drawdown * drawdown;
            }
            if (defined)
                result[i] = (squares / new ReferenceFraction(period)).SqrtToDouble();
        }
        return result;
    }
}

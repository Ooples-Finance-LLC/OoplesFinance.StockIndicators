using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.Validation;

internal static class LogVolatilityReference
{
    internal static double?[] Values(
        IReadOnlyList<Bar> bars,
        int period,
        bool realized,
        bool annualized
    )
    {
        var result = Enumerable.Repeat((double?)0, bars.Count).ToArray();
        var history = new List<double?>();
        var poisoned = false;
        for (var i = 0; i < bars.Count; i++)
        {
            var previous = i == 0 ? 0 : bars[i - 1].Close;
            var value = bars[i].Close;
            if (previous != 0) // NOSONAR: Exact zero boundary or certified equal rounded endpoints.
            {
                double? change = null;
                if (value != 0 && Math.Sign(value) == Math.Sign(previous)) // NOSONAR: Exact zero boundary or certified equal rounded endpoints.
                    change = (
                        ReferenceFraction.FromDouble(Math.Abs(value))
                        / ReferenceFraction.FromDouble(Math.Abs(previous))
                    ).LogToDouble();
                else
                    poisoned = true;
                history.Add(change);
            }
            else if (realized)
                continue;
            if (history.Count < period)
                continue;
            var window = history.Skip(history.Count - period).ToArray();
            if (window.Any(v => !v.HasValue) || (realized && poisoned))
            {
                result[i] = null;
                continue;
            }
            var values = window.Select(v => ReferenceFraction.FromDouble(v!.Value)).ToArray();
            var mean = realized
                ? new ReferenceFraction(0)
                : values.Aggregate(new ReferenceFraction(0), (s, v) => s + v)
                    / new ReferenceFraction(period);
            var variance =
                values.Aggregate(new ReferenceFraction(0), (s, v) => s + (v - mean) * (v - mean))
                / new ReferenceFraction(realized ? period : period - 1);
            result[i] = (variance * new ReferenceFraction(annualized ? 252 : 1)).SqrtToDouble();
        }
        return result;
    }
}

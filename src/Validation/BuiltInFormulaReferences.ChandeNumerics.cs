using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.Validation;

internal static partial class BuiltInFormulaReferences
{
    internal static double[] RoundedChandeAverage(IReadOnlyList<Bar> bars, int length1 = 5, int length2 = 10,
        int length3 = 20, bool absolute = false)
    {
        var prices = bars.Select(b => ReferenceFraction.FromDouble(b.Close)).ToArray();
        var changes = prices.Select((p, i) => p - (i == 0 ? new ReferenceFraction(0) : prices[i - 1])).ToArray();
        return Enumerable.Range(0, bars.Count).Select(i =>
        {
            var sum = new ReferenceFraction(0);
            foreach (var length in new[] { length1, length2, length3 })
            {
                var window = changes.Skip(Math.Max(0, i - length + 1)).Take(Math.Min(length, i + 1)).ToArray();
                var travel = window.Aggregate(new ReferenceFraction(0), (total, d) => total + d.Abs());
                var signed = window.Aggregate(new ReferenceFraction(0), (total, d) => total + d);
                sum += ReferenceFraction.FromDouble(travel.Sign == 0 ? 0 : (signed / travel).ToDouble());
            }
            var value = (new ReferenceFraction(100) * sum / new ReferenceFraction(3)).ToDouble();
            return absolute ? Math.Abs(value) : value;
        }).ToArray();
    }

    internal static bool HasBoundedFilteredChande(IBuiltInIndicator indicator) =>
        indicator.BatchName == IndicatorName.ChandeMomentumOscillatorFilter
        && BoundedMeanKind(indicator.CreateOptions(), 2) is 1 or 2 or 3 or 6 or 7 or 8 or 9 or 10 or 11 or 12 or 13 or 14 or 15 or 16 or 17 or 18 or 19 or 20 or 21;

    internal static double[] RoundedFilteredChande(IReadOnlyList<Bar> bars, int length, double filter = 3)
    {
        var prices = bars.Select(b => ReferenceFraction.FromDouble(b.Close)).ToArray();
        var changes = prices.Select((p, i) => i == 0 ? new ReferenceFraction(0) : p - prices[i - 1])
            .Select(change => Math.Abs(change.ToDouble()) > filter ? new ReferenceFraction(0) : change).ToArray();
        return Enumerable.Range(0, bars.Count).Select(i =>
        {
            var window = changes.Skip(Math.Max(0, i - length + 1)).Take(Math.Min(length, i + 1)).ToArray();
            var travel = window.Aggregate(new ReferenceFraction(0), (sum, d) => sum + d.Abs());
            var signed = window.Aggregate(new ReferenceFraction(0), (sum, d) => sum + d);
            return travel.Sign == 0 ? 0 : (new ReferenceFraction(100) * signed / travel).ToDouble();
        }).ToArray();
    }

    internal static double[] RoundedFilteredChandeSignal(IReadOnlyList<Bar> bars, int length, int kind)
        => RoundedBoundedStage(RoundedFilteredChande(bars, length), length, kind);

    internal static double[] RoundedAbsoluteChande(IReadOnlyList<Bar> bars, int length)
    {
        var prices = bars.Select(b => ReferenceFraction.FromDouble(b.Close)).ToArray();
        return Enumerable.Range(0, bars.Count).Select(i =>
        {
            if (i < length) return 0d;
            var travel = new ReferenceFraction(0);
            for (var j = i - length + 1; j <= i; j++) travel += (prices[j] - prices[j - 1]).Abs();
            return travel.Sign == 0 ? 0 : (new ReferenceFraction(100) * (prices[i] - prices[i - length]).Abs() / travel).ToDouble();
        }).ToArray();
    }

    internal static bool HasBoundedChande(IBuiltInIndicator indicator) =>
        indicator.BatchName == IndicatorName.ChandeMomentumOscillator
        && BoundedMeanKind(indicator.CreateOptions(), 3) is 1 or 2 or 3 or 6 or 7 or 8 or 9 or 10 or 11 or 12 or 13 or 14 or 15 or 16 or 17 or 18 or 19 or 20 or 21;

    internal static double[] RoundedChande(IReadOnlyList<Bar> bars, int length)
    {
        var prices = bars.Select(b => ReferenceFraction.FromDouble(b.Close)).ToArray();
        var changes = prices.Select((p, i) => i == 0 ? new ReferenceFraction(0) : p - prices[i - 1]).ToArray();
        return Enumerable.Range(0, bars.Count).Select(i =>
        {
            var window = changes.Skip(Math.Max(0, i - length + 1)).Take(Math.Min(length, i + 1)).ToArray();
            var mass = window.Aggregate(new ReferenceFraction(0), (sum, d) => sum + d.Abs());
            var signed = window.Aggregate(new ReferenceFraction(0), (sum, d) => sum + d);
            return mass.Sign == 0 ? 0 : (new ReferenceFraction(100) * signed / mass).ToDouble();
        }).ToArray();
    }

    internal static double[] RoundedChandeSignal(IReadOnlyList<Bar> bars, int length, int signalLength, int kind)
        => RoundedBoundedStage(RoundedChande(bars, length), signalLength, kind);
}

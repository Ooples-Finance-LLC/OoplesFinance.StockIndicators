using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.Validation;

internal static class VariablePeriodAverageReference
{
    internal static double?[] Values(IReadOnlyList<Bar> bars, VariablePeriodClassicAverage owner)
    {
        var output = new double?[bars.Count];
        var alternatives = new Dictionary<int, ReferenceFraction?[]>();
        for (var i = (int)Math.Min(bars.Count, owner.MaximumAverage.First); i < bars.Count; i++)
        {
            var selected = Math.Truncate(owner.PeriodSelector(bars[i]));
            if (!FrameworkCompatibility.IsFinite(selected))
                throw new ArgumentOutOfRangeException(nameof(owner.PeriodSelector));
            var period = (int)FrameworkCompatibility.Clamp(selected, owner.MinimumPeriod, owner.MaximumPeriod);
            if (!alternatives.TryGetValue(period, out var values))
            {
                var average = new ClassicMovingAverage(
                    period,
                    owner.Method,
                    owner.FirstPriceSeed,
                    owner.Suppression
                );
                values = ClassicAverageReference.AlignedExtendedValues(
                    bars,
                    average,
                    owner.MaximumAverage.First
                );
                alternatives.Add(period, values);
            }
            output[i] = values[i]?.ToDouble();
        }
        return output;
    }
}

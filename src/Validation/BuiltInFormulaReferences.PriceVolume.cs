using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.Validation;

internal static partial class BuiltInFormulaReferences
{
    internal static IReadOnlyDictionary<string, double[]> PriceVolumeOutputs(IReadOnlyList<Bar> bars, IBuiltInIndicator indicator, int volumeLength = 14)
    {
        var length = Integer(indicator.CreateOptions(), "Length", 50); var zero = new ReferenceFraction(0);
        double[] Leg(double[] values, int period)
        {
            var changes = values.Select((v, i) => i < period ? zero : ReferenceFraction.FromDouble(v) - ReferenceFraction.FromDouble(values[i - period])).ToArray();
            return changes.Select((_, i) =>
            {
                var current = Window(changes, i, period).ToArray();
                var total = current.Aggregate(zero, (a, b) => a + b);
                var magnitude = current.Aggregate(zero, (a, b) => a + b.Abs());
                return magnitude.Sign == 0 ? 0 : (total / magnitude).ToDouble();
            }).ToArray();
        }
        return Outputs(("Po", Leg(bars.Select(b => b.Close).ToArray(), length)), ("Vo", Leg(bars.Select(b => b.Volume).ToArray(), volumeLength)));
    }
}

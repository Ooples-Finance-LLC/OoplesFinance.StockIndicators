using OoplesFinance.StockIndicators.Indicators;
namespace OoplesFinance.StockIndicators.Validation;
internal static partial class BuiltInFormulaReferences
{
    internal static (double[] Roof, double[] Zero) HpLpRoofingOutputs(IReadOnlyList<Bar> bars, int length1 = 48, int length2 = 10)
    {
        var argument = Math.Min(2 * Math.PI / Math.Max(1, length1), .99); var cosine = Math.Cos(argument);
        var alpha = cosine != 0 ? (cosine + Math.Sin(argument) - 1) / cosine : 0;
        var angle = Math.Sqrt(2) * Math.PI / Math.Max(1, length2); var decay = Math.Exp(-angle);
        var c2 = 2 * decay * Math.Cos(Math.Min(angle, .99)); var c3 = -decay * decay; var c1 = 1 - c2 - c3;
        var high = new ReferenceFraction[bars.Count]; var roof = new ReferenceFraction[bars.Count]; var zero = new ReferenceFraction[bars.Count]; var output = new double[bars.Count]; var zeroOutput = new double[bars.Count];
        ReferenceFraction Round(ReferenceFraction v) => RoundRocBankStage(v);
        ReferenceFraction Product(ReferenceFraction v, double factor) => Round(v * ReferenceFraction.FromDouble(factor));
        ReferenceFraction Previous(ReferenceFraction[] v, int i) => i >= 0 ? v[i] : new ReferenceFraction(0);
        for (var i = 0; i < bars.Count; i++)
        {
            var change = i == 0 ? new ReferenceFraction(0) : Round(ReferenceFraction.FromDouble(bars[i].Close) - ReferenceFraction.FromDouble(bars[i - 1].Close));
            high[i] = Round(Product(change, 1 - alpha / 2) + Product(Previous(high, i - 1), 1 - alpha));
            var pair = Product(Round(high[i] + Previous(high, i - 1)), .5);
            roof[i] = Round(Round(Product(pair, c1) + Product(Previous(roof, i - 1), c2)) + Product(Previous(roof, i - 2), c3));
            zero[i] = Round(Product(Round(roof[i] - Previous(roof, i - 1)), 1 - alpha / 2) + Product(Previous(zero, i - 1), 1 - alpha));
            output[i] = roof[i].ToDouble(); zeroOutput[i] = zero[i].ToDouble();
        }
        return (output, zeroOutput);
    }
}

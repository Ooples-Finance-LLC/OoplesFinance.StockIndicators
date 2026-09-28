using OoplesFinance.StockIndicators.Indicators;
namespace OoplesFinance.StockIndicators.Validation;
internal static partial class BuiltInFormulaReferences
{
    internal static double[] GeneralFilterOutputs(IReadOnlyList<Bar> bars, int length = 100, double beta = 5.25, double gamma = 1, double zeta = 1)
    {
        var period = Math.Max(1, (int)Math.Ceiling(Math.Max(1, length) / beta)); var b = new ReferenceFraction[bars.Count]; var d = new ReferenceFraction[bars.Count];
        var result = new double[bars.Count];
        ReferenceFraction Round(ReferenceFraction value) => RoundRocBankStage(value);
        ReferenceFraction Product(ReferenceFraction value, double factor) => Round(value * ReferenceFraction.FromDouble(factor));
        ReferenceFraction Step(ReferenceFraction value) => Product(Round(value / new ReferenceFraction(period)), gamma);
        for (var i = 0; i < bars.Count; i++)
        {
            var price = ReferenceFraction.FromDouble(bars[i].Close); var a = Round(price - (i >= period ? b[i - period] : price));
            b[i] = Round((i > 0 ? b[i - 1] : price) + Step(a));
            var c = Round(b[i] - (i >= period ? d[i - period] : b[i]));
            d[i] = Round((i > 0 ? d[i - 1] : price) + Step(Round(Product(a, zeta) + Product(c, 1 - zeta)))); result[i] = d[i].ToDouble();
        }
        return result;
    }
}

using OoplesFinance.StockIndicators.Indicators;
namespace OoplesFinance.StockIndicators.Validation;
internal static partial class BuiltInFormulaReferences
{
    internal static double[] TruncatedBandPassValues(IReadOnlyList<Bar> bars, int length1, int length2, double bandwidth)
    {
        length1 = Math.Max(1, length1); length2 = Math.Max(1, length2);
        var phase = bandwidth * 2 * Math.PI / length1;
        if (double.IsInfinity(phase)) phase = (ReferenceFraction.FromDouble(bandwidth) * ReferenceFraction.FromDouble(2 * Math.PI) / new ReferenceFraction(length1)).ToDouble();
        var cosine = Math.Cos(phase);
        var decay = 1 / cosine - Math.Sqrt(1 / (cosine * cosine) - 1);
        var drive = ReferenceFraction.FromDouble(.5 * (1 - decay));
        var feedback = ReferenceFraction.FromDouble(Math.Cos(Math.Max(.01, Math.Min(.99, 2 * Math.PI / length1))) * (1 + decay));
        var tail = ReferenceFraction.FromDouble(decay);
        ReferenceFraction Round(ReferenceFraction value) => RoundRocBankStage(value);
        ReferenceFraction At(int index) => index < 0 ? new ReferenceFraction(0) : ReferenceFraction.FromDouble(bars[index].Close);
        return bars.Select((_, i) =>
        {
            var terms = new ReferenceFraction[Math.Min(length2, i + 1) + 2];
            for (var j = 0; j < terms.Length; j++) terms[j] = new ReferenceFraction(0);
            for (var lag = terms.Length - 3; lag >= 0; lag--)
                terms[lag] = Round(Round(Round(drive * Round(At(i - lag) - At(i - lag - 2))) + Round(feedback * terms[lag + 1])) - Round(tail * terms[lag + 2]));
            return terms[0].ToDouble();
        }).ToArray();
    }
}

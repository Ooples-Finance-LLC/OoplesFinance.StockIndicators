using System.Numerics;
using OoplesFinance.StockIndicators.Indicators;
namespace OoplesFinance.StockIndicators.Validation;
internal static partial class BuiltInFormulaReferences
{
    internal static double[] TrendflexValues(IReadOnlyList<Bar> bars, int length)
    {
        length = Math.Max(1, length); var angle = Math.Sqrt(2) * Math.PI / (.5 * length); var radius = Math.Exp(-angle);
        var c2 = 2 * radius * Math.Cos(angle); var c3 = -radius * radius; var c1 = 1 - c2 - c3;
        ReferenceFraction R(double v) => ReferenceFraction.FromDouble(v);
        ReferenceFraction Round(ReferenceFraction v) => RoundRocBankStage(v);
        var filtered = new ReferenceFraction[bars.Count]; var result = new double[bars.Count]; var energy = new ReferenceFraction(0);
        ReferenceFraction At(int i) => i < 0 ? new ReferenceFraction(0) : filtered[i];
        var chunk = new ReferenceFraction(BigInteger.One << 1024); var halfChunk = new ReferenceFraction(BigInteger.One << 512);
        for (var i = 0; i < bars.Count; i++)
        {
            var mean = Round((R(bars[i].Close) + (i == 0 ? new ReferenceFraction(0) : R(bars[i - 1].Close))) / new ReferenceFraction(2));
            filtered[i] = Round(R(c1) * mean + R(c2) * At(i - 1) + R(c3) * At(i - 2));
            var total = new ReferenceFraction(length) * filtered[i];
            for (var j = Math.Max(0, i - length); j < i; j++) total -= filtered[j];
            var deviation = Round(total / new ReferenceFraction(length));
            var nextEnergy = R(.04) * deviation * deviation + R(.96) * energy;
            var rootScale = new ReferenceFraction(1);
            if (nextEnergy.Sign != 0)
            {
                while (nextEnergy.CompareTo(new ReferenceFraction(1) / halfChunk) < 0) { nextEnergy *= chunk; rootScale /= halfChunk; }
                while (nextEnergy.CompareTo(halfChunk) >= 0) { nextEnergy /= chunk; rootScale *= halfChunk; }
            }
            var normalized = R(nextEnergy.ToDouble()); energy = normalized * rootScale * rootScale;
            result[i] = energy.Sign == 0 ? 0 : (deviation / (R(normalized.SqrtToDouble()) * rootScale)).ToDouble();
        }
        return result;
    }
}

using System.Numerics;
using OoplesFinance.StockIndicators.Indicators;
namespace OoplesFinance.StockIndicators.Validation;
internal static partial class BuiltInFormulaReferences
{
    internal static double[] DynamicFilterValues(IReadOnlyList<Bar> bars, int length)
    {
        length = Math.Max(1, length); var source = new ReferenceFraction[bars.Count]; var squares = new ReferenceFraction[bars.Count];
        var lines = new ReferenceFraction[bars.Count]; var gain = new ReferenceFraction(0); var scale = new ReferenceFraction(BigInteger.One << 2148);
        ReferenceFraction Round(ReferenceFraction value) => RoundRocBankStage(value);
        for (var i = 0; i < bars.Count; i++)
        {
            var price = ReferenceFraction.FromDouble(bars[i].Close); var previous = i == 0 ? price : lines[i - 1];
            source[i] = Round(price + Round(price - previous));
            lines[i] = Round(previous + Round(gain * Round(source[i] - previous)));
            var first = Math.Max(0, i - length + 1); var count = new ReferenceFraction(i - first + 1);
            var mean = Round(source.Skip(first).Take(i - first + 1).Aggregate(new ReferenceFraction(0), (sum, value) => sum + value) / count);
            var residual = Round(source[i] - mean); squares[i] = Round(residual * residual * scale);
            var variance = Round(squares.Skip(first).Take(i - first + 1).Aggregate(new ReferenceFraction(0), (sum, value) => sum + value) / count) / scale;
            ReferenceFraction root;
            for (var shift = 0; ; shift += 1024)
            {
                var divisor = new ReferenceFraction(BigInteger.One << (2 * shift)); var result = (variance / divisor).SqrtToDouble();
                if (!double.IsInfinity(result)) { root = ReferenceFraction.FromDouble(result) * new ReferenceFraction(BigInteger.One << shift); break; }
            }
            var difference = Round(source[i] - lines[i]); if (difference.Sign < 0) difference = new ReferenceFraction(0) - difference;
            gain = difference.Sign == 0 ? new ReferenceFraction(0) : Round(difference / Round(difference + Round(root * new ReferenceFraction(length))));
        }
        return lines.Select(v => v.ToDouble()).ToArray();
    }
}

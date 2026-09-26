using System.Numerics;
using OoplesFinance.StockIndicators.Indicators;
namespace OoplesFinance.StockIndicators.Validation;
internal static partial class BuiltInFormulaReferences
{
    internal static IReadOnlyDictionary<string, double[]> QuadraticProjectionOutputs(IReadOnlyList<Bar> bars, IBuiltInIndicator indicator)
    {
        var options = indicator.CreateOptions(); return QuadraticProjectionOutputs(bars, Math.Max(1, Integer(options, "Length", 500)), AverageKind(options, 1));
    }
    internal static IReadOnlyDictionary<string, double[]> QuadraticProjectionOutputs(IReadOnlyList<Bar> bars, int length, int kind)
    {
        var zero = new ReferenceFraction(0); var prices = bars.Select(b => ReferenceFraction.FromDouble(b.Close)).ToArray();
        var indices = Enumerable.Range(0, bars.Count).Select(i => new ReferenceFraction(i)).ToArray();
        var xMean = SmoothRocBankStage(indices, length, kind); var qMean = SmoothRocBankStage(indices.Select(x => x * x).ToArray(), length, kind);
        var yMean = SmoothRocBankStage(prices, length, kind); var result = new double[bars.Count];
        // Prefix sums are independent of the production sliding-state update. Geometry
        // uses Gram-Schmidt in local coordinates, with zero-index prehistory multiplicity.
        var prefix0 = new ReferenceFraction[bars.Count + 1]; var prefix1 = new ReferenceFraction[bars.Count + 1]; var prefix2 = new ReferenceFraction[bars.Count + 1];
        prefix0[0] = prefix1[0] = prefix2[0] = zero;
        for (var j = 0; j < bars.Count; j++)
        {
            prefix0[j + 1] = prefix0[j] + prices[j];
            prefix1[j + 1] = prefix1[j] + indices[j] * prices[j];
            prefix2[j + 1] = prefix2[j] + indices[j] * indices[j] * prices[j];
        }
        BigInteger sx = 0, sq = 0, sc = 0, sf = 0; var n = new BigInteger(length);
        for (var i = 0; i < bars.Count; i++)
        {
            if (i < length) { var x = new BigInteger(i); sx += x; sq += x * x; sc += x * x * x; sf += x * x * x * x; }
            result[i] = yMean[i].ToDouble(); if (length < 3 || i < 2) continue;
            var start = Math.Max(0, i - length + 1); var origin = new ReferenceFraction(start);
            var sy = prefix0[i + 1] - prefix0[start];
            var xy = prefix1[i + 1] - prefix1[start] - origin * sy;
            var qy = prefix2[i + 1] - prefix2[start] - new ReferenceFraction(2) * origin * (prefix1[i + 1] - prefix1[start]) + origin * origin * sy;
            // u = n*x-sx; z = n*x^2-sq; v = norm(u)*z-dot(u,z)*u.
            var norm = n * (n * sq - sx * sx); var projection = n * (n * sc - sx * sq);
            var znorm = n * (n * sf - sq * sq); var vnorm = norm * (norm * znorm - projection * projection);
            var uy = new ReferenceFraction(n) * xy - new ReferenceFraction(sx) * sy;
            var vy = new ReferenceFraction(norm) * (new ReferenceFraction(n) * qy - new ReferenceFraction(sq) * sy) - new ReferenceFraction(projection) * uy;
            var curvature = new ReferenceFraction(n * norm) * vy / new ReferenceFraction(vnorm);
            var slope = new ReferenceFraction(n) * uy / new ReferenceFraction(norm) - new ReferenceFraction(projection) * curvature / new ReferenceFraction(norm);
            var distance = indices[i] - xMean[i];
            result[i] = (yMean[i] + slope * distance + curvature * (indices[i] * indices[i] - qMean[i] - new ReferenceFraction(2) * origin * distance)).ToDouble();
        }
        return Outputs(("QuadReg", result));
    }
}

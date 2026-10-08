using System.Numerics;
using OoplesFinance.StockIndicators.Indicators;
namespace OoplesFinance.StockIndicators.Validation;
internal static partial class BuiltInFormulaReferences
{
    internal static IReadOnlyDictionary<string, double[]> RmseBandOutputs(IReadOnlyList<Bar> bars, IBuiltInIndicator indicator)
    { var options = indicator.CreateOptions(); return RmseBandOutputs(bars, Math.Max(1, Integer(options, "Length", 14)), AverageKind(options, 1), Number(options, 1, "StdDevFactor")); }
    internal static IReadOnlyDictionary<string, double[]> RmseBandOutputs(IReadOnlyList<Bar> bars, int length, int kind, double factor, double[]? externalMean = null, double[]? externalVariance = null)
    {
        length = Math.Max(1, length); var prices = bars.Select(b => ReferenceFraction.FromDouble(b.Close)).ToArray(); var unit = new ReferenceFraction(BigInteger.One << 2148);
        var middle = externalMean is null ? SmoothRocBankStage(prices, length, kind) : externalMean.Select(ReferenceFraction.FromDouble).ToArray();
        var squares = prices.Select((v, i) => { var residual = v - middle[i]; return residual * residual; }).ToArray();
        var scaled = squares.Select(v => (v * unit).RoundExtendedBinary64()).ToArray();
        var variance = externalVariance is null ? SmoothRocBankStage(scaled, length, kind).Select(v => v / unit).ToArray() : externalVariance.Select(ReferenceFraction.FromDouble).ToArray();
        var deviation = variance.Select(v =>
        {
            if (v.Sign <= 0) return new ReferenceFraction(0);
            var root = v.SqrtToDouble(); if (!double.IsInfinity(root)) return ReferenceFraction.FromDouble(root);
            var normalized = v / new ReferenceFraction(BigInteger.One << 2048); return ReferenceFraction.FromDouble(normalized.SqrtToDouble()) * new ReferenceFraction(BigInteger.One << 1024);
        }).ToArray();
        var mult = ReferenceFraction.FromDouble(factor);
        return Outputs(("UpperBand", middle.Select((v, i) => (v + mult * deviation[i]).ToDouble()).ToArray()), ("MiddleBand", middle.Select(v => v.ToDouble()).ToArray()), ("LowerBand", middle.Select((v, i) => (v - mult * deviation[i]).ToDouble()).ToArray()), ("RawSquare", squares.Select(v => v.ToDouble()).ToArray()));
    }
}

using OoplesFinance.StockIndicators.Indicators;
namespace OoplesFinance.StockIndicators.Validation;
internal static partial class BuiltInFormulaReferences
{
    internal static double[] DeviationSuperValues(IReadOnlyList<Bar> bars, int length, int rmsLength, MovingAvgType kind)
    {
        ReferenceFraction R(double value) => ReferenceFraction.FromDouble(value);
        ReferenceFraction Round(ReferenceFraction value) => DeviationReferenceRound(value);
        length = Math.Max(1, length); rmsLength = Math.Max(1, rmsLength);
        var one = new ReferenceFraction(1); var two = new ReferenceFraction(2); var zero = new ReferenceFraction(0);
        ReferenceFraction Root(ReferenceFraction variance) => DeviationReferenceRoot(variance);
        var smoothing = (int)Math.Ceiling(length / 1.4m);
        var weights = Enumerable.Range(0, smoothing).Select(lag => R(kind == MovingAvgType.EhlersHannMovingAverage ? 1 - Math.Cos(2 * Math.PI * ((lag + 1d) / (smoothing + 1d))) : smoothing - lag)).ToArray();
        var mass = weights.Aggregate(zero, (sum, weight) => sum + weight);
        var momentum = new ReferenceFraction[bars.Count]; var filtered = new ReferenceFraction[bars.Count]; var squared = new ReferenceFraction[bars.Count]; var output = new ReferenceFraction[bars.Count]; var result = new double[bars.Count];
        for (var i = 0; i < bars.Count; i++)
        {
            momentum[i] = Round(R(bars[i].Close) - (i < length ? zero : R(bars[i - length].Close)));
            var drive = zero; for (var lag = 0; lag < smoothing && lag <= i; lag++) drive += weights[lag] * momentum[i - lag];
            filtered[i] = Round(drive / mass); squared[i] = filtered[i] * filtered[i];
            var count = Math.Min(rmsLength, i + 1); var squares = zero; for (var j = i - count + 1; j <= i; j++) squares += squared[j];
            var rms = Root(squares / new ReferenceFraction(count));
            var magnitude = rms.Sign == 0 || filtered[i].Sign == 0 ? 1 : (filtered[i].Abs() / rms).ToDouble();
            if (magnitude == 0) magnitude = 1;
            var angle = Math.Sqrt(2) * Math.PI * magnitude / length; var radius = R(Math.Exp(-angle)); var cosine = R(Math.Cos(angle));
            var c2 = two * radius * cosine; var c3 = zero - radius * radius; var c1 = one - c2 - c3;
            output[i] = Round(c1 * (R(bars[i].Close) + (i == 0 ? zero : R(bars[i - 1].Close))) / two + c2 * (i == 0 ? zero : output[i - 1]) + c3 * (i < 2 ? zero : output[i - 2]));
            result[i] = output[i].ToDouble();
        }
        return result;
    }
}

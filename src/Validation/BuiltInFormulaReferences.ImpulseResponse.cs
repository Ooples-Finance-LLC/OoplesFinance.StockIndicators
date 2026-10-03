using OoplesFinance.StockIndicators.Indicators;
namespace OoplesFinance.StockIndicators.Validation;
internal static partial class BuiltInFormulaReferences
{
    internal static double[] ImpulseResponseValues(IReadOnlyList<Bar> bars, int length, double bandwidth, int kind)
    {
        length = Math.Max(1, length); var period = (int)Math.Max(2, Math.Min(530, Math.Ceiling(length / 1.4)));
        var cosine = Math.Cos(Math.Max(.01, Math.Min(.99, bandwidth * 2 * Math.PI / length))); var decay = 1 / cosine - Math.Sqrt(1 / (cosine * cosine) - 1);
        var drive = ReferenceFraction.FromDouble(.5 * (1 - decay)); var feedback = ReferenceFraction.FromDouble(Math.Cos(Math.Max(.01, Math.Min(.99, 2 * Math.PI / length))) * (1 + decay)); var tail = ReferenceFraction.FromDouble(decay);
        var band = new ReferenceFraction[bars.Count]; ReferenceFraction R(ReferenceFraction v) => RoundRocBankStage(v);
        for (var i = 0; i < bars.Count; i++)
            band[i] = i < 3 ? new(0) : R(R(R(drive * R(ReferenceFraction.FromDouble(bars[i].Close) - ReferenceFraction.FromDouble(bars[i-2].Close))) + R(feedback * band[i-1])) - R(tail * band[i-2]));
        if (kind != 7) return SmoothRocBankStage(band, period, kind).Select(v => v.ToDouble()).ToArray();
        var weights = Enumerable.Range(1, period).Select(j => ReferenceFraction.FromDouble(1-Math.Cos(2*Math.PI*(j/(period+1d))))).ToArray();
        var mass = weights.Aggregate(new ReferenceFraction(0), (sum,v) => sum+v); var result = new double[bars.Count];
        for (var i = 0; i < bars.Count; i++) { var sum = new ReferenceFraction(0); for (var lag = 0; lag < Math.Min(period,i+1); lag++) sum += band[i-lag]*weights[lag]; result[i]=(sum/mass).ToDouble(); }
        return result;
    }
}

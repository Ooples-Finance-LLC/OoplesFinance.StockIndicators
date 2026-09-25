using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.Validation;

internal static partial class BuiltInFormulaReferences
{
    internal static IReadOnlyDictionary<string, double[]> QuasiWhiteNoiseOutputs(IReadOnlyList<Bar> bars, IBuiltInIndicator indicator)
    {
        var options = indicator.CreateOptions(); var kind = AverageKind(options, 6);
        var length = Integer(options, "NoiseLength", 500);
        var connors = ConnorsTrajectories(Closes(bars), length, length, Integer(options, "Length", 20), kind)["ConnorsRsi"];
        // Preserve the published subtraction, reciprocal, then product rounding boundaries.
        var reciprocal = ReferenceFraction.FromDouble((new ReferenceFraction(1) / ReferenceFraction.FromDouble(Number(options, 40, "Divisor"))).ToDouble());
        var noise = connors.Select(v => (ReferenceFraction.FromDouble((ReferenceFraction.FromDouble(v) - new ReferenceFraction(50)).ToDouble()) * reciprocal).ToDouble()).ToArray();
        var average = SmoothStrengthStage(noise.Select(ReferenceFraction.FromDouble).ToArray(), length, kind).Select(v => v.ToDouble()).ToArray();
        var deviation = new double[noise.Length]; var sum = new ReferenceFraction(0); var squares = new ReferenceFraction(0);
        for (var i = 0; i < noise.Length; i++)
        {
            var current = ReferenceFraction.FromDouble(noise[i]); sum += current; squares += current * current;
            if (i >= length) { var old = ReferenceFraction.FromDouble(noise[i - length]); sum -= old; squares -= old * old; }
            if (i >= length - 1)
            {
                var n = new ReferenceFraction(length);
                deviation[i] = ((n * squares - sum * sum) / (n * n)).SqrtToDouble();
            }
        }
        var variance = deviation.Select(v => (ReferenceFraction.FromDouble(v) * ReferenceFraction.FromDouble(v)).ToDouble()).ToArray();
        return Outputs(("WhiteNoise", noise), ("WhiteNoiseMa", average), ("WhiteNoiseStdDev", deviation), ("WhiteNoiseVariance", variance));
    }
}

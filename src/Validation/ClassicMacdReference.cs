using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.Validation;

internal static class ClassicMacdReference
{
    internal static double?[][] Values(IReadOnlyList<Bar> bars, ClassicMacd owner)
    {
        var result = Enumerable.Range(0, 3).Select(_ => new double?[bars.Count]).ToArray();
        if (owner.OscillatorFirst >= bars.Count)
            return result;
        var fast = ClassicAverageReference.AlignedExtendedValues(
            bars,
            owner.FastAverage,
            owner.OscillatorFirst
        );
        var slow = ClassicAverageReference.AlignedExtendedValues(
            bars,
            owner.SlowAverage,
            owner.OscillatorFirst
        );
        var start = (int)owner.OscillatorFirst;
        var differences = Enumerable
            .Range(start, bars.Count - start)
            .Select(i => (fast[i]!.Value - slow[i]!.Value).RoundExtendedBinary64())
            .ToArray();
        var signal = ClassicAverageReference.ExtendedValues(differences, owner.SignalAverage);
        for (var j = 0; j < signal.Length; j++)
        {
            if (!signal[j].HasValue)
                continue;
            result[0][start + j] = differences[j].ToDouble();
            result[1][start + j] = signal[j]!.Value.ToDouble();
            result[2][start + j] = (differences[j] - signal[j]!.Value).ToDouble();
        }
        return result;
    }
}

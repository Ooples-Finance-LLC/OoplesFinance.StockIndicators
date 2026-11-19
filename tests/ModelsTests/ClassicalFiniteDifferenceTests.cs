using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Validation;

namespace OoplesFinance.StockIndicators.Tests.Unit.ModelsTests;

public sealed class ClassicalFiniteDifferenceTests
{
    public static IEnumerable<object[]> Cases =>
        from method in new[] { ClassicAverageMethod.Sma, ClassicAverageMethod.Wma,
            ClassicAverageMethod.Trima, ClassicAverageMethod.Ema,
            ClassicAverageMethod.Dema, ClassicAverageMethod.Tema }
        from dataset in new[] { 0, 1, 2 }
        select new object[] { method, dataset };

    [Theory, MemberData(nameof(Cases))]
    public void RoundedDifferencesPreserveReferenceBitsPresenceAndReset(ClassicAverageMethod method, int dataset)
    {
        double[] edge = dataset switch
        {
            0 => [0, -0d, double.Epsilon, -double.Epsilon, 3 * double.Epsilon,
                -3 * double.Epsilon, Math.ScaleB(1, -1022), -Math.ScaleB(1, -1022)],
            1 => [double.MaxValue, -double.MaxValue, double.MaxValue / 2,
                -double.MaxValue / 2, 0, double.Epsilon, 1, -1],
            _ => [1, Math.BitIncrement(1), Math.BitDecrement(1), -1,
                Math.ScaleB(1, 52), Math.ScaleB(1, 52) + 1, -Math.ScaleB(1, 52), 0],
        };
        var bars = Enumerable.Range(0, 64)
            .Select(i => new Bar(default, edge[i % edge.Length], edge[i % edge.Length],
                edge[i % edge.Length], edge[i % edge.Length], 1)).ToArray();

        foreach (bool percentage in new[] { false, true })
        {
            var owner = new ClassicPriceOscillator(2, 5, method, percentage);
            var fast = ClassicAverageReference.ExtendedValues(bars, owner.Fast);
            var slow = ClassicAverageReference.ExtendedValues(bars, owner.Slow);
            var state = (IMultiOutputState)owner.CreateState();
            using var lifetime = state as IDisposable;
            var output = new double[2];
            for (int replay = 0; replay < 2; replay++)
            {
                state.Reset();
                for (int i = 0; i < bars.Length; i++)
                {
                    double? expected = null;
                    if (fast[i].HasValue && slow[i].HasValue)
                    {
                        var difference = fast[i]!.Value - slow[i]!.Value;
                        expected = !percentage ? difference.ToDouble()
                            : slow[i]!.Value.Sign == 0 ? 0
                            : (difference * new ReferenceFraction(100) / slow[i]!.Value).ToDouble();
                    }
                    state.Update(in bars[i], output);
                    Equal(expected, output[0], output[1]);
                }
            }
        }

        var macd = new ClassicMacd(2, 5, 3, method, method, method);
        var reference = ClassicMacdReference.Values(bars, macd);
        var macdState = (IMultiOutputState)macd.CreateState();
        using var macdLifetime = macdState as IDisposable;
        var values = new double[6];
        for (int replay = 0; replay < 2; replay++)
        {
            macdState.Reset();
            for (int i = 0; i < bars.Length; i++)
            {
                macdState.Update(in bars[i], values);
                for (int slot = 0; slot < 3; slot++)
                    Equal(reference[slot][i], values[slot], values[slot + 3]);
            }
        }
    }

    private static void Equal(double? expected, double value, double present)
    {
        Assert.Equal(expected.HasValue ? 1d : 0d, present);
        Assert.Equal(BitConverter.DoubleToInt64Bits(expected ?? 0), BitConverter.DoubleToInt64Bits(value));
    }
}

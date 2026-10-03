using System.Numerics;
using OoplesFinance.StockIndicators.Indicators;
namespace OoplesFinance.StockIndicators.Validation;
internal static partial class BuiltInFormulaReferences
{
    internal static readonly IndicatorErrorBudget VolatilityWaveBudget = new(0, 5e-16, requireSameSign: true);
    internal static IReadOnlyDictionary<string, double[]> VolatilityWaveOutputs(IReadOnlyList<Bar> bars, IBuiltInIndicator indicator)
    {
        var options = indicator.CreateOptions();
        return VolatilityWaveValues(bars, (MovingAvgType)options.GetType().GetProperty("MaType")!.GetValue(options)!, Integer(options, "Length", 20));
    }
    internal static Dictionary<string, double[]> VolatilityWaveValues(IReadOnlyList<Bar> bars,
        MovingAvgType kind = MovingAvgType.WeightedMovingAverage, int length = 20, double factor = 2.5)
    {
        ReferenceFraction R(long n) => new(n);
        var k = ReferenceFraction.FromDouble(factor); var prices = bars.Select(b => ReferenceFraction.FromDouble(b.Close)).ToArray();
        length = Math.Max(1, length); var smooth = Math.Max(2, Math.Min(530, (int)Math.Ceiling(Math.Sqrt(length)))); var code = AverageKind(new { MaType = kind }, 2);
        var powers = new ReferenceFraction[prices.Length];
        for (var i = 0; i < prices.Length; i++)
        {
            var start = Math.Max(0, i - length + 1); var count = i - start + 1; var pairs = R(0);
            for (var a = start; a <= i; a++) for (var b = a + 1; b <= i; b++) { var difference = prices[a] - prices[b]; pairs += difference * difference; }
            var q = prices[i].Sign > 0 && k.Sign > 0 ? R(10000) * k * k * k * k * pairs / (R(count) * R(count) * prices[i] * prices[i]) : R(0);
            powers[i] = q.CompareTo(R(1)) <= 0 ? R(1) : q.CompareTo(R(256)) >= 0 ? R(256) : q;
        }
        WaveInterval[] Mean(WaveInterval[] input)
        {
            var output = new WaveInterval[input.Length]; var previous = WaveInterval.Zero;
            if (code is not (1 or 2 or 3 or 6)) throw new ArgumentOutOfRangeException(nameof(kind));
            for (var i = 0; i < input.Length; i++)
            {
                var sum = WaveInterval.Zero;
                if (code is 1 or 2)
                {
                    if (code == 2 || i + 1 >= smooth)
                        for (var j = Math.Max(0, i - smooth + 1); j <= i; j++) sum = sum.Add(input[j].Scale(R(code == 2 ? smooth - i + j : 1)));
                    output[i] = sum.Scale(R(1) / (code == 2 ? R(smooth) * R(smooth + 1) / R(2) : R(smooth)));
                }
                else if (code == 3 && i < smooth)
                { for (var j = 0; j <= i; j++) sum = sum.Add(input[j]); output[i] = sum.Scale(R(1) / R(i + 1)); }
                else
                {
                    var alpha = R(code == 3 ? 2 : 1) / R(code == 3 ? smooth + 1 : smooth);
                    output[i] = previous.Scale(R(1) - alpha).Add(input[i].Scale(alpha));
                }
                previous = output[i];
            }
            return output;
        }
        var results = new double[prices.Length];
        for (var bits = 128; ; bits = checked(bits * 2))
        {
            var weighted = new WaveInterval[prices.Length];
            for (var i = 0; i < prices.Length; i++)
            {
                var square = UltimateReferenceArithmetic.Root(powers[i], bits + 32);
                var pLow = UltimateReferenceArithmetic.Root(square.Low, bits + 32).Low;
                var pHigh = UltimateReferenceArithmetic.Root(square.High, bits + 32).High;
                var totalLow = UltimateReferenceArithmetic.PublicWeightSum(length, pHigh, bits).Low;
                if (totalLow.CompareTo(R(1)) < 0) totalLow = R(1);
                var totalHigh = UltimateReferenceArithmetic.PublicWeightSum(length, pLow, bits).High;
                var numerator = WaveInterval.Zero;
                for (var j = Math.Max(0, i - length + 1); j <= i; j++)
                {
                    if (prices[j].Sign == 0) continue;
                    var low = UltimateReferenceArithmetic.Weight(length - i + j, length, pHigh, bits + 32).Low;
                    var high = UltimateReferenceArithmetic.Weight(length - i + j, length, pLow, bits + 32).High;
                    numerator = numerator.Add(new WaveInterval(low, high).Scale(prices[j]));
                }
                weighted[i] = new(numerator.Low / (numerator.Low.Sign >= 0 ? totalHigh : totalLow), numerator.High / (numerator.High.Sign >= 0 ? totalLow : totalHigh));
            }
            var first = Mean(weighted); var second = Mean(first); var ready = true;
            for (var i = 0; i < prices.Length; i++)
            {
                var line = first[i].Scale(R(2)).Add(second[i].Scale(R(-1)));
                var a = line.Low.ToDouble(); var b = line.High.ToDouble();
                if (a == b) { results[i] = a; continue; }
                var center = (line.Low + line.High) / R(2);
                if (line.Low.Sign == line.High.Sign && (line.High - line.Low).CompareTo(center.Abs() / new ReferenceFraction(BigInteger.One << 68)) <= 0)
                { results[i] = center.ToDouble(); continue; }
                ready = false;
            }
            if (ready) return new() { ["Vwma"] = results };
        }
    }
    private readonly struct WaveInterval
    {
        internal readonly ReferenceFraction Low, High;
        internal static WaveInterval Zero => new(new(0), new(0));
        internal WaveInterval(ReferenceFraction low, ReferenceFraction high) { Low = low; High = high; }
        internal WaveInterval Add(WaveInterval other) => new(Low + other.Low, High + other.High);
        internal WaveInterval Scale(ReferenceFraction value) => value.Sign >= 0 ? new(Low * value, High * value) : new(High * value, Low * value);
    }
}

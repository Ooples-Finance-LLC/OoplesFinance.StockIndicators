using System.Numerics;
using OoplesFinance.StockIndicators.Indicators;
namespace OoplesFinance.StockIndicators.Validation;
internal static partial class BuiltInFormulaReferences
{
    internal static IReadOnlyDictionary<string, double[]> VolatilityAverageOutputs(IReadOnlyList<Bar> bars, IBuiltInIndicator indicator)
    {
        var options = indicator.CreateOptions();
        return VolatilityAverageValues(bars, (MovingAvgType)options.GetType().GetProperty("MaType")!.GetValue(options)!,
            Integer(options, "Length", 20), Integer(options, "LbLength", 10), Integer(options, "SmoothLength", 3)).Outputs;
    }
    internal static (Dictionary<string, double[]> Outputs, Signal[] Signals, int[] Periods) VolatilityAverageValues(IReadOnlyList<Bar> bars,
        MovingAvgType kind = MovingAvgType.SimpleMovingAverage, int length = 20, int lookback = 10, int smooth = 3)
    {
        ReferenceFraction R(long value) => new(value);
        var code = AverageKind(new { MaType = kind }, 1); length = Math.Max(1, length); lookback = Math.Max(1, lookback); smooth = Math.Max(1, smooth);
        VolatilityScoreOracle[] Mean(VolatilityScoreOracle[] values, int period)
        {
            var output = new VolatilityScoreOracle[values.Length];
            if (code is 3 or 6)
            {
                var seed = VolatilityScoreOracle.Zero; var previous = VolatilityScoreOracle.Zero;
                var alpha = R(code == 3 ? 2 : 1) / R(code == 3 ? period + 1L : period);
                for (var i = 0; i < values.Length; i++)
                {
                    if (code == 3 && i < period) { seed = seed.Plus(values[i]); previous = seed.Scale(R(1) / R(i + 1L)); }
                    else previous = previous.Plus(values[i].Plus(previous.Scale(R(-1))).Scale(alpha));
                    output[i] = previous;
                }
                return output;
            }
            if (code is not (1 or 2)) throw new ArgumentOutOfRangeException(nameof(kind));
            for (var i = 0; i < values.Length; i++)
            {
                var sum = VolatilityScoreOracle.Zero;
                if (code != 1 || i + 1 >= period)
                    for (var j = Math.Max(0, i - period + 1); j <= i; j++) sum = sum.Plus(values[j].Scale(R(code == 2 ? period - (long)i + j : 1)));
                output[i] = sum.Scale(R(1) / (code == 2 ? R(period) * R(period + 1L) / R(2) : R(period)));
            }
            return output;
        }
        var prices = bars.Select(b => ReferenceFraction.FromDouble(b.Close)).ToArray();
        var means = Mean(prices.Select(VolatilityScoreOracle.Of).ToArray(), lookback); var scores = new VolatilityScoreOracle[prices.Length];
        for (var i = 0; i < prices.Length; i++)
        {
            var start = Math.Max(0, i - lookback + 1); var count = i - start + 1; var pairs = R(0);
            // Pairwise distances give population variance without production's rolling moments.
            for (var a = start; a <= i; a++) for (var b = a + 1; b <= i; b++) { var delta = prices[a] - prices[b]; pairs += delta * delta; }
            var variance = pairs / (R(count) * R(count));
            scores[i] = VolatilityScoreOracle.DivideRoot(R(100) * (prices[i] - means[i].Rational()), variance);
        }
        var filtered = Mean(scores, smooth); var periods = new int[prices.Length]; var weighted = new VolatilityScoreOracle[prices.Length];
        for (var i = 0; i < prices.Length; i++)
        {
            var score = filtered[i]; if (score.Compare(R(0)) < 0) score = score.Scale(R(-1));
            if (score.Compare(R(100)) >= 0) score = VolatilityScoreOracle.Of(R(100));
            var bucket = 0;
            for (var j = 1; j <= 100; j++)
            {
                var cmp = score.Compare(R(lookback) * R(2L * j - 1) / R(2));
                if (cmp < 0 || cmp == 0 && j % 2 == 1) break;
                bucket = j;
            }
            var numerator = Math.Max(10, (long)length * (10 - bucket));
            var lower = numerator / 10; var remainder = numerator % 10;
            periods[i] = (int)(lower + (remainder > 5 || remainder == 5 && lower % 2 == 1 ? 1 : 0));
            var sum = R(0);
            for (var lag = 0; lag < Math.Min(periods[i], i + 1); lag++) sum += R(periods[i] - lag) * prices[i - lag];
            weighted[i] = VolatilityScoreOracle.Of(R(2) * sum / (R(periods[i]) * R(periods[i] + 1L)));
        }
        var lines = Mean(weighted, smooth); var outputValues = new double[prices.Length]; var signals = new Signal[prices.Length]; var previousMargin = R(0);
        for (var i = 0; i < prices.Length; i++)
        {
            var line = lines[i].Rational(); var margin = prices[i] - line;
            signals[i] = margin.Sign > 0 && margin.CompareTo(previousMargin) > 0 ? Signal.StrongBuy
                : margin.Sign < 0 && margin.CompareTo(previousMargin) < 0 ? Signal.StrongSell
                : margin.Sign > 0 ? Signal.Buy : margin.Sign < 0 ? Signal.Sell : Signal.None;
            outputValues[i] = line.ToDouble(); previousMargin = margin;
        }
        return (new Dictionary<string, double[]> { ["Vma"] = outputValues }, signals, periods);
    }
}

// Independent validation expression: rational coefficients multiplying square roots
// of rational variances (production instead uses integer radical kernels).
internal sealed class VolatilityScoreOracle
{
    private readonly List<(ReferenceFraction Coefficient, ReferenceFraction Root)> _terms;
    internal static readonly VolatilityScoreOracle Zero = new(new());
    private VolatilityScoreOracle(List<(ReferenceFraction, ReferenceFraction)> terms) => _terms = terms;
    internal static VolatilityScoreOracle Of(ReferenceFraction value) => value.Sign == 0 ? Zero : new(new() { (value, new(1)) });
    internal static VolatilityScoreOracle DivideRoot(ReferenceFraction value, ReferenceFraction variance)
    {
        if (value.Sign == 0 || variance.Sign == 0) return Zero;
        var (numerator, denominator) = variance.Components; var a = Root(numerator); var b = Root(denominator);
        if (a * a == numerator && b * b == denominator) return Of(value * new ReferenceFraction(b) / new ReferenceFraction(a));
        return new(new() { (value / variance, variance) });
    }
    internal ReferenceFraction Rational()
    {
        if (_terms.Count == 0) return new(0);
        if (_terms.Count == 1 && _terms[0].Root.CompareTo(new(1)) == 0) return _terms[0].Coefficient;
        throw new InvalidOperationException("Reference price average must be rational.");
    }
    internal VolatilityScoreOracle Scale(ReferenceFraction factor)
        => factor.Sign == 0 ? Zero : new(_terms.Select(t => (t.Coefficient * factor, t.Root)).ToList());
    internal VolatilityScoreOracle Plus(VolatilityScoreOracle other)
    {
        var result = _terms.ToList();
        foreach (var term in other._terms)
        {
            var matched = false;
            for (var i = 0; i < result.Count; i++)
            {
                var ratio = term.Root / result[i].Root; var (numerator, denominator) = ratio.Components;
                var a = Root(numerator); var b = Root(denominator);
                if (a * a != numerator || b * b != denominator) continue;
                var coefficient = result[i].Coefficient + term.Coefficient * new ReferenceFraction(a) / new ReferenceFraction(b);
                if (coefficient.Sign == 0) result.RemoveAt(i); else result[i] = (coefficient, result[i].Root);
                matched = true; break;
            }
            if (!matched) result.Add(term);
        }
        return new(result);
    }
    private static BigInteger Root(BigInteger value)
    {
        if (value.IsZero) return BigInteger.Zero;
        var estimate = BigInteger.One << ((value.ToByteArray().Length * 8 + 1) / 2);
        while (true) { var next = (estimate + value / estimate) / 2; if (next >= estimate) return estimate; estimate = next; }
    }
    internal int Compare(ReferenceFraction threshold)
    {
        var difference = Plus(Of(new ReferenceFraction(-1) * threshold));
        if (difference._terms.Count == 0) return 0;
        if (difference._terms.Count == 1) return difference._terms[0].Coefficient.Sign;
        for (var precision = 80; ; precision = checked(precision * 3))
        {
            BigInteger lower = 0, upper = 0;
            foreach (var term in difference._terms)
            {
                var (a, b) = term.Coefficient.Components; var (c, d) = term.Root.Components;
                var numerator = (a * a * c) << checked(2 * precision); var denominator = b * b * d;
                var floor = Root(numerator / denominator); var ceiling = floor * floor * denominator == numerator ? floor : floor + 1;
                if (a.Sign > 0) { lower += floor; upper += ceiling; } else { lower -= ceiling; upper -= floor; }
            }
            if (lower.Sign > 0) return 1; if (upper.Sign < 0) return -1;
        }
    }
}

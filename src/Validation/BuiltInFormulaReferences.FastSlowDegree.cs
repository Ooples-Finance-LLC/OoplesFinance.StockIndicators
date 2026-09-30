using System.Numerics;
using OoplesFinance.StockIndicators.Indicators;
namespace OoplesFinance.StockIndicators.Validation;
internal static partial class BuiltInFormulaReferences
{
    internal static IReadOnlyDictionary<string, double[]> FastSlowDegreeOutputs(IReadOnlyList<Bar> bars, IBuiltInIndicator indicator)
    { var options = indicator.CreateOptions(); return FastSlowDegreeValues(Closes(bars), Integer(options, "Length", 100), Integer(options, "FastLength", 3), Integer(options, "SlowLength", 2), Integer(options, "SignalLength", 14), AverageKind(options, 3)).Outputs; }
    private static double DegreeSine(BigInteger numerator, int length)
    {
        var period = new BigInteger(length) * 2; var phase = (numerator % period + period) % period; var sign = 1;
        if (phase >= length) { phase -= length; sign = -1; }
        phase = BigInteger.Min(phase, length - phase);
        if (phase.IsZero) return 0;
        if (phase * 2 == length) return sign;
        var angle = (new ReferenceFraction(phase) * ReferenceFraction.FromDouble(Math.PI) / new ReferenceFraction(length)).ToDouble();
        return sign * Math.Sin(angle);
    }
    internal static (Dictionary<string, double[]> Outputs, Signal[] Signals) FastSlowDegreeValues(double[] prices, int length, int fast, int slow, int signalLength, int kind, double[]? externalSignal = null)
    {
        length = Math.Max(1, length); fast = Math.Max(1, fast); slow = Math.Max(1, slow); signalLength = Math.Max(1, signalLength);
        ReferenceFraction R(double value) => ReferenceFraction.FromDouble(value);
        var coefficients = Enumerable.Range(0, prices.Length).Select(i => { var n = new BigInteger(i) + 1; return ((R(DegreeSine(n * n, length)) - R(DegreeSine(n * (n - 1), length))) / new ReferenceFraction(n)).RoundExtendedBinary64(); }).ToArray();
        var products = Enumerable.Range(0, prices.Length).Select(i =>
        {
            var n = new BigInteger(i) + 1; var polynomial = new ReferenceFraction(n * n - (n - 1) * (n - 1)) / new ReferenceFraction((BigInteger)length * length);
            var fastWeight = polynomial + Window(coefficients, i, fast).Aggregate(R(0), (a, b) => a + b);
            var slowWeight = polynomial + Window(coefficients, i, slow).Aggregate(R(0), (a, b) => a + b);
            var price = R(i == 0 ? 0 : prices[i - 1]); return (price * fastWeight - price * slowWeight).RoundExtendedBinary64();
        }).ToArray();
        var line = Enumerable.Range(0, prices.Length).Select(i => Window(products, i, length).Aggregate(R(0), (a, b) => a + b).RoundExtendedBinary64()).ToArray();
        var mean = externalSignal is null ? SmoothRocBankStage(line, signalLength, kind) : externalSignal.Select(R).ToArray(); var spread = line.Select((v, i) => v - mean[i]).ToArray();
        var signals = new Signal[prices.Length]; var previous = R(0);
        for (var i = 0; i < prices.Length; i++) { var v = spread[i]; signals[i] = v.Sign > 0 && v.CompareTo(previous) > 0 ? Signal.StrongBuy : v.Sign < 0 && v.CompareTo(previous) < 0 ? Signal.StrongSell : v.Sign > 0 ? Signal.Buy : v.Sign < 0 ? Signal.Sell : Signal.None; previous = v; }
        return (new Dictionary<string, double[]> { ["Fsdo"] = line.Select(v => v.ToDouble()).ToArray(), ["Signal"] = mean.Select(v => v.ToDouble()).ToArray(), ["Histogram"] = spread.Select(v => v.ToDouble()).ToArray() }, signals);
    }
}

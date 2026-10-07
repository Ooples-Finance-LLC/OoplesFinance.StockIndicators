using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.Validation;

internal static partial class BuiltInFormulaReferences
{
    internal static IReadOnlyDictionary<string,double[]> StationaryLevelsOscillatorOutputs(IReadOnlyList<Bar> bars, IBuiltInIndicator indicator)
    {
        var options = indicator.CreateOptions();
        return StationaryLevelsOscillatorOutputs(bars, Math.Max(1, Integer(options, "Length", 200)), AverageKind(options, 1));
    }
    internal static IReadOnlyDictionary<string,double[]> StationaryLevelsOscillatorOutputs(IReadOnlyList<Bar> bars, int length, int kind,
        ICollection<Signal>? signals = null, IReadOnlyList<double>? suppliedMeans = null)
    {
        ReferenceFraction R(double value) => ReferenceFraction.FromDouble(value);
        ReferenceFraction Round(ReferenceFraction value) => value.RoundExtendedBinary64();
        var prices = bars.Select(b => R(b.Close)).ToArray();
        var means = suppliedMeans is null ? SmoothRocBankStage(prices, length, kind, Round) : suppliedMeans.Select(R).ToArray();
        var residual = prices.Select((v,i) => Round(v - means[i])).ToArray();
        var extrapolated = prices.Select((_,i) => Round(Round(Round(R(2) * (i < length ? R(0) : residual[i-length]))
            - (i < 2L * length ? R(0) : residual[(int)(i - 2L * length)])) / R(2))).ToArray();
        var output = new double[bars.Count]; var previous = R(0); var previousSlope = R(0);
        for (var i = 0; i < bars.Count; i++)
        {
            var first = (int)Math.Max(0, i - 2L * length + 1); var low = extrapolated[i]; var high = low;
            for (var j = first; j <= i; j++)
            {
                if (extrapolated[j].CompareTo(low) < 0) low = extrapolated[j];
                if (extrapolated[j].CompareTo(high) > 0) high = extrapolated[j];
            }
            output[i] = high.CompareTo(low) == 0 ? 0 : (R(100) * (extrapolated[i] - low) / (high - low)).ToDouble();
            var value = R(output[i]); var slope = value - previous;
            signals?.Add(slope.Sign > 0 ? slope.CompareTo(previousSlope) > 0 ? Signal.StrongBuy : Signal.Buy
                : slope.Sign < 0 ? slope.CompareTo(previousSlope) < 0 ? Signal.StrongSell : Signal.Sell : Signal.None);
            previous = value; previousSlope = slope;
        }
        return Outputs(("Selo", output));
    }
}

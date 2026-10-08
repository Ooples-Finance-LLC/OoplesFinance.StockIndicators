using OoplesFinance.StockIndicators.Indicators;
namespace OoplesFinance.StockIndicators.Validation;
internal static partial class BuiltInFormulaReferences
{
    internal static IReadOnlyDictionary<string, double[]> AutonomousRecursiveOutputs(IReadOnlyList<Bar> bars, IBuiltInIndicator indicator)
        => AutonomousRecursiveValues(bars, Integer(indicator.CreateOptions(), "Length", 14), 7, 3).Outputs;
    internal static (Dictionary<string, double[]> Outputs, Signal[] Signals) AutonomousRecursiveValues(IReadOnlyList<Bar> bars, int length, int momentumLength, double gamma)
    {
        length = Math.Max(1, length); momentumLength = Math.Max(1, momentumLength);
        ReferenceFraction R(double value) => ReferenceFraction.FromDouble(value);
        ReferenceFraction Round(ReferenceFraction value) => value.RoundExtendedBinary64();
        var prices = bars.Select(b => R(b.Close)).ToArray(); var targets = new ReferenceFraction[bars.Count]; var firstMeans = new ReferenceFraction[bars.Count]; var lines = new ReferenceFraction[bars.Count];
        var sum = R(0); var factor = R(gamma); var previousSpread = R(0); var signals = new Signal[bars.Count];
        for (var i = 0; i < bars.Count; i++)
        {
            var previous = i == 0 ? prices[i] : lines[i - 1]; var lagged = i < momentumLength ? R(0) : prices[i - momentumLength];
            sum = Round(sum + Round((lagged - previous).Abs()));
            var radius = i == 0 ? R(0) : Round(Round(sum / new ReferenceFraction(i)) * factor);
            var upper = Round(previous + radius); var lower = Round(previous - radius);
            targets[i] = prices[i].CompareTo(upper) > 0 ? Round(prices[i] + radius) : prices[i].CompareTo(lower) < 0 ? Round(prices[i] - radius) : previous;
            var targetWindow = Window(targets, i, length).ToArray(); firstMeans[i] = Round(targetWindow.Aggregate(R(0), (total, value) => total + value) / new ReferenceFraction(targetWindow.Length));
            var firstWindow = Window(firstMeans, i, length).ToArray(); lines[i] = Round(firstWindow.Aggregate(R(0), (total, value) => total + value) / new ReferenceFraction(firstWindow.Length));
            var spread = prices[i] - lines[i]; signals[i] = spread.Sign > 0 && spread.CompareTo(previousSpread) > 0 ? Signal.StrongBuy : spread.Sign < 0 && spread.CompareTo(previousSpread) < 0 ? Signal.StrongSell : spread.Sign > 0 ? Signal.Buy : spread.Sign < 0 ? Signal.Sell : Signal.None; previousSpread = spread;
        }
        return (new Dictionary<string, double[]> { ["Arma"] = lines.Select(v => v.ToDouble()).ToArray() }, signals);
    }
}

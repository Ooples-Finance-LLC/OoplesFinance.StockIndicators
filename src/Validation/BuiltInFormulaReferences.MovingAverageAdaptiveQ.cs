using OoplesFinance.StockIndicators.Indicators;
namespace OoplesFinance.StockIndicators.Validation;
internal static partial class BuiltInFormulaReferences
{
    internal static readonly IndicatorErrorBudget MovingAverageAdaptiveQBudget = new(0, 4e-15, requireSameSign: true);
    internal static Dictionary<string, double[]> MovingAverageAdaptiveQOutputs(IReadOnlyList<Bar> bars, IBuiltInIndicator indicator)
    {
        var options = indicator.CreateOptions();
        return MovingAverageAdaptiveQValues(Closes(bars), Integer(options, "Length", 10), Number(options, .667, "FastAlpha"), Number(options, .0645, "SlowAlpha")).Outputs;
    }
    internal static (Dictionary<string, double[]> Outputs, Signal[] Signals) MovingAverageAdaptiveQValues(double[] prices, int length, double fast, double slow)
    {
        length = Math.Max(1, length); ReferenceFraction R(double v) => ReferenceFraction.FromDouble(v);
        var zero = R(0); var anchor = zero; var memory = zero; var priorMargin = zero;
        var values = new double[prices.Length]; var signals = new Signal[prices.Length];
        ReferenceFraction Abs(ReferenceFraction value) => value.Sign < 0 ? zero - value : value;
        ReferenceFraction Compact(ReferenceFraction value) => CompactReferenceFraction(value);
        for (var i = 0; i < prices.Length; i++)
        {
            var current = R(prices[i]); var efficiency = zero;
            if (i >= length)
            {
                var moves = zero;
                for (var j = i - length + 1; j <= i; j++) moves += Abs(R(prices[j]) - R(prices[j - 1]));
                if (moves.Sign != 0) efficiency = Abs(current - R(prices[i - length])) / moves;
            }
            var prior = i == 0 ? current : anchor + memory;
            var rate = R(fast) * efficiency + R(slow);
            var mean = prior + rate * rate * (current - prior); values[i] = mean.ToDouble();
            var gap = mean - current;
            if (Abs(mean).CompareTo(Abs(gap)) < 0) { anchor = zero; memory = Compact(mean); }
            else { anchor = current; memory = Compact(gap); }
            var margin = current - mean; var direction = margin.CompareTo(priorMargin);
            signals[i] = margin.Sign > 0 && direction > 0 ? Signal.StrongBuy : margin.Sign < 0 && direction < 0 ? Signal.StrongSell
                : margin.Sign > 0 ? Signal.Buy : margin.Sign < 0 ? Signal.Sell : Signal.None;
            priorMargin = margin;
        }
        return (new Dictionary<string, double[]> { ["Maaq"] = values }, signals);
    }
}

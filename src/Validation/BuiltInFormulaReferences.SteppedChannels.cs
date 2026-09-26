using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.Validation;

internal static partial class BuiltInFormulaReferences
{
    private static FormulaDefinition? SteppedChannels(IBuiltInIndicator indicator)
    {
        var name = indicator.BatchName;
        var linear = name is IndicatorName.LinearChannels or IndicatorName.LinearTrailingStop;
        var motion = name is IndicatorName.MotionToAttractionChannels or IndicatorName.MotionToAttractionTrailingStop;
        if (!linear && !motion) return null;
        var stop = name is IndicatorName.LinearTrailingStop or IndicatorName.MotionToAttractionTrailingStop;
        var options = indicator.CreateOptions();
        var length = Integer(options, "Length", 14);
        var multiplier = Number(options, stop ? 28 : 50, "Mult");
        var keys = stop ? new[] { "Ts" } : linear ? new[] { "UpperBand", "LowerBand" } : new[] { "UpperBand", "MiddleBand", "LowerBand" };
        return new(stop ? "Ts" : linear ? "UpperBand" : "MiddleBand", keys, bars =>
        {
            var upper = new double[bars.Count];
            var lower = new double[bars.Count];
            var anchor = new double[bars.Count];
            var trailing = new double[bars.Count];
            double high = 0, low = 0;
            var highAttractions = 0;
            var lowAttractions = 0;
            var rising = false;
            for (var i = 0; i < bars.Count; i++)
            {
                var price = bars[i].Close;
                if (linear)
                {
                    var previous = i == 0 ? price : anchor[i - 1];
                    var older = i < 2 ? price : anchor[i - 2];
                    var projected = price + multiplier * (previous - older);
                    var step = 1d / length;
                    var direction = projected > previous + step ? 1 : projected < previous - step ? -1 : 0;
                    anchor[i] = previous + direction * step;
                    var radius = Math.Abs(anchor[i] - previous) * multiplier;
                    upper[i] = anchor[i] + radius == anchor[i] ? i == 0 ? 0 : upper[i - 1] : anchor[i] + radius; // NOSONAR: S1244 - Detect whether the rounded addition actually changes the anchor.
                    lower[i] = anchor[i] - radius == anchor[i] ? i == 0 ? 0 : lower[i - 1] : anchor[i] - radius; // NOSONAR: S1244 - Detect whether the rounded subtraction actually changes the anchor.
                }
                else
                {
                    if (i == 0) high = low = price;
                    var nextHigh = price > (i == 0 ? price : upper[i - 1]) ? price : high;
                    var nextLow = price < (i == 0 ? price : lower[i - 1]) ? price : low;
                    var highChanged = nextHigh != high; // NOSONAR: S1244 - The state transition requires a bound actually changing.
                    var lowChanged = nextLow != low; // NOSONAR: S1244 - The state transition requires a bound actually changing.
                    highAttractions = lowChanged ? Math.Min(length, highAttractions + 1) : highChanged ? 0 : highAttractions;
                    lowAttractions = highChanged ? Math.Min(length, lowAttractions + 1) : lowChanged ? 0 : lowAttractions;
                    high = nextHigh; low = nextLow;
                    var halfRange = (high - low) / 2;
                    upper[i] = high - highAttractions / (double)length * halfRange;
                    lower[i] = low + lowAttractions / (double)length * halfRange;
                }
                var upperTrigger = linear ? upper[i] : i == 0 ? price : upper[i - 1];
                var lowerTrigger = linear ? lower[i] : i == 0 ? price : lower[i - 1];
                if (price > upperTrigger) rising = true;
                else if (price < lowerTrigger) rising = false;
                trailing[i] = rising ? lower[i] : upper[i];
            }
            return stop ? Outputs(("Ts", trailing)) : Outputs(("UpperBand", upper), ("LowerBand", lower),
                ("MiddleBand", upper.Zip(lower, (u, l) => (u + l) / 2).ToArray()));
        });
    }
}

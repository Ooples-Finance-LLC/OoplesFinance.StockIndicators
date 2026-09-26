using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.Validation;

internal static partial class BuiltInFormulaReferences
{
    private static FormulaDefinition? ConfluenceFormula(IBuiltInIndicator indicator)
    {
        if (indicator.BatchName != IndicatorName.ConfluenceIndicator) return null;
        var options = indicator.CreateOptions(); var kind = AverageKind(options, 1);
        if (kind == 0) return null;
        var length = Integer(options, "Length");
        return new("Ci", new[] { "Ci" }, bars =>
        {
            var periods = new[] { length, 2 * length - 1, 4 * length - 3, 8 * length - 7 };
            var delays = periods.Take(3).Select(p => p / 2).ToArray();
            var prices = Closes(bars);
            var means = periods.Select(p => Average(prices, p, kind)).ToArray();
            double At(double[] values, int i) => i < 0 ? 0 : values[i];
            var projections = means.Select(mean => mean.Select((v, i) => 2 * v - At(mean, i - 1)).ToArray()).ToArray();
            var shortPeriod = Math.Max(2, Math.Min(530, length - 1));
            var shortMean = Average(prices, shortPeriod, kind);
            var fullMean = Average(bars.Select(b => (b.Open + b.High + b.Low + b.Close) / 4).ToArray(), Math.Max(1, periods[3] - 1), kind);
            var lastMean = Average(prices, Math.Max(1, periods[3] - 1), kind);
            for (var i = 0; i < bars.Count; i++)
            {
                projections[0][i] += (length - 1d - shortPeriod) / length * shortMean[i];
                projections[3][i] += (periods[3] - 1d) / periods[3] * (fullMean[i] - lastMean[i]);
            }
            var momentum = new double[bars.Count]; var benchmark = new double[bars.Count];
            var cyclic = new double[bars.Count]; var baselineCyclic = new double[bars.Count];
            for (var i = 0; i < bars.Count; i++)
                for (var stage = 0; stage < 3; stage++)
                {
                    momentum[i] += projections[stage + 1][i] - At(projections[stage], i - delays[stage]);
                    benchmark[i] += means[stage + 1][i] - At(means[stage], i - delays[stage]);
                    cyclic[i] += Math.Sqrt(2) * Math.Sin(projections[stage][i] * Math.PI / 180 + Math.PI / 4);
                    baselineCyclic[i] += Math.Sqrt(2) * Math.Sin(means[stage][i] * Math.PI / 180 + Math.PI / 4);
                }
            int Order(double a, double b) => Math.Abs(a - b) <= 1e-12 * Math.Max(1, Math.Max(Math.Abs(a), Math.Abs(b))) ? 0 : a > b ? 1 : -1;
            var error = cyclic.Select((v, i) => (v - baselineCyclic[i]) *
                (Order(v, At(cyclic, i - delays[1])) * Order(means[0][i], At(means[0], i - delays[1])) < 0 ? -1 : 1)).ToArray();
            var spread = prices.Select((_, i) => projections[0][i] - projections[3][i]).ToArray();
            int Vote(double value, double previous, double signal)
            {
                var sign = Order(value, 0); var motion = Order(value, previous); var position = Order(value, signal);
                return sign == 0 || motion == 0 || position == 0 ? 0 : sign * (1 + (motion == sign ? 1 : 0) + (position == sign ? 1 : 0));
            }
            return Outputs(("Ci", prices.Select((_, i) =>
            {
                var errorSignal = delays[1] == 0 ? 0 : Window(error, i, delays[1]).Average();
                var total = Vote(error[i], At(error, i - 1), errorSignal)
                    + Vote(momentum[i], At(momentum, i - 1), benchmark[i])
                    + Vote(spread[i], At(spread, i - 1), Window(spread, i, length).Average());
                return Order(spread[i], 0) == 0 ? 0 : Math.Sign(total) == Order(spread[i], 0) ? total : total / 10d;
            }).ToArray()));
        });
    }
}

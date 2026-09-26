using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.Validation;

internal static partial class BuiltInFormulaReferences
{
    private static FormulaDefinition? SelfAdjustingLaguerreFormula(IBuiltInIndicator indicator)
    {
        if (indicator.BatchName != IndicatorName.EhlersLaguerreRelativeStrengthIndexWithSelfAdjustingAlpha) return null;
        var length = Integer(indicator.CreateOptions(), "Length");
        return new("Elrsiwsa", new[] { "Elrsiwsa" }, bars =>
        {
            var ranges = bars.Select((b, i) => Math.Max(b.High, i == 0 ? 0 : bars[i-1].Close)
                - Math.Min(b.Low, i == 0 ? 0 : bars[i-1].Close)).ToArray();
            var ratios = bars.Select((_, i) =>
            {
                var window = Window(bars, i, length).ToArray();
                var range = window.Max(b => b.High)-window.Min(b => b.Low);
                return range == 0 ? 0 : ranges[i]/range;
            }).ToArray();
            var gains = ratios.Select((_, i) =>
            {
                var sum = Window(ratios, i, length).Sum();
                return sum <= 0 ? .01 : length == 1 ? .99 : Clamp(Math.Log(sum)/Math.Log(length), .01, .99);
            }).ToArray();
            var source = bars.Select((b, i) =>
            {
                var previous = i == 0 ? 0 : bars[i-1].Close;
                return ((b.Open+previous)/2+Math.Max(b.High, previous)+Math.Min(b.Low, previous)+b.Close)/4;
            }).ToArray();
            decimal[] Integrate(decimal[] forcing) => forcing.Select((_, i) =>
            {
                decimal sum = 0, retained = 1;
                for (var j = i; j >= 0; j--) { sum += retained*forcing[j]; retained *= 1-(decimal)gains[j]; }
                return sum;
            }).ToArray();
            var stages = new List<decimal[]> { Integrate(source.Select((v, i) => (decimal)gains[i]*(decimal)v).ToArray()) };
            for (var stage = 1; stage < 4; stage++)
            {
                var previous = stages.Last();
                stages.Add(Integrate(previous.Select((v, i) => (i == 0 ? 0 : previous[i-1])-(1-(decimal)gains[i])*v).ToArray()));
            }
            var output = source.Select((_, i) =>
            {
                var differences = Enumerable.Range(0, 3).Select(j => stages[j][i]-stages[j+1][i]).ToArray();
                var total = differences.Sum(Math.Abs);
                var scale = stages.Max(stage => Math.Abs(stage[i]));
                return (double)total <= 64*Math.Pow(2, -52)*(double)scale ? 0
                    : (double)(differences.Sum(v => Math.Max(0, v))/total);
            }).ToArray();
            return Outputs(("Elrsiwsa", output));
        });
    }
}

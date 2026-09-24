using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Builder.Specs;

namespace OoplesFinance.StockIndicators.Validation;

internal static partial class BuiltInFormulaReferences
{
    private static FormulaDefinition? UltimateMomentumFormula(IBuiltInIndicator indicator)
    {
        if (indicator.CreateOptions() is not UltimateMomentumIndicatorSpecOptions options) return null;
        var kind = AverageKind(options, 1);
        if (kind == 0) return null;
        return new("Utm", new[] { "Utm" }, bars =>
        {
            var prices = Closes(bars);
            var direction = prices.Select((v, i) => Math.Sign(v-(i == 0 ? 0 : prices[i-1]))).ToArray();
            var advances = prices.Select((_, i) => (double)Window(direction, i, options.Length2).Count(v => v > 0)).ToArray();
            var declines = prices.Select((_, i) => (double)Window(direction, i, options.Length2).Count(v => v < 0)).ToArray();
            var net = advances.Select((v, i) => v+declines[i] == 0 ? 0 : 1000*(v-declines[i])/(v+declines[i])).ToArray();
            var fast = Average(net, options.Length2, kind); var slow = Average(net, options.Length4, kind);
            var exactPrices = prices.Select(BinaryDecimal).ToArray();
            var center = MotionDecimalAverage(exactPrices, options.Length5, kind);
            var position = prices.Select((_, i) =>
            {
                if (i+1 < options.Length5 || options.StdDevMult == 0) return 0d;
                var window = Window(exactPrices, i, options.Length5).ToArray(); var mean = window.Average();
                var sigma = Math.Sqrt(window.Average(v => Math.Pow((double)(v-mean), 2)));
                return sigma == 0 ? 0 : 50+50*(double)(exactPrices[i]-center[i])/(options.StdDevMult*sigma);
            }).ToArray();
            var typical = bars.Select(b => (b.High+b.Low+b.Close)/3).ToArray();
            var inflow = typical.Select((v, i) => i > 0 && v > typical[i-1] ? v*bars[i].Volume : 0).ToArray();
            var outflow = typical.Select((v, i) => i > 0 && v < typical[i-1] ? v*bars[i].Volume : 0).ToArray();
            double[] Flow(int length) => prices.Select((_, i) =>
            {
                var positive = Window(inflow, i, length).Sum(); var negative = Window(outflow, i, length).Sum();
                return negative == 0 ? 100 : 100*positive/(positive+negative);
            }).ToArray();
            var flow1 = Flow(options.Length2); var flow2 = Flow(options.Length3); var flow3 = Flow(options.Length4);
            var blend = prices.Select((_, i) => 200*position[i]+(declines[i] == 0 ? 0 : 100*advances[i]/declines[i])
                +2*(fast[i]-slow[i])+3*(flow1[i]+flow2[i])+1.5*flow3[i]).ToArray();
            for (var i = 1; i < blend.Length; i++)
                if (Math.Abs(blend[i]-blend[i-1]) <= 64*Math.Pow(2, -52)*Math.Max(Math.Abs(blend[i]), Math.Abs(blend[i-1])))
                    blend[i] = blend[i-1];
            var changes = blend.Select((v, i) => i == 0 ? 0 : v-blend[i-1]).ToArray();
            var gains = Average(changes.Select(v => Math.Max(0, v)).ToArray(), options.Length1, kind);
            var losses = Average(changes.Select(v => Math.Max(0, -v)).ToArray(), options.Length1, kind);
            var strength = gains.Select((v, i) => losses[i] == 0 ? 100 : 100*v/(v+losses[i])).ToArray();
            if (options.Length1 > 1 && kind is 3 or 6)
                for (var i = 1; i < strength.Length; i++) if (changes[i] == 0) strength[i] = strength[i-1];
            return Outputs(("Utm", Average(strength, options.Length1, kind)));
        });
    }
}

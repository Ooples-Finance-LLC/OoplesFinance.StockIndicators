using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.Validation;

internal static partial class BuiltInFormulaReferences
{
    private static FormulaDefinition? CandleTrends(IBuiltInIndicator indicator)
    {
        if (indicator.BatchName == IndicatorName.VervoortModifiedBollingerBandIndicator)
        {
            var options = indicator.CreateOptions();
            var band = Integer(options, "Length1"); var outer = Integer(options, "Length2");
            var smooth = Integer(options, "SmoothLength"); var multiplier = Number(options, 1.6, "StdDevMult");
            var kind = AverageKind(options, 5);
            if (kind == 0) return null;
            return new("MiddleBand", new[] { "UpperBand", "MiddleBand", "LowerBand", "PercentB" }, bars =>
            {
                var source = bars.Select(b => (decimal)((b.Open+b.High+b.Low+b.Close)/4)).ToArray();
                var open = source.Select((_, i) =>
                {
                    decimal sum = 0, weight = .5m;
                    for (var j = i-1; j >= 0; j--) { sum += source[j]*weight; weight /= 2; }
                    return sum;
                }).ToArray();
                var close = bars.Select((b, i) => (source[i]+open[i]+Math.Max((decimal)b.High, open[i])+Math.Min((decimal)b.Low, open[i]))/4).ToArray();
                var first = MotionDecimalAverage(close, smooth, kind); var second = MotionDecimalAverage(first, smooth, kind);
                var filtered = MotionDecimalAverage(first.Select((v, i) => 2*v-second[i]).ToArray(), smooth, kind);
                var center = MotionDecimalAverage(filtered, band, 2);
                var percent = filtered.Select((v, i) =>
                {
                    if (i+1 < band) return 0d;
                    var window = Window(filtered, i, band).ToArray(); var mean = window.Average();
                    var sigma = Math.Sqrt(window.Average(x => Math.Pow((double)(x-mean), 2)));
                    return sigma <= 64*Math.Pow(2, -52)*(double)window.Max(Math.Abs) ? 0
                        : 50+25*(double)(v-center[i])/sigma;
                }).ToArray();
                var outerVariance = PopulationVariance(percent, outer);
                return Outputs(("PercentB", percent), ("MiddleBand", Enumerable.Repeat(50d, bars.Count).ToArray()),
                    ("UpperBand", outerVariance.Select(v => 50+multiplier*Math.Sqrt(v)).ToArray()),
                    ("LowerBand", outerVariance.Select(v => 50-multiplier*Math.Sqrt(v)).ToArray()));
            });
        }
        if (indicator.BatchName == IndicatorName.VervoortSmoothedOscillator)
        {
            var opts = indicator.CreateOptions();
            var bandPeriod = Integer(opts, "Length1"); var rangePeriod = Integer(opts, "Length2");
            var cascadePeriod = Integer(opts, "Length3"); var smoothPeriod = Integer(opts, "SmoothLength");
            var multiplier = Number(opts, 2, "StdDevMult");
            return new("Vso", new[] { "Vso", "Sk" }, bars =>
            {
                var prices = Closes(bars); var layers = new List<double[]>(); var stage = prices;
                for (var depth = 0; depth < 10; depth++) { stage = Average(stage, cascadePeriod, 1); layers.Add(stage); }
                var rainbow = prices.Select((_, i) => layers.Select((layer, depth) => layer[i]*Math.Max(1, 5-depth)).Sum()/20).ToArray();
                var preciseLayers = new List<decimal[]>(); var preciseStage = prices.Select(v => (decimal)v).ToArray();
                for (var depth = 0; depth < 10; depth++) { preciseStage = MotionDecimalAverage(preciseStage, cascadePeriod, 1); preciseLayers.Add(preciseStage); }
                var exactRainbow = prices.Select((_, i) => preciseLayers.Select((layer, depth) => layer[i]*Math.Max(1, 5-depth)).Sum()/20).ToArray();
                var filtered = MotionDecimalAverage(MotionDecimalAverage(exactRainbow, smoothPeriod, 4), smoothPeriod, 5);
                var center = MotionDecimalAverage(filtered, bandPeriod, 2);
                var position = filtered.Select((v, i) =>
                {
                    if (i+1 < bandPeriod || multiplier == 0) return 0d;
                    var window = Window(filtered, i, bandPeriod).ToArray(); var mean = window.Average();
                    var sigma = Math.Sqrt(window.Average(x => Math.Pow((double)(x-mean), 2)));
                    return sigma <= 64*Math.Pow(2, -52)*(double)window.Max(Math.Abs) ? 0
                        : 50+50*(double)(v-center[i])/(multiplier*sigma);
                }).ToArray();
                var blended = bars.Select((b, i) => (rainbow[i]+(b.High+b.Low+b.Close)/3)/2).ToArray();
                var stochastic = prices.Select((_, i) =>
                {
                    var candles = Window(bars, i, rangePeriod).ToArray();
                    var denominator = candles.Max(b => b.High)-Window(blended, i, rangePeriod).Min();
                    return denominator == 0 ? 0 : Clamp(100*(blended[i]-candles.Min(b => b.Low))/denominator, 0, 100);
                }).ToArray();
                return Outputs(("Vso", position), ("Sk", stochastic.Select((_, i) => Window(stochastic, i, smoothPeriod).Average()).ToArray()));
            });
        }

        var longTerm = indicator.BatchName == IndicatorName.VervoortHeikenAshiLongTermCandlestickOscillator;
        if (!longTerm && indicator.BatchName != IndicatorName.VervoortHeikenAshiCandlestickOscillator) return null;
        var key = longTerm ? "Vhaltco" : "Vhaco";
        return new(key,new[]{key},bars=>VervoortCandleOutputs(bars,indicator));
    }
}

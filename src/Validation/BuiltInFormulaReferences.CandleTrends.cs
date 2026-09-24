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
        var length = Integer(indicator.CreateOptions(), "Length");
        var key = longTerm ? "Vhaltco" : "Vhaco";
        return new(key, new[] { key }, bars =>
        {
            var count = bars.Count;
            var full = bars.Select(b => (b.Open + b.High + b.Low + b.Close) / 4).ToArray();
            // Expand the half-weight open filter rather than using the production recurrence.
            var open = bars.Select((_, i) => Enumerable.Range(0, i).Sum(j => full[j] * Math.Pow(.5, i - j))).ToArray();
            var close = bars.Select((b, i) => (full[i] + open[i] + Math.Max(b.High, open[i]) + Math.Min(b.Low, open[i])) / 4).ToArray();
            double[] Smooth(double[] values)
            {
                var first = Average(values, length, 5);
                if (longTerm) return first;
                var second = Average(first, length, 5);
                return first.Select((v, i) => 2 * v - second[i]).ToArray();
            }
            double[] Project(double[] values)
            {
                var first = Smooth(values); var second = Smooth(first);
                return first.Select((v, i) => 2 * v - second[i]).ToArray();
            }
            var transformed = Project(close);
            var midpoint = Project(bars.Select(b => (b.High + b.Low) / 2).ToArray());
            var upSeed = new bool[count]; var downSeed = new bool[count];
            var upKeep = new bool[count]; var downKeep = new bool[count];
            var upTrend = new bool[count]; var downTrend = new bool[count];
            var result = new double[count];
            for (var i = 0; i < count; i++)
            {
                var b = bars[i];
                var previousHigh = i == 0 ? 0 : bars[i - 1].High;
                var previousLow = i == 0 ? 0 : bars[i - 1].Low;
                var previousClose = i == 0 ? 0 : bars[i - 1].Close;
                var previousUpCandle = i == 0 || close[i - 1] >= open[i - 1];
                var previousDownCandle = i > 0 && close[i - 1] < open[i - 1];
                var risingCandle = b.Close >= b.Open || b.Close >= previousClose;
                var fallingCandle = b.Close < b.Open || b.Close < previousClose;
                var narrow = Math.Abs(b.Close - b.Open) < (b.High - b.Low) * (longTerm ? 1.1 : .35);
                upSeed[i] = close[i] >= open[i] && previousUpCandle || midpoint[i] >= transformed[i]
                    || longTerm && (b.Close >= close[i] || b.High > previousHigh || b.Low > previousLow);
                downSeed[i] = close[i] < open[i] && previousDownCandle || midpoint[i] < transformed[i];
                upKeep[i] = longTerm ? upSeed[i] || i > 0 && upSeed[i - 1] && risingCandle
                    : (upSeed[i] || i > 0 && upSeed[i - 1]) && risingCandle;
                downKeep[i] = longTerm ? downSeed[i] || i > 0 && downSeed[i - 1] && fallingCandle
                    : (downSeed[i] || i > 0 && downSeed[i - 1]) && fallingCandle;
                upTrend[i] = upKeep[i] || i > 0 && upKeep[i - 1] && narrow && b.High >= previousLow;
                downTrend[i] = longTerm ? (downKeep[i] || i > 0 && downKeep[i - 1]) && narrow && b.Low <= previousHigh
                    : downKeep[i] || i > 0 && downKeep[i - 1] && narrow && b.Low <= previousHigh;
                var upward = i > 0 && downTrend[i - 1] && !downTrend[i] && upTrend[i];
                var downward = i > 0 && upTrend[i - 1] && !upTrend[i] && downTrend[i];
                result[i] = upward ? 1 : downward ? -1 : i == 0 ? 0 : result[i - 1];
            }
            return Outputs((key, result));
        });
    }
}

using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.Validation;

internal static partial class BuiltInFormulaReferences
{
    internal static MultiSeriesFormulaReference? MultiSeriesFormula(IndicatorName name, IReadOnlyDictionary<string, object?> parameters)
    {
        int Period(string key, int fallback) => parameters.TryGetValue(key, out var value) ? Math.Max(1, (int)value!) : fallback;
        var average = parameters.TryGetValue("maType", out var selected) ? (MovingAvgType)selected! : MovingAvgType.SimpleMovingAverage;
        var kind = average == MovingAvgType.SimpleMovingAverage ? 1 : average == MovingAvgType.WeightedMovingAverage ? 2
            : average == MovingAvgType.ExponentialMovingAverage ? 3 : 0;
        if (kind == 0) return null;
        if (name is not (IndicatorName.ComparePriceMomentumOscillator or IndicatorName.KaufmanStressIndicator or IndicatorName.RSMKIndicator
            or IndicatorName.RelativeNormalizedVolatility or IndicatorName.RelativeStrength3DIndicator or IndicatorName.SectorRotationModel)) return null;
        return (primary, benchmark) =>
        {
            var p = Closes(primary); var b = Closes(benchmark);
            var result = new Dictionary<string, IReadOnlyList<double>>();
            double[] Roc(double[] x, int lag) => x.Select((v, i) => i < lag || x[i-lag] == 0 ? 0 : 100*(v/x[i-lag]-1)).ToArray();
            switch (name)
            {
                case IndicatorName.ComparePriceMomentumOscillator:
                    double[] Pmo(double[] x)
                    {
                        double[] Smooth(double[] source, int period) => source.Select((_, i) => Enumerable.Range(0, i+1)
                            .Sum(j => source[j]*2/period*Math.Pow(1-2d/period, i-j))).ToArray();
                        return Smooth(Smooth(Roc(x, 1), Period("length1", 20)), Period("length2", 35));
                    }
                    var primaryPmo = Pmo(p); var benchmarkPmo = Pmo(b);
                    result["Cpmo"] = p.Select((_, i) => 10*(primaryPmo[i]-benchmarkPmo[i])).ToArray();
                    break;
                case IndicatorName.RSMKIndicator:
                    var horizon = Period("length", 90);
                    var log = p.Select((v, i) => v > 0 && b[i] > 0 ? Math.Log(v)-Math.Log(b[i]) : 0).ToArray();
                    var change = log.Select((v, i) => i < horizon ? 0 : v-log[i-horizon]).ToArray();
                    result["Rsmk"] = Average(change, Period("smoothLength", 3), kind).Select(v => 100*v).ToArray();
                    break;
                case IndicatorName.KaufmanStressIndicator:
                    var window = Period("length", 60);
                    double[] Location(IReadOnlyList<Bar> source) => source.Select((bar, i) =>
                    {
                        var sample = Window(source, i, window).ToArray(); var lo = sample.Min(v => v.Low); var hi = sample.Max(v => v.High);
                        return hi == lo ? .5 : (bar.Close-lo)/(hi-lo); // NOSONAR: S1244 - Equal bounds define an exactly zero range; a nonzero range must still be evaluated.
                    }).ToArray();
                    var stockPosition = Location(primary); var marketPosition = Location(benchmark);
                    var spread = stockPosition.Select((v, i) => Math.Abs(v-marketPosition[i]) <= 64*Math.Pow(2, -52) ? 0 : v-marketPosition[i]).ToArray();
                    result["Ksi"] = spread.Select((v, i) =>
                    {
                        var sample = Window(spread, i, window).ToArray(); var lo = sample.Min(); var hi = sample.Max();
                        return hi == lo ? 50 : 100*(v-lo)/(hi-lo); // NOSONAR: S1244 - Equal bounds define an exactly zero range; a nonzero range must still be evaluated.
                    }).ToArray();
                    break;
                case IndicatorName.RelativeNormalizedVolatility:
                    var length = Period("length", 14);
                    double[] Volatility(double[] source) => Average(source.Select((v, i) =>
                    {
                        if (i == 0 || i+1 < length) return 0d;
                        var sample = Window(source, i, length).Select(BinaryDecimal).ToArray(); var center = sample.Average();
                        var sigma = Math.Sqrt((double)sample.Average(x => (x-center)*(x-center)));
                        return sigma == 0 ? 0 : Math.Abs(v-source[i-1])/sigma;
                    }).ToArray(), length, kind);
                    var stockVolatility = Volatility(p); var marketVolatility = Volatility(b);
                    result["Rnv"] = p.Select((_, i) => marketVolatility[i] == 0 ? 0 : stockVolatility[i]/marketVolatility[i]).ToArray();
                    break;
                case IndicatorName.SectorRotationModel:
                    var first = Period("length1", 25); var second = Period("length2", 75);
                    var stockFirst = Roc(p, first); var stockSecond = Roc(p, second);
                    var marketFirst = Roc(b, first); var marketSecond = Roc(b, second);
                    var rotation = p.Select((_, i) => 50*(stockFirst[i]+stockSecond[i]-marketFirst[i]-marketSecond[i])).ToArray();
                    result["Srm"] = rotation; result["Signal"] = Average(rotation, first, kind);
                    break;
                case IndicatorName.RelativeStrength3DIndicator:
                    var ratio = new double[p.Length];
                    for (var i = 0; i < ratio.Length; i++) ratio[i] = b[i] == 0 ? i == 0 ? 0 : ratio[i-1] : 100*p[i]/b[i];
                    var fast = Average(ratio, Period("length3", 10), kind); var medium = Average(fast, Period("length2", 7), kind);
                    var slowLength = Period("length4", 15); var slow = Average(fast, slowLength, kind);
                    var verySlow = Average(slow, Period("length5", 30), kind);
                    bool Below(double x, double y) => y-x > 1e-12*Math.Max(1, Math.Max(Math.Abs(x), Math.Abs(y)));
                    var score = p.Select((_, i) => Below(medium[i], slow[i]) ? 0d : Below(fast[i], medium[i])
                        ? Below(slow[i], verySlow[i]) ? 5 : 9 : Below(slow[i], verySlow[i]) ? 9 : 10).ToArray();
                    var scoreMean = Average(score, Period("length1", 4), kind);
                    result["Rs3d"] = score.Select((v, i) => v >= 5 || Below(scoreMean[i], v)
                        ? 100d*Window(score, i, slowLength).Count(x => x >= 5)/slowLength : 0).ToArray();
                    break;
            }
            return result;
        };
    }
}

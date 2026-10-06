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
            ReferenceFraction D(double v) => ReferenceFraction.FromDouble(v);
            ReferenceFraction R(ReferenceFraction v) => RoundRocBankStage(v);
            var zero = D(0);
            ReferenceFraction[] Mean(ReferenceFraction[] values, int length) => SmoothRocBankStage(values, length, kind);
            ReferenceFraction[] Roc(double[] x, int lag) => x.Select((v, i) => i < lag || x[i-lag] == 0 ? zero
                : R((D(v)-D(x[i-lag]))*D(100)/D(x[i-lag]))).ToArray();
            switch (name)
            {
                case IndicatorName.ComparePriceMomentumOscillator:
                    ReferenceFraction[] Pmo(double[] x)
                    {
                        var roc = Roc(x, 1); var mean = zero; var previous = zero;
                        var values = new ReferenceFraction[x.Length];
                        var first = D(Period("length1", 20)); var second = D(Period("length2", 35));
                        for (var i = 0; i < x.Length; i++)
                        {
                            mean = R(((first-D(2))*mean + D(2)*roc[i])/first);
                            previous = R(((second-D(2))*previous + D(2)*R(mean*D(10)))/second);
                            values[i] = previous;
                        }
                        return values;
                    }
                    var primaryPmo = Pmo(p); var benchmarkPmo = Pmo(b);
                    result["Cpmo"] = p.Select((_, i) => R(primaryPmo[i]-benchmarkPmo[i]).ToDouble()).ToArray();
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
                    ReferenceFraction[] Volatility(double[] source) => Mean(source.Select((v, i) =>
                    {
                        if (i == 0 || i+1 < length) return zero;
                        var sample = Window(source, i, length).Select(D).ToArray();
                        var center = sample.Aggregate(zero, (sum,x) => sum+x) / D(sample.Length);
                        var variance = sample.Aggregate(zero, (sum,x) => sum+(x-center)*(x-center)) / D(sample.Length);
                        var sigma = variance.SqrtToDouble();
                        return sigma == 0 ? zero : R(R(D(v)-D(source[i-1])).Abs()/D(sigma));
                    }).ToArray(), length);
                    var stockVolatility = Volatility(p); var marketVolatility = Volatility(b);
                    result["Rnv"] = p.Select((_, i) => marketVolatility[i].Sign == 0 ? 0 : R(stockVolatility[i]/marketVolatility[i]).ToDouble()).ToArray();
                    break;
                case IndicatorName.SectorRotationModel:
                    var first = Period("length1", 25); var second = Period("length2", 75);
                    var stockFirst = Roc(p, first); var stockSecond = Roc(p, second);
                    var marketFirst = Roc(b, first); var marketSecond = Roc(b, second);
                    var rotation = p.Select((_, i) => R(D(100)*R(R(R(stockFirst[i]+stockSecond[i])/D(2))-R(R(marketFirst[i]+marketSecond[i])/D(2))))).ToArray();
                    result["Srm"] = rotation.Select(v=>v.ToDouble()).ToArray();
                    result["Signal"] = Mean(rotation, first).Select(v=>v.ToDouble()).ToArray();
                    break;
                case IndicatorName.RelativeStrength3DIndicator:
                    var ratio = new ReferenceFraction[p.Length];
                    for (var i = 0; i < ratio.Length; i++) ratio[i] = b[i] == 0 ? i == 0 ? zero : ratio[i-1] : R(R(D(p[i])/D(b[i]))*D(100));
                    var fast = Mean(ratio, Period("length3", 10)); var medium = Mean(fast, Period("length2", 7));
                    var slowLength = Period("length4", 15); var slow = Mean(fast, slowLength);
                    var verySlow = Mean(slow, Period("length5", 30));
                    // Compare before projecting extended-range components to binary64.
                    bool Below(ReferenceFraction x, ReferenceFraction y) => x.CompareTo(y) < 0;
                    var score = p.Select((_, i) => Below(medium[i], slow[i]) ? 0d : Below(fast[i], medium[i])
                        ? Below(slow[i], verySlow[i]) ? 5 : 9 : Below(slow[i], verySlow[i]) ? 9 : 10).ToArray();
                    var scoreMean = Average(score, Period("length1", 4), kind);
                    result["Rs3d"] = score.Select((v, i) => v >= 5 || scoreMean[i] < v
                        ? (double)Window(score, i, slowLength).Count(x => x >= 5)/slowLength*100 : 0).ToArray();
                    break;
            }
            return result;
        };
    }
}

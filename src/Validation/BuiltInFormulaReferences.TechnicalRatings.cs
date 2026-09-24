using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Builder.Specs;

namespace OoplesFinance.StockIndicators.Validation;

internal static partial class BuiltInFormulaReferences
{
    private static FormulaDefinition? TechnicalRatingsFormula(IBuiltInIndicator indicator)
    {
        if (indicator.CreateOptions() is not TechnicalRatingsSpecOptions options) return null;
        var kind = AverageKind(options, 3);
        if (kind == 0) return null;
        return new("Tr", new[] { "Tr", "Or", "Mr" }, bars =>
        {
            var price = Closes(bars);
            // Only independent reference definitions are composed here; no engines or states.
            var rsi = Foundation((IBuiltInIndicator)new Rsi(options.RsiLength))!.Compute(bars)["Rsi"];
            var directional = Foundation((IBuiltInIndicator)new Adx(14))!.Compute(bars);
            var channel = Statistics((IBuiltInIndicator)new Cci(20))!.Compute(bars)["Cci"];
            var hull = Foundation((IBuiltInIndicator)new HullMovingAverage(9))!.Compute(bars)["Hma"];
            var volume = Foundation((IBuiltInIndicator)new VolumeWeightedMovingAverage(20))!.Compute(bars)["Vwma"];
            var averages = new[] { 10, 20, 30, 50, 100, 200 }.Select(p => Average(price, p, kind)).ToArray();
            var median = bars.Select(b => (b.High+b.Low)/2).ToArray();
            var aoFast = Average(median, options.AoLength1, 1); var aoSlow = Average(median, options.AoLength2, 1);
            var awesome = price.Select((_, i) => aoFast[i]-aoSlow[i]).ToArray();
            var fast = Average(price, 12, 3); var slow = Average(price, 26, 3);
            var macd = price.Select((_, i) => fast[i]-slow[i]).ToArray(); var macdSignal = Average(macd, 9, 3);
            var bullBear = Average(price, 13, 3);
            var bull = bars.Select((b, i) => b.High-bullBear[i]).ToArray(); var bear = bars.Select((b, i) => b.Low-bullBear[i]).ToArray();
            var momentum = price.Select((v, i) => i < 10 || price[i-10] == 0 ? 0 : 100*v/price[i-10]).ToArray();
            double[] Position(int period) => price.Select((v, i) =>
            {
                var window = Window(bars, i, period).ToArray(); var high = window.Max(b => b.High); var low = window.Min(b => b.Low);
                return high == low ? 0 : 100*(v-low)/(high-low);
            }).ToArray();
            var stoch = Position(options.StochLength1); var stochMean = Average(stoch, options.StochLength2, 1);
            var williams = Position(14).Select(v => v-100).ToArray();
            var stochasticRsi = rsi.Select((v, i) =>
            {
                var window = Window(rsi, i, options.StochLength1).ToArray(); var range = window.Max()-window.Min();
                return range == 0 ? 0 : 100*(v-window.Min())/range;
            }).ToArray();
            var stochasticRsiMean = Average(stochasticRsi, options.StochLength2, 1);
            double[] Midrange(int period) => price.Select((_, i) =>
                (Window(bars, i, period).Max(b => b.High)+Window(bars, i, period).Min(b => b.Low))/2).ToArray();
            var conversion = Midrange(9); var baseline = Midrange(26); var cloudB = Midrange(52);
            var cloudA = price.Select((_, i) => (conversion[i]+baseline[i])/2).ToArray();
            var trueRange = TrueRanges(bars);
            var pressure = bars.Select((b, i) => b.Close-Math.Min(b.Low, i == 0 ? b.Close : price[i-1])).ToArray();
            var ultimate = price.Select((_, i) => 100d/7*new[] { 7, 14, 28 }.Select((period, j) =>
            {
                var denominator = Window(trueRange, i, period).Sum();
                return denominator == 0 ? 0 : Math.Pow(2, 2-j)*Window(pressure, i, period).Sum()/denominator;
            }).Sum()).ToArray();
            var moving = new double[bars.Count]; var oscillator = new double[bars.Count];
            for (var i = 0; i < bars.Count; i++)
            {
                double Prev(double[] values, int lag = 1) => i < lag ? 0 : values[i-lag];
                // Independent implementation of the documented relative tie interval.
                int CompareVote(double x, double y)
                {
                    var radius = Math.Max(1, Math.Max(Math.Abs(x), Math.Abs(y))) / 1e12;
                    return x > y + radius ? 1 : x < y - radius ? -1 : 0;
                }
                int Vote(bool buy, bool sell) => buy ? 1 : sell ? -1 : 0;
                var p = price[i]; var previous = Prev(price);
                var movingVotes = averages.Select(a => CompareVote(p, a[i])).ToList();
                movingVotes.Add(CompareVote(p, hull[i])); movingVotes.Add(CompareVote(p, volume[i]));
                movingVotes.Add(Vote(CompareVote(cloudA[i], cloudB[i]) > 0 && CompareVote(p, cloudA[i]) > 0 && CompareVote(p, baseline[i]) < 0 && CompareVote(previous, conversion[i]) < 0 && CompareVote(p, conversion[i]) > 0,
                    CompareVote(cloudB[i], cloudA[i]) > 0 && CompareVote(p, cloudB[i]) < 0 && CompareVote(p, baseline[i]) > 0 && CompareVote(previous, conversion[i]) > 0 && CompareVote(p, conversion[i]) < 0));
                moving[i] = movingVotes.Sum()/9d;
                var plus = directional["DiPlus"]; var minus = directional["DiMinus"]; var adx = directional["Adx"];
                var votes = new[]
                {
                    Vote(CompareVote(rsi[i], 30) < 0 && CompareVote(Prev(rsi), rsi[i]) < 0, CompareVote(rsi[i], 70) > 0 && CompareVote(Prev(rsi), rsi[i]) > 0),
                    Vote(CompareVote(stoch[i], 20) < 0 && CompareVote(stochMean[i], 20) < 0 && CompareVote(stoch[i], stochMean[i]) > 0 && CompareVote(Prev(stoch), Prev(stochMean)) < 0,
                        CompareVote(stoch[i], 80) > 0 && CompareVote(stochMean[i], 80) > 0 && CompareVote(stoch[i], stochMean[i]) < 0 && CompareVote(Prev(stoch), Prev(stochMean)) > 0),
                    Vote(CompareVote(channel[i], -100) < 0 && CompareVote(channel[i], Prev(channel)) > 0, CompareVote(channel[i], 100) > 0 && CompareVote(channel[i], Prev(channel)) < 0),
                    Vote(CompareVote(adx[i], 20) > 0 && CompareVote(Prev(plus), Prev(minus)) < 0 && CompareVote(plus[i], minus[i]) > 0, CompareVote(adx[i], 20) > 0 && CompareVote(Prev(plus), Prev(minus)) > 0 && CompareVote(plus[i], minus[i]) < 0),
                    Vote(CompareVote(awesome[i], 0) > 0 && (CompareVote(Prev(awesome), 0) < 0 || CompareVote(Prev(awesome), 0) > 0 && CompareVote(awesome[i], Prev(awesome)) > 0 && CompareVote(Prev(awesome, 2), Prev(awesome)) > 0),
                        CompareVote(awesome[i], 0) < 0 && (CompareVote(Prev(awesome), 0) > 0 || CompareVote(Prev(awesome), 0) < 0 && CompareVote(awesome[i], Prev(awesome)) < 0 && CompareVote(Prev(awesome, 2), Prev(awesome)) < 0)),
                    CompareVote(momentum[i], Prev(momentum)), CompareVote(macd[i], macdSignal[i]),
                    Vote(CompareVote(p, averages[3][i]) < 0 && CompareVote(stochasticRsi[i], 20) < 0 && CompareVote(stochasticRsiMean[i], 20) < 0 && CompareVote(stochasticRsi[i], stochasticRsiMean[i]) > 0 && CompareVote(Prev(stochasticRsi), Prev(stochasticRsiMean)) < 0,
                        CompareVote(p, averages[3][i]) > 0 && CompareVote(stochasticRsi[i], 80) > 0 && CompareVote(stochasticRsiMean[i], 80) > 0 && CompareVote(stochasticRsi[i], stochasticRsiMean[i]) < 0 && CompareVote(Prev(stochasticRsi), Prev(stochasticRsiMean)) > 0),
                    Vote(CompareVote(williams[i], -80) < 0 && CompareVote(williams[i], Prev(williams)) > 0, CompareVote(williams[i], -20) > 0 && CompareVote(williams[i], Prev(williams)) < 0),
                    Vote(CompareVote(p, averages[3][i]) > 0 && CompareVote(bear[i], 0) < 0 && CompareVote(bear[i], Prev(bear)) > 0, CompareVote(p, averages[3][i]) < 0 && CompareVote(bull[i], 0) > 0 && CompareVote(bull[i], Prev(bull)) < 0),
                    Vote(CompareVote(ultimate[i], 70) > 0, CompareVote(ultimate[i], 30) < 0)
                };
                oscillator[i] = votes.Sum()/11d;
            }
            return Outputs(("Mr", moving), ("Or", oscillator), ("Tr", moving.Select((v, i) => (v+oscillator[i])/2).ToArray()));
        });
    }
}

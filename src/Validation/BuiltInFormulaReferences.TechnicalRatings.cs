using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Builder.Specs;

namespace OoplesFinance.StockIndicators.Validation;

internal static partial class BuiltInFormulaReferences
{
    internal static IReadOnlyDictionary<string,double[]> TechnicalRatingValues(IReadOnlyList<Bar> bars, IBuiltInIndicator indicator) => TechnicalRatingsFormula(indicator)!.Compute(bars);

    private static FormulaDefinition? TechnicalRatingsFormula(IBuiltInIndicator indicator)
    {
        if (indicator.CreateOptions() is not TechnicalRatingsSpecOptions options) return null;
        var kind = AverageKind(options, 3);
        if (kind is not (1 or 2 or 3 or 6)) return null;
        return new("Tr", new[] { "Tr", "Or", "Mr" }, bars =>
        {
            var selected = indicator is IIndicator { Source: not null };
            var zero = new ReferenceFraction(0);
            ReferenceFraction D(double v) => ReferenceFraction.FromDouble(v);
            ReferenceFraction R(ReferenceFraction v) => RoundRocBankStage(v);
            ReferenceFraction[] F(IEnumerable<double> v) => v.Select(D).ToArray();
            ReferenceFraction[] Mean(ReferenceFraction[] v, int n, int k) => SmoothRocBankStage(v, Math.Max(1,n), k);
            var price = F(Closes(bars));
            // Selected input has its own component range when it leaves the candle range.
            var ranged = bars.Select((bar,i) =>
            {
                if (!selected || bar.Close >= bar.Low && bar.Close <= bar.High) return bar;
                var previous = i == 0 ? bar.Close : bars[i-1].Close;
                return new Bar(bar.Time, bar.Open, Math.Max(previous,bar.Close), Math.Min(previous,bar.Close),bar.Close,bar.Volume);
            }).ToArray();
            var rsi = F(Foundation((IBuiltInIndicator)new Rsi(options.RsiLength))!.Compute(bars)["Rsi"]);
            var directional = DirectionalIndexOutputs(ranged,14,6).ToDictionary(p=>p.Key,p=>F(p.Value));
            var typical = selected ? Closes(bars) : bars.Select(b=>((D(b.High)+D(b.Low)+D(b.Close))/D(3)).ToDouble()).ToArray();
            var channel = F(CommodityValues(typical,20,1));
            var fullHull = Mean(price,9,2); var halfHull = Mean(price,4,2);
            var hull = Mean(price.Select((_,i)=>R(D(2)*halfHull[i]-fullHull[i])).ToArray(),3,2);
            var volume = price.Select((_,i)=>
            {
                var window=Window(bars,i,20).ToArray();var mass=window.Aggregate(zero,(s,b)=>s+D(b.Volume));
                return i<19||mass.Sign==0?zero:R(window.Aggregate(zero,(s,b)=>s+D(b.Close)*D(b.Volume))/mass);
            }).ToArray();
            var averages = new[] {10,20,30,50,100,200}.Select(n=>Mean(price,n,kind)).ToArray();
            var median = selected ? price : bars.Select(b=>R((D(b.High)+D(b.Low))/D(2))).ToArray();
            var aoFast=Mean(median,options.AoLength1,1);var aoSlow=Mean(median,options.AoLength2,1);
            var awesome=price.Select((_,i)=>R(aoFast[i]-aoSlow[i])).ToArray();
            var fast=Mean(price,12,3);var slow=Mean(price,26,3);
            var macd=price.Select((_,i)=>R(fast[i]-slow[i])).ToArray();var macdSignal=Mean(macd,9,3);
            var bullBear=Mean(price,13,3);
            var bull=ranged.Select((b,i)=>R(D(b.High)-bullBear[i])).ToArray();var bear=ranged.Select((b,i)=>R(D(b.Low)-bullBear[i])).ToArray();
            var momentum=price.Select((v,i)=>i<10||price[i-10].Sign==0?zero:R(D(100)*v/price[i-10])).ToArray();
            ReferenceFraction Position(ReferenceFraction v,ReferenceFraction low,ReferenceFraction high)
            {
                if(high.CompareTo(low)==0)return zero;
                var ratio=(v-low)*D(100)/(high-low);return ratio.Sign<0?zero:ratio.CompareTo(D(100))>0?D(100):R(ratio);
            }
            ReferenceFraction[] Positions(int period) => price.Select((v,i)=>Position(v,D(Window(ranged,i,period).Min(b=>b.Low)),D(Window(ranged,i,period).Max(b=>b.High)))).ToArray();
            var stoch=Positions(options.StochLength1);var stochMean=Mean(stoch,options.StochLength2,1);
            var williams=price.Select((v,i)=> { var high=D(Window(ranged,i,14).Max(b=>b.High)); var low=D(Window(ranged,i,14).Min(b=>b.Low)); return high.CompareTo(low)==0?D(-100):R(D(100)*(v-high)/(high-low)); }).ToArray();
            var stochasticRsi=rsi.Select((v,i)=>
            {
                var window=Window(rsi,i,options.StochLength1).OrderBy(v=>v).ToArray();return Position(v,window[0],window[window.Length-1]);
            }).ToArray();
            var stochasticRsiMean=Mean(stochasticRsi,options.StochLength2,1);
            ReferenceFraction[] Midrange(int n)=>price.Select((_,i)=>R((D(Window(ranged,i,n).Max(b=>b.High))+D(Window(ranged,i,n).Min(b=>b.Low)))/D(2))).ToArray();
            var conversion=Midrange(9);var baseline=Midrange(26);var cloudB=Midrange(52);
            var cloudA=price.Select((_,i)=>R((conversion[i]+baseline[i])/D(2))).ToArray();
            var ultimate=F(UltimatePressureOutputs(bars,7,14,28));
            var moving = new double[bars.Count]; var oscillator = new double[bars.Count];
            for (var i = 0; i < bars.Count; i++)
            {
                ReferenceFraction Prev(ReferenceFraction[] values, int lag = 1) => i < lag ? zero : values[i-lag];
                int CompareVote(ReferenceFraction x, ReferenceFraction y) => x.CompareTo(y);
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
                    Vote(CompareVote(rsi[i], D(30)) < 0 && CompareVote(Prev(rsi), rsi[i]) < 0, CompareVote(rsi[i], D(70)) > 0 && CompareVote(Prev(rsi), rsi[i]) > 0),
                    Vote(CompareVote(stoch[i], D(20)) < 0 && CompareVote(stochMean[i], D(20)) < 0 && CompareVote(stoch[i], stochMean[i]) > 0 && CompareVote(Prev(stoch), Prev(stochMean)) < 0,
                        CompareVote(stoch[i], D(80)) > 0 && CompareVote(stochMean[i], D(80)) > 0 && CompareVote(stoch[i], stochMean[i]) < 0 && CompareVote(Prev(stoch), Prev(stochMean)) > 0),
                    Vote(CompareVote(channel[i], D(-100)) < 0 && CompareVote(channel[i], Prev(channel)) > 0, CompareVote(channel[i], D(100)) > 0 && CompareVote(channel[i], Prev(channel)) < 0),
                    Vote(CompareVote(adx[i], D(20)) > 0 && CompareVote(Prev(plus), Prev(minus)) < 0 && CompareVote(plus[i], minus[i]) > 0, CompareVote(adx[i], D(20)) > 0 && CompareVote(Prev(plus), Prev(minus)) > 0 && CompareVote(plus[i], minus[i]) < 0),
                    Vote(CompareVote(awesome[i], D(0)) > 0 && (CompareVote(Prev(awesome), D(0)) < 0 || CompareVote(Prev(awesome), D(0)) > 0 && CompareVote(awesome[i], Prev(awesome)) > 0 && CompareVote(Prev(awesome, 2), Prev(awesome)) > 0),
                        CompareVote(awesome[i], D(0)) < 0 && (CompareVote(Prev(awesome), D(0)) > 0 || CompareVote(Prev(awesome), D(0)) < 0 && CompareVote(awesome[i], Prev(awesome)) < 0 && CompareVote(Prev(awesome, 2), Prev(awesome)) < 0)),
                    CompareVote(momentum[i], Prev(momentum)), CompareVote(macd[i], macdSignal[i]),
                    Vote(CompareVote(p, averages[3][i]) < 0 && CompareVote(stochasticRsi[i], D(20)) < 0 && CompareVote(stochasticRsiMean[i], D(20)) < 0 && CompareVote(stochasticRsi[i], stochasticRsiMean[i]) > 0 && CompareVote(Prev(stochasticRsi), Prev(stochasticRsiMean)) < 0,
                        CompareVote(p, averages[3][i]) > 0 && CompareVote(stochasticRsi[i], D(80)) > 0 && CompareVote(stochasticRsiMean[i], D(80)) > 0 && CompareVote(stochasticRsi[i], stochasticRsiMean[i]) < 0 && CompareVote(Prev(stochasticRsi), Prev(stochasticRsiMean)) > 0),
                    Vote(CompareVote(williams[i], D(-80)) < 0 && CompareVote(williams[i], Prev(williams)) > 0, CompareVote(williams[i], D(-20)) > 0 && CompareVote(williams[i], Prev(williams)) < 0),
                    Vote(CompareVote(p, averages[3][i]) > 0 && CompareVote(bear[i], D(0)) < 0 && CompareVote(bear[i], Prev(bear)) > 0, CompareVote(p, averages[3][i]) < 0 && CompareVote(bull[i], D(0)) > 0 && CompareVote(bull[i], Prev(bull)) < 0),
                    Vote(CompareVote(ultimate[i], D(70)) > 0, CompareVote(ultimate[i], D(30)) < 0)
                };
                oscillator[i] = votes.Sum()/11d;
            }
            return Outputs(("Mr", moving), ("Or", oscillator), ("Tr", moving.Select((v, i) => (v+oscillator[i])/2).ToArray()));
        });
    }
}

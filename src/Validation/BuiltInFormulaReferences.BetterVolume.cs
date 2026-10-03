using OoplesFinance.StockIndicators.Indicators;
namespace OoplesFinance.StockIndicators.Validation;
internal static partial class BuiltInFormulaReferences
{
    internal static IReadOnlyDictionary<string, double[]> BetterVolumeOutputs(IReadOnlyList<Bar> bars, IBuiltInIndicator indicator)
    { var options = indicator.CreateOptions(); return BetterVolumeValues(bars, Integer(options, "Length", 8), Integer(options, "LbLength", 2)).Outputs; }
    internal static (Dictionary<string, double[]> Outputs, Signal[] Signals) BetterVolumeValues(IReadOnlyList<Bar> bars, int length, int lookback)
    {
        length = Math.Max(1, length); lookback = Math.Max(1, lookback); ReferenceFraction F(double x) => ReferenceFraction.FromDouble(x);
        ReferenceFraction R(ReferenceFraction x) => x.RoundExtendedBinary64(); ReferenceFraction Ratio(ReferenceFraction n, ReferenceFraction d) => d.Sign == 0 ? F(0) : R(n / d);
        var metric = Enumerable.Range(0, 22).Select(_ => new ReferenceFraction[bars.Count]).ToArray(); var up = new ReferenceFraction[bars.Count]; var down = new ReferenceFraction[bars.Count]; var output = new double[bars.Count]; var signals = new Signal[bars.Count];
        for (var i = 0; i < bars.Count; i++)
        {
            var b = bars[i]; var previousClose = F(i == 0 ? b.Close : bars[i - 1].Close); var previousOpen = F(i == 0 ? 0 : bars[i - 1].Open);
            var range = new[] { F(b.High) - F(b.Low), (F(b.High) - previousClose).Abs(), (F(b.Low) - previousClose).Abs() }.Aggregate((a, z) => a.CompareTo(z) > 0 ? a : z);
            var recent = Window(bars, i, lookback).ToArray(); var span = F(recent.Max(v => v.High)) - F(recent.Min(v => v.Low)); var body = F(b.Close) - F(b.Open); var volume = F(b.Volume);
            up[i] = body.Sign == 0 ? R(volume / F(2)) : body.Sign > 0 ? Ratio(volume * range, F(2) * range - body) : Ratio(volume * (range + body), F(2) * range + body);
            down[i] = R(volume - up[i]); output[i] = up[i].ToDouble(); var pairUp = up[i] + (i == 0 ? F(0) : up[i - 1]); var pairDown = down[i] + (i == 0 ? F(0) : down[i - 1]);
            metric[4][i] = R(up[i] * range); metric[5][i] = R((up[i] - down[i]) * range); metric[6][i] = R(down[i] * range); metric[7][i] = R((down[i] - up[i]) * range);
            metric[8][i] = Ratio(up[i], range); metric[9][i] = Ratio(up[i] - down[i], range); metric[10][i] = Ratio(down[i], range); metric[11][i] = Ratio(down[i] - up[i], range);
            metric[14][i] = R(pairUp * span); metric[15][i] = R((pairUp - pairDown) * span); metric[16][i] = R(pairDown * span); metric[17][i] = R((pairDown - pairUp) * span);
            metric[18][i] = Ratio(pairUp, span); metric[19][i] = Ratio(pairUp - pairDown, span); metric[20][i] = Ratio(pairDown, span); metric[21][i] = Ratio(pairDown - pairUp, span);
            bool Maximum(int slot) => Window(metric[slot], i, length).All(v => metric[slot][i].CompareTo(v) >= 0);
            bool Minimum(int slot) => Window(metric[slot], i, length).All(v => metric[slot][i].CompareTo(v) <= 0);
            var rising = body.Sign > 0; var falling = body.Sign < 0; var pr = previousClose.CompareTo(previousOpen) > 0; var pf = previousClose.CompareTo(previousOpen) < 0;
            var buy = rising && (Maximum(4) || Maximum(5) || Minimum(10) || Minimum(11) || pr && (Maximum(14) || Minimum(20) || Minimum(21)) || pf && Maximum(15));
            var sell = falling && (Maximum(6) || Maximum(7) || Minimum(8) || Minimum(9) || pf && (Minimum(16) || Minimum(17) || Minimum(18))) || rising && pf && Minimum(19);
            signals[i] = buy ? Signal.Buy : sell ? Signal.Sell : Signal.None;
        }
        return (new Dictionary<string, double[]> { ["Bvi"] = output }, signals);
    }
}

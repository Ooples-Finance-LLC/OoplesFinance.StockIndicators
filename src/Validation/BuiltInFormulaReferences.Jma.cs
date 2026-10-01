using OoplesFinance.StockIndicators.Indicators;
namespace OoplesFinance.StockIndicators.Validation;
internal static partial class BuiltInFormulaReferences
{
    internal static IReadOnlyDictionary<string, double[]> JmaOutputs(IReadOnlyList<Bar> bars, IBuiltInIndicator indicator) =>
        JmaValues(bars, Integer(indicator.CreateOptions(), "Length", 7), 50, 2).Outputs;
    internal static (Dictionary<string, double[]> Outputs, Signal[] Signals) JmaValues(IReadOnlyList<Bar> bars, int length, double phase, double power)
    {
        length = Math.Max(1, length); var ratio = .45 * (length - 1L); var beta = ratio / (ratio + 2);
        var alpha = power == 2 ? beta * beta : power == 3 ? beta * beta * beta : Math.Pow(beta, power);
        ReferenceFraction R(double x) => ReferenceFraction.FromDouble(x);
        ReferenceFraction Round(ReferenceFraction x) => x.RoundExtendedBinary64();
        var pole = R(alpha); var residualPole = R(beta); var drive = R(1 - alpha); var residualDrive = R(1 - beta);
        var phaseWeight = R(Math.Max(-100, Math.Min(100, phase)) / 100 + 1.5);
        var correctionDrive = Round(drive * drive); var correctionMemory = Round(pole * pole);
        var count = bars.Count; var level = new ReferenceFraction[count]; var residual = new ReferenceFraction[count];
        var increment = new ReferenceFraction[count]; var output = new ReferenceFraction[count]; var margin = new ReferenceFraction[count]; var trades = new Signal[count];
        var zero = R(0);
        ReferenceFraction Previous(ReferenceFraction[] values, int i) => i == 0 ? zero : values[i - 1];
        for (var i = 0; i < count; i++)
        {
            var price = R(bars[i].Close);
            level[i] = Round(price * drive + Previous(level, i) * pole);
            residual[i] = Round(price * residualDrive - level[i] * residualDrive + Previous(residual, i) * residualPole);
            var target = level[i] + phaseWeight * residual[i];
            increment[i] = Round(target * correctionDrive - Previous(output, i) * correctionDrive + Previous(increment, i) * correctionMemory);
            output[i] = pole.Sign == 0 ? price : Round(Previous(output, i) + increment[i]); margin[i] = price - output[i];
            var prior = Previous(margin, i);
            trades[i] = margin[i].Sign > 0 && margin[i].CompareTo(prior) > 0 ? Signal.StrongBuy : margin[i].Sign < 0 && margin[i].CompareTo(prior) < 0 ? Signal.StrongSell
                : margin[i].Sign > 0 ? Signal.Buy : margin[i].Sign < 0 ? Signal.Sell : Signal.None;
        }
        return (new Dictionary<string, double[]> { ["Jma"] = output.Select(v => v.ToDouble()).ToArray() }, trades);
    }
}

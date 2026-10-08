using OoplesFinance.StockIndicators.Indicators;
namespace OoplesFinance.StockIndicators.Validation;
internal static partial class BuiltInFormulaReferences
{
    internal static Dictionary<string, double[]> ChebyshevWaveValues(IReadOnlyList<Bar> bars, string? selected = null)
    {
        double[][] sections = { new[] {1.907,.293,.063,.513,.451,.481}, new[] {1.777,.731,.166,.977,1.008,.561}, new[] {1.572,1.026,.282,.356,1.329,.644}, new[] {1.192,1.281,.426,-.384,1.565,.729}, new[] {.681,1.46,.543,-.966,1.703,.793}, new[] {.012,1.606,.65,-1.408,1.801,.848}, new[] {-.669,1.716,.74,-1.685,1.866,.89}, new[] {-1.226,1.8,.811,-1.842,1.91,.922}, new[] {-1.659,1.873,.878,-1.957,1.946,.951} };
        var outputs = new Dictionary<string, double[]>();
        ReferenceFraction R(ReferenceFraction value) => RoundRocBankStage(value);
        for (var w = 0; w < sections.Length; w++)
        {
            if (selected is not null && selected != "Eclpf"+(w-2)) continue;
            var c = sections[w]; var gain = ReferenceFraction.FromDouble((1-c[1]+c[2])*(1-c[4]+c[5])/((2+c[0])*(2+c[3])));
            var k = c.Select(ReferenceFraction.FromDouble).ToArray(); var first = new ReferenceFraction[bars.Count]; var second = new ReferenceFraction[bars.Count];
            ReferenceFraction Price(int index) => index < 0 ? new(0) : ReferenceFraction.FromDouble(bars[index].Close);
            ReferenceFraction At(ReferenceFraction[] values, int index) => index < 0 ? new(0) : values[index];
            for (var i = 0; i < bars.Count; i++)
            {
                var forcing = R(R(Price(i) + R(k[0]*Price(i-1))) + Price(i-2));
                first[i] = R(R(R(gain*forcing) + R(k[1]*At(first,i-1))) - R(k[2]*At(first,i-2)));
                second[i] = R(R(R(R(first[i] + R(k[3]*At(first,i-1))) + At(first,i-2)) + R(k[4]*At(second,i-1))) - R(k[5]*At(second,i-2)));
            }
            outputs["Eclpf"+(w-2)] = second.Select(v => v.ToDouble()).ToArray();
        }
        return outputs;
    }
}

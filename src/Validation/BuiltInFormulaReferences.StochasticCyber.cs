using OoplesFinance.StockIndicators.Indicators;
namespace OoplesFinance.StockIndicators.Validation;
internal static partial class BuiltInFormulaReferences
{
    internal static Dictionary<string,double[]> StochasticCyberValues(IReadOnlyList<Bar> bars, int length, double alpha)
    {
        length = Math.Max(2, length); var cycle = CyberCycleStages(bars, alpha); var raw = new double[bars.Count]; var line = new double[bars.Count]; var signal = new double[bars.Count];
        ReferenceFraction F(double v) => ReferenceFraction.FromDouble(v);
        ReferenceFraction R(ReferenceFraction v) => ReferenceFraction.FromDouble(v.ToDouble());
        for (var i = 0; i < bars.Count; i++)
        {
            var low = cycle[i]; var high = cycle[i];
            for (var j = Math.Max(0, i-length+1); j < i; j++) { if (cycle[j].CompareTo(low)<0) low=cycle[j]; if(cycle[j].CompareTo(high)>0) high=cycle[j]; }
            raw[i] = high.CompareTo(low)==0 ? 0 : ((cycle[i]-low)/(high-low)).ToDouble();
            var sum = new ReferenceFraction(0);
            for(var lag=0;lag<4;lag++) sum=R(sum+R(F(i<lag?0:raw[i-lag])*new ReferenceFraction(4-lag)));
            line[i] = Math.Max(-1,Math.Min(1,R(R(R(sum/new ReferenceFraction(10))-F(.5))*new ReferenceFraction(2)).ToDouble()));
            signal[i] = Math.Max(-1,Math.Min(1,R(F(.96)*R(F(i==0?0:line[i-1])+F(.02))).ToDouble()));
        }
        return new() { { "Escc",line }, { "Signal",signal } };
    }
}

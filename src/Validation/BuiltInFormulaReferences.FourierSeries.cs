using OoplesFinance.StockIndicators.Indicators;
namespace OoplesFinance.StockIndicators.Validation;
internal static partial class BuiltInFormulaReferences
{
    internal static IReadOnlyDictionary<string,double[]> FourierSeriesOutputs(IReadOnlyList<Bar> bars,IBuiltInIndicator indicator)
    {var o=indicator.CreateOptions();return FourierSeriesOutputs(bars,Integer(o,"Length",20),Number(o,.1,"Bw"));}
    internal static IReadOnlyDictionary<string,double[]> FourierSeriesOutputs(IReadOnlyList<Bar> bars,int period,double bandwidth,ICollection<Signal>? signals=null)
    {
        ReferenceFraction R(double v)=>ReferenceFraction.FromDouble(v);ReferenceFraction Round(ReferenceFraction v)=>v.RoundExtendedBinary64();
        var bands=new List<ReferenceFraction[]>();var powers=new List<ReferenceFraction[]>();
        for(var harmonic=1;harmonic<=3;harmonic++)
        {
            var cycle=(double)period/harmonic;var angle=Math.Min(Math.PI/2,Math.Abs(bandwidth)*2*Math.PI/cycle);var pole=cycle<=2?1:Math.Cos(angle)/(1+Math.Sin(angle));var drive=R(.5*(1-pole));var feedback=R(Math.Cos(2*Math.PI/cycle)*(1+pole));var qFactor=R(period/(2*harmonic*Math.PI));
            var band=new ReferenceFraction[bars.Count];var energy=new ReferenceFraction[bars.Count];var power=new ReferenceFraction[bars.Count];
            for(var i=0;i<bars.Count;i++)
            {
                band[i]=i<4?R(0):Round(drive*(R(bars[i].Close)-R(bars[i-2].Close))+feedback*band[i-1]-R(pole)*band[i-2]);var q=i<5?R(0):Round(qFactor*(band[i]-band[i-1]));energy[i]=band[i]*band[i]+q*q;
                power[i]=energy.Skip(Math.Max(0,i-period+1)).Take(Math.Min(period,i+1)).Aggregate(R(0),(sum,v)=>sum+v);
            }
            bands.Add(band);powers.Add(power);
        }
        var wave=new ReferenceFraction[bars.Count];var roc=new double[bars.Count];
        for(var i=0;i<bars.Count;i++)
        {
            var sum=VolatilityScoreOracle.Of(bands[0][i]);
            if(powers[0][i].Sign==0)sum=VolatilityScoreOracle.Zero;
            else for(var k=1;k<3;k++)if(powers[k][i].Sign!=0)sum=sum.Plus(VolatilityScoreOracle.DivideRoot(bands[k][i],powers[0][i]/powers[k][i]));
            wave[i]=sum.RoundExtended();roc[i]=Round(R(period/(4*Math.PI))*(wave[i]-(i<2?R(0):wave[i-2]))).ToDouble();var previous=i==0?R(0):wave[i-1];signals?.Add(wave[i].Sign>0&&wave[i].CompareTo(previous)>0?Signal.StrongBuy:wave[i].Sign<0&&wave[i].CompareTo(previous)<0?Signal.StrongSell:wave[i].Sign>0?Signal.Buy:wave[i].Sign<0?Signal.Sell:Signal.None);
        }
        return Outputs(("Wave",wave.Select(v=>v.ToDouble()).ToArray()),("Roc",roc));
    }
}

using OoplesFinance.StockIndicators.Indicators;
using R=OoplesFinance.StockIndicators.Validation.ReferenceFraction;
namespace OoplesFinance.StockIndicators.Validation;
internal static partial class BuiltInFormulaReferences
{
    internal static IReadOnlyDictionary<string,double[]> MesaPredictV2Outputs(IReadOnlyList<Bar> bars,IBuiltInIndicator indicator)
    {var o=indicator.CreateOptions();return MesaPredictV2Outputs(bars,Integer(o,"Length1",5),Integer(o,"Length2",135),Integer(o,"Length3",12),Integer(o,"Length4",4),o.GetType().GetProperty("MaType")?.GetValue(o) is MovingAvgType.EhlersHannMovingAverage);}
    internal static IReadOnlyDictionary<string,double[]> MesaPredictV2Outputs(IReadOnlyList<Bar> bars,int history,int high,int smooth,int horizon,bool hann,ICollection<Signal>? signals=null)
    {
        history=Math.Max(1,history);high=Math.Max(1,high);smooth=Math.Max(1,smooth);horizon=Math.Min(history,Math.Max(1,horizon));R D(double value)=>R.FromDouble(value);R Round(R value)=>value.RoundExtendedBinary64();
        var hp=new R[bars.Count];var ssf=new R[bars.Count];var filtered=new R[bars.Count];var predict=new R[bars.Count];var extrap=new R[bars.Count];R At(R[] values,int i)=>i<0?new R(0):values[i];
        (R Gain,R Feedback,R Decay) Coefficients(int period,bool highPass){var angle=Math.Max(.01,Math.Min(.99,1.414*Math.PI/period));var radius=Math.Exp(-angle);var feedback=2*radius*Math.Cos(angle);var decay=-radius*radius;return(D(highPass?(1+feedback-decay)/4:1-feedback-decay),D(feedback),D(decay));}
        var hi=Coefficients(high,true);var lo=Coefficients(smooth,false);var coefficients=new[]{4.525,-8.45,8.145,-4.045,.825}.Select(D).ToArray();
        for(var i=0;i<bars.Count;i++)
        {
            hp[i]=i<4?new R(0):Round(hi.Gain*(D(bars[i].Close)-new R(2)*D(bars[i-1].Close)+D(bars[i-2].Close))+hi.Feedback*At(hp,i-1)+hi.Decay*At(hp,i-2));
            ssf[i]=i<3?hp[i]:Round(lo.Gain*(hp[i]+At(hp,i-1))/new R(2)+lo.Feedback*At(ssf,i-1)+lo.Decay*At(ssf,i-2));
            var total=new R(0);for(var lag=0;lag<Math.Min((long)smooth,i+1L);lag++){var sine=Math.Sin(Math.PI*((lag+1d)/(smooth+1d)));var weight=hann?D(2*sine*sine):new R(smooth-lag);total+=weight*ssf[i-lag];}filtered[i]=Round(total/(hann?new R(smooth+1L):new R(smooth)*new R(smooth+1L)/new R(2)));
            var trajectory=new List<R>();for(var lag=4;lag>=0;lag--)trajectory.Add(lag<history?At(filtered,i-lag):new R(0));var linearPrevious=history<2?new R(0):At(filtered,i-1);var linear=filtered[i];
            if(trajectory.Any(v=>v.Sign!=0))for(long step=0;step<horizon;step++)
            {
                var next=new R(0);for(var k=0;k<5;k++)next+=coefficients[k]*trajectory[4-k];trajectory.RemoveAt(0);trajectory.Add(Round(next));var nextLinear=Round(new R(2)*linear-linearPrevious);linearPrevious=linear;linear=nextLinear;
            }
            predict[i]=trajectory[4];extrap[i]=linear;var slope=predict[i]-At(predict,i-1);var old=At(predict,i-1)-At(predict,i-2);signals?.Add(slope.Sign>0&&slope.CompareTo(old)>0?Signal.StrongBuy:slope.Sign<0&&slope.CompareTo(old)<0?Signal.StrongSell:slope.Sign>0?Signal.Buy:slope.Sign<0?Signal.Sell:Signal.None);
        }
        return Outputs(("Ssf",filtered.Select(v=>v.ToDouble()).ToArray()),("Predict",predict.Select(v=>v.ToDouble()).ToArray()),("Extrap",extrap.Select(v=>v.ToDouble()).ToArray()));
    }
}

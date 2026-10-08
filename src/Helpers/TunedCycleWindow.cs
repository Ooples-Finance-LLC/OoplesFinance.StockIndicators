using System.Numerics;
namespace OoplesFinance.StockIndicators.Helpers;
internal sealed class TunedCycleWindow
{
    private readonly int _minimum,_maximum,_medianLength;private readonly double _pole;private readonly Queue<BigInteger> _hp=new(),_smooth=new();private readonly Queue<double> _cycles=new();private Dictionary<int,(BigInteger Real,BigInteger Real2,BigInteger Imag,BigInteger Imag2)> _bands=new();private BigInteger _previous;private long _index;
    internal TunedCycleWindow(int minimum,int maximum,int cutoff,int median){_minimum=Math.Max(3,minimum);_maximum=Math.Max(_minimum,maximum);_medianLength=Math.Max(1,median);_pole=Math.Tan(Math.PI/4-Math.PI/Math.Max(3,cutoff));}
    private static BigInteger U(double value)=>ExactVarianceWindow.Units(value);
    private static BigInteger Round(BigInteger n,BigInteger d)=>RocBankValue.RoundUnits(n,d);
    internal double Next(double price,bool final)
    {
        var unit=BigInteger.One<<1074;var value=U(price)<<1074;var hpHistory=_hp.ToArray();var smoothHistory=_smooth.ToArray();BigInteger H(int lag)=>lag<=hpHistory.Length?hpHistory[hpHistory.Length-lag]:BigInteger.Zero;BigInteger S(int lag)=>lag<=smoothHistory.Length?smoothHistory[smoothHistory.Length-lag]:BigInteger.Zero;
        var hp=_index<7?value:Round(U(.5*(1+_pole))*(value-_previous)+U(_pole)*H(1),unit);var smooth=_index<7?value-_previous:Round(hp+2*H(1)+3*H(2)+3*H(3)+2*H(4)+H(5),12);
        var delta=Math.Max(.5-.015*_index,.15);var bands=new Dictionary<int,(BigInteger Real,BigInteger Real2,BigInteger Imag,BigInteger Imag2)>();var powers=new List<(int Period,BigInteger Power)>();BigInteger peak=0;
        for(long period=_minimum;period<=_maximum;period++)
        {
            var old=_bands.TryGetValue((int)period,out var prior)?prior:default;var width=4*Math.PI*delta/period;var alpha=Math.Cos(width)/(1+Math.Abs(Math.Sin(width)));var gain=U(.5*(1-alpha));var feedback=U(Math.Cos(2*Math.PI/period)*(1+alpha));var decay=U(alpha);var scale=U(period/(2*Math.PI));
            var quadrature=Round(scale*(smooth-S(1)),unit);var previousQuadrature=Round(scale*(S(2)-S(3)),unit);
            var real=Round(gain*(smooth-S(2))+feedback*old.Real-decay*old.Real2,unit);var imaginary=Round(gain*(quadrature-previousQuadrature)+feedback*old.Imag-decay*old.Imag2,unit);var power=real*real+imaginary*imaginary;
            powers.Add(((int)period,power));peak=BigInteger.Max(peak,power);bands.Add((int)period,(real,old.Real,imaginary,old.Imag));
        }
        var numerator=new ExactMeanAccumulator();var denominator=new ExactMeanAccumulator();if(!peak.IsZero)foreach(var bin in powers){var ratio=ExactMeanAccumulator.UnitRatio((100*peak-99*bin.Power)<<1074,peak);var db=10*Math.Log10(ratio);if(db<=3){var weight=_maximum-db;numerator.Add(weight,bin.Period);denominator.Add(weight);}}
        var cycle=denominator.IsExactlyZero?_minimum:Math.Max(_minimum,Math.Min(_maximum,numerator.Ratio(denominator)));var kept=_cycles.Count==_medianLength?_cycles.Skip(1):_cycles;var sorted=kept.Concat(new[] { cycle }).OrderBy(v=>v).ToArray();var mean=new ExactMeanAccumulator();mean.Add(sorted[(sorted.Length-1)/2]);mean.Add(sorted[sorted.Length/2]);var result=mean.Mean(2);
        if(final){if(_hp.Count==5)_hp.Dequeue();_hp.Enqueue(hp);if(_smooth.Count==3)_smooth.Dequeue();_smooth.Enqueue(smooth);if(_cycles.Count==_medianLength)_cycles.Dequeue();_cycles.Enqueue(cycle);_bands=bands;_previous=value;_index++;}return result;
    }
    internal void Reset(){_hp.Clear();_smooth.Clear();_cycles.Clear();_bands.Clear();_previous=default;_index=0;}
}

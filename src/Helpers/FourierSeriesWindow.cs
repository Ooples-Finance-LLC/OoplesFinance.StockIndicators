using System.Numerics;
using OoplesFinance.StockIndicators.Streaming;
using Number=OoplesFinance.StockIndicators.Helpers.PeriodicChannelWindow.Number;
using Radical=OoplesFinance.StockIndicators.Helpers.VolatilityAverageWindow.Radical;
namespace OoplesFinance.StockIndicators.Helpers;
internal sealed class FourierSeriesWindow : IDisposable
{
    private readonly int _period;private readonly double[] _drive=new double[3],_feedback=new double[3],_pole=new double[3],_quadrature=new double[3];private readonly double _rocFactor;
    private readonly BigInteger[] _last=new BigInteger[3],_prior=new BigInteger[3],_powers=new BigInteger[3];
    private readonly Queue<BigInteger[]> _energy=new();private readonly Queue<BigInteger> _prices=new(),_waves=new();private long _count;
    internal FourierSeriesWindow(int period,double bandwidth)
    {
        StreamingInputValidation.Finite(bandwidth,nameof(bandwidth));_period=Math.Max(1,period);_rocFactor=_period/(4*Math.PI);
        for(var k=0;k<3;k++){var cycle=(double)_period/(k+1);var pole=FourierHarmonicPole.For(cycle,bandwidth);_pole[k]=pole;_drive[k]=.5*(1-pole);_feedback[k]=Math.Cos(2*Math.PI/cycle)*(1+pole);_quadrature[k]=_period/(2*(k+1)*Math.PI);}
    }
    private static BigInteger U(double value)=>ExactVarianceWindow.Units(value);
    private static BigInteger Product(BigInteger units,double coefficient)=>RocBankValue.RoundUnits(units*U(coefficient),BigInteger.One<<1074);
    private static BigInteger Round(Radical value)
    {
        for(var shift=0;;shift+=512){var rounded=value.Times(Number.Integer(1).Divide(Number.Integer(BigInteger.One<<shift))).Publish();if(!double.IsInfinity(rounded))return U(rounded)<<shift;}
    }
    internal (double Wave,double Roc,Signal Trade) Next(double price,bool final)
    {
        StreamingInputValidation.Finite(price,nameof(price));var units=U(price);var difference=units-(_prices.Count==2?_prices.Peek():BigInteger.Zero);var bp=new BigInteger[3];var energy=new BigInteger[3];var power=new BigInteger[3];
        for(var k=0;k<3;k++)
        {
            var numerator=U(_drive[k])*difference+U(_feedback[k])*_last[k]-U(_pole[k])*_prior[k];bp[k]=_count<4?BigInteger.Zero:RocBankValue.RoundUnits(numerator,BigInteger.One<<1074);
            var q=_count<5?BigInteger.Zero:Product(bp[k]-_last[k],_quadrature[k]);energy[k]=bp[k]*bp[k]+q*q;power[k]=_powers[k]+energy[k]-(_energy.Count==_period?_energy.Peek()[k]:BigInteger.Zero);
        }
        var wave=BigInteger.Zero;
        if(!power[0].IsZero)
        {
            var total=Radical.Of(Number.Integer(bp[0]).Divide(Number.Integer(BigInteger.One<<1074)));
            for(var k=1;k<3;k++)if(!power[k].IsZero)total+=Radical.QuotientRoot(Number.Integer(bp[k]).Divide(Number.Integer(BigInteger.One<<1074)),Number.Integer(power[0]).Divide(Number.Integer(power[k])));
            wave=Round(total);
        }
        var previousWave=_waves.Count==0?BigInteger.Zero:_waves.Last();var priorWave=_waves.Count==2?_waves.Peek():BigInteger.Zero;var roc=Product(wave-priorWave,_rocFactor);
        var trade=wave.Sign>0&&wave>previousWave?Signal.StrongBuy:wave.Sign<0&&wave<previousWave?Signal.StrongSell:wave.Sign>0?Signal.Buy:wave.Sign<0?Signal.Sell:Signal.None;
        if(final){for(var k=0;k<3;k++){_prior[k]=_last[k];_last[k]=bp[k];_powers[k]=power[k];}if(_energy.Count==_period)_energy.Dequeue();_energy.Enqueue(energy);if(_prices.Count==2)_prices.Dequeue();_prices.Enqueue(units);if(_waves.Count==2)_waves.Dequeue();_waves.Enqueue(wave);_count++;}
        return(ExactMeanAccumulator.UnitRatio(wave,BigInteger.One),ExactMeanAccumulator.UnitRatio(roc,BigInteger.One),trade);
    }
    internal void Reset(){Array.Clear(_last,0,3);Array.Clear(_prior,0,3);Array.Clear(_powers,0,3);_energy.Clear();_prices.Clear();_waves.Clear();_count=0;}
    public void Dispose()=>Reset();
    internal static (Dictionary<string,List<double>> Outputs,List<Signal> Signals) Calculate(StockData data,int period,double bandwidth)
    {
        var(input,_,_,_,_)=CalculationsHelper.GetInputValuesList(data);foreach(var values in new[]{input,data.OpenPrices,data.HighPrices,data.LowPrices,data.ClosePrices,data.Volumes})foreach(var value in values)StreamingInputValidation.Finite(value,nameof(data));
        using var state=new FourierSeriesWindow(period,bandwidth);var output=new Dictionary<string,List<double>>{{"Wave",new()},{"Roc",new()}};var signals=new List<Signal>(input.Count);
        foreach(var price in input){var point=state.Next(price,true);output["Wave"].Add(point.Wave);output["Roc"].Add(point.Roc);signals.Add(point.Trade);}return(output,signals);
    }
}

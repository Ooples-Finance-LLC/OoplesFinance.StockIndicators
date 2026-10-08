using System.Numerics;
using OoplesFinance.StockIndicators.Streaming;
using Number=OoplesFinance.StockIndicators.Helpers.PeriodicChannelWindow.Number;
using Radical=OoplesFinance.StockIndicators.Helpers.VolatilityAverageWindow.Radical;
namespace OoplesFinance.StockIndicators.Helpers;

internal sealed class VolumeFlowWindow : IDisposable
{
    private readonly int _flowPeriod,_variancePeriod;
    private readonly double _coefficient,_volumeCoefficient;
    private readonly RocBankAverage _volume,_line,_signal;
    private readonly Queue<BigInteger> _returns=new(),_flows=new();
    private BigInteger _sum,_squares,_flowSum,_previousAverage,_previousDifference;
    private double _previousPrice;private bool _hasPrevious;
    internal VolumeFlowWindow(MovingAvgType kind,int flow,int variance,int signal,int smooth,double coefficient,double volumeCoefficient)
    {
        StreamingInputValidation.Finite(coefficient,nameof(coefficient));StreamingInputValidation.Finite(volumeCoefficient,nameof(volumeCoefficient));
        _flowPeriod=Math.Max(1,flow);_variancePeriod=Math.Max(1,variance);_coefficient=coefficient;_volumeCoefficient=volumeCoefficient;
        _volume=new(kind,flow,1,true);_line=new(kind,smooth,1,true);_signal=new(MovingAvgType.ExponentialMovingAverage,signal,1,true);
    }
    private static BigInteger U(double value)=>ExactVarianceWindow.Units(value);
    private static BigInteger U(RocBankValue value)=>U(value.Mantissa)<<value.UpperShift;
    private static RocBankValue Value(BigInteger units)
    {
        for(var shift=0;;shift+=512){var value=ExactMeanAccumulator.UnitRatio(units,BigInteger.One<<shift);if(!double.IsInfinity(value))return new(value,shift);}
    }
    internal (double Line,double Signal,double Histogram,Signal Trade) Next(double price,double close,double volume,bool final)
    {
        StreamingInputValidation.Finite(price,nameof(price));StreamingInputValidation.Finite(close,nameof(close));StreamingInputValidation.Finite(volume,nameof(volume));
        var log=U(_hasPrevious?StableLogRatio.Of(price,_previousPrice):0);var expired=_returns.Count==_variancePeriod?_returns.Peek():BigInteger.Zero;
        var sum=_sum+log-expired;var squares=_squares+log*log-expired*expired;var variance=_returns.Count>=_variancePeriod-1
            ?Number.Integer(_variancePeriod*squares-sum*sum).Divide(Number.Integer(new BigInteger((long)_variancePeriod*_variancePeriod)<<2148)):default;
        var threshold=Radical.QuotientRoot(variance*Number.Of(close)*Number.Of(_coefficient),variance);
        var change=_hasPrevious?Number.Of(price)-Number.Of(_previousPrice):default;
        var average=U(_volume.Next(new(volume),final));var cap=RocBankValue.RoundUnits(_previousAverage*U(_volumeCoefficient),BigInteger.One<<1074);var capped=BigInteger.Min(U(volume),cap);
        var flow=threshold.Compare(change)<0?capped:threshold.Times(Number.Integer(-1)).Compare(change)>0?-capped:BigInteger.Zero;
        var flowSum=_flowSum+flow-(_flows.Count==_flowPeriod?_flows.Peek():BigInteger.Zero);
        var normalized=average.IsZero?BigInteger.Zero:RocBankValue.RoundUnits((flowSum*average.Sign)<<1074,BigInteger.Abs(average));
        var line=_line.Next(Value(normalized),final);var signal=_signal.Next(line,final);var difference=U(line)-U(signal);
        var histogram=Value(difference);var roundedDifference=U(histogram);
        var trade=roundedDifference.Sign>0 && roundedDifference>_previousDifference?Signal.StrongBuy:roundedDifference.Sign<0 && roundedDifference<_previousDifference?Signal.StrongSell:roundedDifference.Sign>0?Signal.Buy:roundedDifference.Sign<0?Signal.Sell:Signal.None;
        if(final){if(_returns.Count==_variancePeriod)_returns.Dequeue();_returns.Enqueue(log);if(_flows.Count==_flowPeriod)_flows.Dequeue();_flows.Enqueue(flow);_sum=sum;_squares=squares;_flowSum=flowSum;_previousPrice=price;_previousAverage=average;_previousDifference=roundedDifference;_hasPrevious=true;}
        return(line.Publish(),signal.Publish(),histogram.Publish(),trade);
    }
    internal void Reset(){_volume.Reset();_line.Reset();_signal.Reset();_returns.Clear();_flows.Clear();_sum=_squares=_flowSum=_previousAverage=_previousDifference=default;_previousPrice=0;_hasPrevious=false;}
    public void Dispose(){_volume.Dispose();_line.Dispose();_signal.Dispose();Reset();}
    internal static (Dictionary<string,List<double>> Outputs,List<Signal> Signals) Calculate(StockData data,MovingAvgType kind,int flow,int variance,int signal,int smooth,double coefficient,double volumeCoefficient)
    {
        var(input,high,low,open,close,volume)=CalculationsHelper.GetInputValuesList(InputName.TypicalPrice,data);
        foreach(var values in new[]{input,high,low,open,close,volume})foreach(var value in values)StreamingInputValidation.Finite(value,nameof(data));
        using var state=new VolumeFlowWindow(kind,flow,variance,signal,smooth,coefficient,volumeCoefficient);var outputs=new Dictionary<string,List<double>>{{"Vfi",new()},{"Signal",new()},{"Histogram",new()}};var signals=new List<Signal>(input.Count);
        for(var i=0;i<input.Count;i++){var typical=new ExactMeanAccumulator();typical.Add(high[i]);typical.Add(low[i]);typical.Add(close[i]);var point=state.Next(data.ChainedValues is { Count: > 0 }?input[i]:typical.Mean(3),close[i],volume[i],true);outputs["Vfi"].Add(point.Line);outputs["Signal"].Add(point.Signal);outputs["Histogram"].Add(point.Histogram);signals.Add(point.Trade);}return(outputs,signals);
    }
}

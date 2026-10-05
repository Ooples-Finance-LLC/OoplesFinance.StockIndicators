using System.Numerics;
using OoplesFinance.StockIndicators.Streaming;
using Number=OoplesFinance.StockIndicators.Helpers.PeriodicChannelWindow.Number;
using Radical=OoplesFinance.StockIndicators.Helpers.VolatilityAverageWindow.Radical;
using Average=OoplesFinance.StockIndicators.Helpers.VolatilityAverageWindow.Average;
namespace OoplesFinance.StockIndicators.Helpers;

internal sealed class VervoortModifiedWindow : IDisposable
{
    private sealed class Stage
    {
        private readonly Average _first;
        private readonly Average? _second,_third;
        internal Stage(MovingAvgType kind,int period)
        {
            var composite=kind is MovingAvgType.DoubleExponentialMovingAverage or MovingAvgType.TripleExponentialMovingAverage;
            _first=new(composite?MovingAvgType.ExponentialMovingAverage:kind,period);
            if(composite)_second=new(MovingAvgType.ExponentialMovingAverage,period);
            if(kind==MovingAvgType.TripleExponentialMovingAverage)_third=new(MovingAvgType.ExponentialMovingAverage,period);
        }
        internal Number Next(Number value,bool final)
        {
            var first=_first.Next(Radical.Of(value),final).Rational();if(_second is null)return first;
            var second=_second.Next(Radical.Of(first),final).Rational();return _third is null?first.Times(2)-second:first.Times(3)-second.Times(3)+_third.Next(Radical.Of(second),final).Rational();
        }
        internal void Reset(){_first.Reset();_second?.Reset();_third?.Reset();}
    }
    private sealed class Moments
    {
        private readonly int _period;private readonly Queue<Number> _values=new();private Number _sum,_squares;
        internal Moments(int period)=>_period=Math.Max(1,period);
        internal (Number Variance,Number Maximum,bool Full) Next(Number value,bool final)
        {
            var full=_values.Count==_period;var expired=full?_values.Peek():default;var sum=_sum+value-expired;var squares=_squares+value*value-expired*expired;
            var count=Math.Min(_period,_values.Count+1L);var variance=(squares.Times(count)-sum*sum).Divide(count*count);var maximum=value.Sign<0?value.Times(-1):value;
            foreach(var old in full?_values.Skip(1):_values){var absolute=old.Sign<0?old.Times(-1):old;if((absolute-maximum).Sign>0)maximum=absolute;}
            if(final){if(full)_values.Dequeue();_values.Enqueue(value);_sum=sum;_squares=squares;}return(variance,maximum,count==_period);
        }
        internal void Reset(){_values.Clear();_sum=_squares=default;}
    }
    private readonly Stage _first,_second,_third,_center;
    private readonly Moments _inner,_outer;private readonly double _multiplier;
    private Number _previousInput,_previousOpen,_previousPercent,_previousSlope;
    private Number _previousVariance;private bool _hasPrevious;
    internal static bool Supports(MovingAvgType kind)=>StrengthWindow.Supports(kind)||kind is MovingAvgType.DoubleExponentialMovingAverage or MovingAvgType.TripleExponentialMovingAverage;
    internal VervoortModifiedWindow(MovingAvgType kind,int band,int outer,int smooth,double multiplier)
    {
        StreamingInputValidation.Finite(multiplier,nameof(multiplier));_multiplier=multiplier;
        _first=new(kind,smooth);_second=new(kind,smooth);_third=new(kind,smooth);_center=new(MovingAvgType.WeightedMovingAverage,band);_inner=new(band);_outer=new(outer);
    }
    internal static double OffsetRoot(Number numerator,Number variance)
    {
        if(numerator.Sign==0 || variance.Sign==0)return 50;
        var square=(numerator*numerator).Divide(variance);
        for(var precision=64;;precision=checked(precision*2))
        {
            var scaled=square.Numerator<<(2*precision);var floor=ExactPopulationDeviation.IntegerRoot(scaled/square.Denominator);
            var ceiling=floor*floor*square.Denominator==scaled?floor:floor+1;var grid=Number.Integer(BigInteger.One<<precision);
            var low=(Number.Integer(50)+Number.Integer(numerator.Sign>0?floor:-ceiling).Divide(grid)).Publish();
            var high=(Number.Integer(50)+Number.Integer(numerator.Sign>0?ceiling:-floor).Divide(grid)).Publish();
#pragma warning disable S1244 // Matching rounded bounds certify the exact algebraic output.
            if(low==high)return low;
#pragma warning restore S1244
        }
    }
    private int CompareBand(Number variance,Number price,int direction)
    {
        var coefficient=Number.Of(_multiplier).Times(direction);var delta=price-Number.Integer(50);
        if(variance.Sign==0 || coefficient.Sign==0)return (Number.Integer(50)-price).Sign;
        if(coefficient.Sign!=delta.Sign)return coefficient.Sign.CompareTo(delta.Sign);
        return coefficient.Sign*(coefficient*coefficient*variance-delta*delta).Sign;
    }
    internal (double Percent,double Upper,double Lower,Signal Trade) Next(double input,double high,double low,bool final)
    {
        StreamingInputValidation.Finite(input,nameof(input));StreamingInputValidation.Finite(high,nameof(high));StreamingInputValidation.Finite(low,nameof(low));
        var source=Number.Of(input);var open=(_previousInput+_previousOpen).Divide(2);var upperCandle=Number.Of(high);var lowerCandle=Number.Of(low);
        if((upperCandle-open).Sign<0)upperCandle=open;if((lowerCandle-open).Sign>0)lowerCandle=open;
        var close=(source+open+upperCandle+lowerCandle).Divide(4);var first=_first.Next(close,final);var second=_second.Next(first,final);var filtered=_third.Next(first.Times(2)-second,final);var center=_center.Next(filtered,final);
        var inner=_inner.Next(filtered,final);var threshold=inner.Maximum*Number.Of(1.4210854715202004e-14);double percent=0;
        if(inner.Full && (inner.Variance-threshold*threshold).Sign>0)percent=OffsetRoot((filtered-center).Times(25),inner.Variance);
        var value=Number.Of(percent);var outer=_outer.Next(value,final);var variance=outer.Full?outer.Variance:default;
        var width=variance*Number.Of(_multiplier);var upper=OffsetRoot(width,variance);var lower=OffsetRoot(width.Times(-1),variance);
        var slope=value-Number.Integer(50);var trade=slope.Sign>0 && (slope-_previousSlope).Sign>0?Signal.StrongBuy:slope.Sign<0 && (slope-_previousSlope).Sign<0?Signal.StrongSell
            :slope.Sign>0 || (_hasPrevious?CompareBand(_previousVariance,_previousPercent,-1):-_previousPercent.Sign)>0 && CompareBand(variance,value,-1)<0?Signal.Buy
            :slope.Sign<0 || (_hasPrevious?CompareBand(_previousVariance,_previousPercent,1):-_previousPercent.Sign)<0 && CompareBand(variance,value,1)>0?Signal.Sell:Signal.None;
        if(final){_previousInput=source;_previousOpen=open;_previousPercent=value;_previousSlope=slope;_previousVariance=variance;_hasPrevious=true;}
        return(percent,upper,lower,trade);
    }
    internal void Reset(){_first.Reset();_second.Reset();_third.Reset();_center.Reset();_inner.Reset();_outer.Reset();_previousInput=_previousOpen=_previousPercent=_previousSlope=default;_previousVariance=default;_hasPrevious=false;}
    public void Dispose()=>Reset();
    internal static (Dictionary<string,List<double>> Outputs,List<Signal> Signals) Calculate(StockData data,MovingAvgType kind,int band,int outer,int smooth,double multiplier)
    {
        var(input,high,low,open,close,_)=CalculationsHelper.GetInputValuesList(InputName.FullTypicalPrice,data);
        foreach(var values in new[]{input,high,low,open,close,data.Volumes})foreach(var value in values)StreamingInputValidation.Finite(value,nameof(data));
        var outputs=new Dictionary<string,List<double>>{{"UpperBand",new()},{"MiddleBand",new()},{"LowerBand",new()},{"PercentB",new()}};var signals=new List<Signal>(input.Count);
        using var state=new VervoortModifiedWindow(kind,band,outer,smooth,multiplier);
        for(var i=0;i<input.Count;i++){var mean=new ExactMeanAccumulator();mean.Add(open[i]);mean.Add(high[i]);mean.Add(low[i]);mean.Add(close[i]);var point=state.Next(data.ChainedValues is { Count: > 0 }?input[i]:mean.Mean(4),high[i],low[i],true);outputs["UpperBand"].Add(point.Upper);outputs["MiddleBand"].Add(50);outputs["LowerBand"].Add(point.Lower);outputs["PercentB"].Add(point.Percent);signals.Add(point.Trade);}return(outputs,signals);
    }
}

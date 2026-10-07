using System.Numerics;
using OoplesFinance.StockIndicators.Streaming;
using Number=OoplesFinance.StockIndicators.Helpers.PeriodicChannelWindow.Number;
namespace OoplesFinance.StockIndicators.Helpers;

internal sealed class R2AdaptiveWindow : IDisposable
{
    private sealed class Pair
    {
        private readonly int _period;private readonly Queue<(BigInteger X,BigInteger Y)> _history=new();
        private BigInteger _x,_y,_xx,_yy,_xy;
        internal Pair(int period)=>_period=period;
        internal (Number Covariance,Number XVariance,Number Weight,Number Mean,int Count) Next(BigInteger x,BigInteger y,bool final)
        {
            var full=_history.Count==_period;var old=full?_history.Peek():default;var sx=_x+x-old.X;var sy=_y+y-old.Y;var xx=_xx+x*x-old.X*old.X;var yy=_yy+y*y-old.Y*old.Y;var xy=_xy+x*y-old.X*old.Y;
            var n=Math.Min(_period,_history.Count+1L);var vx=n*xx-sx*sx;var vy=n*yy-sy*sy;var cov=n*xy-sx*sy;
            var weight=vx.IsZero||vy.IsZero?default:Number.Integer(cov*cov).Divide(Number.Integer(vx*vy));
            if(final){if(full)_history.Dequeue();_history.Enqueue((x,y));_x=sx;_y=sy;_xx=xx;_yy=yy;_xy=xy;}
            return(Number.Integer(cov).Divide(n*n),Number.Integer(vx).Divide(n*n),weight,Number.Integer(sx).Divide(n),(int)n);
        }
        internal void Reset(){_history.Clear();_x=_y=_xx=_yy=_xy=default;}
    }
    private readonly int _period;private readonly ExactLinearFitWindow _linear;private readonly RocBankAverage _mean;
    private readonly Pair _lagged,_first,_second;private readonly Queue<Number> _errors=new();private Number _energy;
    private BigInteger _previous,_previousSlope;private bool _hasPrevious;
    internal R2AdaptiveWindow(MovingAvgType kind,int period)
    {_period=Math.Max(1,period);_linear=new(_period,true);_mean=new(kind,_period,1,true);_lagged=new(_period);_first=new(_period);_second=new(_period);}
    private static BigInteger U(double v)=>ExactVarianceWindow.Units(v);
    private static BigInteger Round(Number value)=>RocBankValue.RoundUnits(value.Numerator,value.Denominator);
    internal static BigInteger OffsetUnits(Number center,Number numerator,Number variance)
    {
        if(numerator.Sign==0 || variance.Sign==0)return Round(center);
        var square=(numerator*numerator).Divide(variance);
        for(var precision=64;;precision=checked(precision*2))
        {
            var scaled=square.Numerator<<(2*precision);var floor=ExactPopulationDeviation.IntegerRoot(scaled/square.Denominator);var ceiling=floor*floor*square.Denominator==scaled?floor:floor+1;
            var grid=Number.Integer(BigInteger.One<<precision);var low=Round(center+Number.Integer(numerator.Sign>0?floor:-ceiling).Divide(grid));var high=Round(center+Number.Integer(numerator.Sign>0?ceiling:-floor).Divide(grid));
            if(low==high)return low;
        }
    }
    internal (double Value,Signal Trade) Next(double price,bool final)
    {
        StreamingInputValidation.Finite(price,nameof(price));var input=U(price);var y1=_linear.Next(price,final).RoundedLastUnits;var average=_mean.Next(new(price),final);var mean=Number.Integer(U(average.Mantissa)<<average.UpperShift);
        var x2=_hasPrevious?_previous:input;var lag=_lagged.Next(x2,input,final);var difference=Number.Integer(x2)-lag.Mean;
        var error=difference*difference;var energy=_energy+error-(_errors.Count==_period?_errors.Peek():default);var count=Math.Min(_period,_errors.Count+1L);var residual=energy.Divide(count);
        var y2=OffsetUnits(mean,lag.Count==_period?lag.Covariance*difference:default,lag.XVariance*residual);
        var first=_first.Next(y1,input,final).Weight;var second=_second.Next(y2,input,final).Weight;
        var output=Round(Number.Integer(x2)+first*Number.Integer(y1-x2)+second*Number.Integer(y2-x2));var slope=input-output;
        var trade=slope.Sign>0&&slope>_previousSlope?Signal.StrongBuy:slope.Sign<0&&slope<_previousSlope?Signal.StrongSell:slope.Sign>0?Signal.Buy:slope.Sign<0?Signal.Sell:Signal.None;
        if(final){if(_errors.Count==_period)_errors.Dequeue();_errors.Enqueue(error);_energy=energy;_previous=output;_previousSlope=slope;_hasPrevious=true;}
        return(ExactMeanAccumulator.UnitRatio(output,BigInteger.One),trade);
    }
    internal void Reset(){_linear.Reset();_mean.Reset();_lagged.Reset();_first.Reset();_second.Reset();_errors.Clear();_energy=default;_previous=_previousSlope=default;_hasPrevious=false;}
    public void Dispose(){_linear.Dispose();_mean.Dispose();_errors.Clear();}
    internal static (List<double> Values,List<Signal> Signals) Calculate(StockData data,MovingAvgType kind,int period)
    {
        var(input,_,_,_,_)=CalculationsHelper.GetInputValuesList(data);foreach(var values in new[]{input,data.OpenPrices,data.HighPrices,data.LowPrices,data.ClosePrices,data.Volumes})foreach(var value in values)StreamingInputValidation.Finite(value,nameof(data));
        using var state=new R2AdaptiveWindow(kind,period);var outputs=new List<double>(input.Count);var signals=new List<Signal>(input.Count);
        foreach(var price in input){var point=state.Next(price,true);outputs.Add(point.Value);signals.Add(point.Trade);}return(outputs,signals);
    }
    internal static void Core(ReadOnlySpan<double> input,Span<double> output,int period)
    {
        if(output.Length<input.Length)throw new ArgumentException("Output span must be at least input length.",nameof(output));var prices=input.ToArray();foreach(var value in prices)StreamingInputValidation.Finite(value,nameof(input));
        period=Math.Max(1,period);using var fit=new ExactLinearFitWindow(period,true);var history=new Queue<(BigInteger Price,BigInteger Error)>();BigInteger sum=0,squares=0,errors=0;
        for(var i=0;i<prices.Length;i++)
        {
            var price=U(prices[i]);var predicted=fit.Next(prices[i],true).RoundedLastUnits;var error=(price-predicted)*(price-predicted);
            if(history.Count==period){var old=history.Dequeue();sum-=old.Price;squares-=old.Price*old.Price;errors-=old.Error;}history.Enqueue((price,error));sum+=price;squares+=price*price;errors+=error;
            var total=history.Count*squares-sum*sum;var weight=total.IsZero?default:Number.Integer(total-history.Count*errors).Divide(Number.Integer(total));if(weight.Sign<0)weight=default;
            var blended=Number.Integer(price)+weight*Number.Integer(predicted-price);output[i]=ExactMeanAccumulator.UnitRatio(blended.Numerator,blended.Denominator);
        }
    }
}

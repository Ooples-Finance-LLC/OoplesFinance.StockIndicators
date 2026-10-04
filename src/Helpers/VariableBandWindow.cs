using OoplesFinance.StockIndicators.Builder.Compute;
using OoplesFinance.StockIndicators.Compatibility;
using OoplesFinance.StockIndicators.Streaming;
using E = OoplesFinance.StockIndicators.Helpers.VariableAverageExpression;
using F = OoplesFinance.StockIndicators.Helpers.UltimatePowerWeights.Fraction;
using N = OoplesFinance.StockIndicators.Helpers.MacZWindow.Number;

namespace OoplesFinance.StockIndicators.Helpers;

internal sealed class VariableBandWindow : IDisposable
{
    private readonly ExactVariableMovingAverageEngine? _variableLine, _variableRange;
    private readonly UnroundedMovingAverage? _line, _range;
    private readonly E _mult;
    private F _previousRangePrice, _previousDifference;
    private double _previousPrice, _previousUpper, _previousLower;
    private bool _started;
    internal VariableBandWindow(MovingAvgType kind,int length,double mult)
    {
        StreamingInputValidation.Finite(mult,nameof(mult)); _mult=E.Constant(F.Of(mult));
        if (kind==MovingAvgType.VariableMovingAverage) { _variableLine=new(length); _variableRange=new(length); }
        else { _line=new(kind,length); _range=new(kind,length); }
    }
    private static N Number(F value) => N.Integer(value.Numerator).Divide(N.Integer(value.Denominator));
    internal static F TrueRange(double high,double low,F previous)
    {
        var h=F.Of(high); var l=F.Of(low); var range=h-l;
        var up=(h-previous).Abs(); var down=(l-previous).Abs();
        return up>range ? (down>up ? down : up) : (down>range ? down : range);
    }
    internal static void Validate(double price,double high,double low)
    {
        StreamingInputValidation.Finite(price,nameof(price));
        StreamingInputValidation.Finite(high,nameof(high));
        StreamingInputValidation.Finite(low,nameof(low));
    }
    private (double Upper,double Middle,double Lower,Signal Trade) Publish(E line,E range,double price,bool final)
    {
        var width=_mult*range;
        var upper=(line+width).Publish(); var middle=line.Publish(); var lower=(line-width).Publish();
        var difference=F.Of(price)-F.Of(middle); var change=(difference-_previousDifference).Sign;
        var trade=difference.Sign>0 && change>0 ? Signal.StrongBuy : difference.Sign<0 && change<0 ? Signal.StrongSell
            : difference.Sign>0 || _previousPrice<_previousLower && price>lower ? Signal.Buy
            : difference.Sign<0 || _previousPrice>_previousUpper && price<upper ? Signal.Sell : Signal.None;
        if (final) { _previousDifference=difference; _previousPrice=price; _previousUpper=upper; _previousLower=lower; }
        return (upper,middle,lower,trade);
    }
    internal (double Upper,double Middle,double Lower,Signal Trade) Next(double price,double high,double low,bool final)
    {
        Validate(price,high,low); var p=F.Of(price);
        var tr=TrueRange(high,low,_started ? _previousRangePrice : p);
        var line=_variableLine is not null ? _variableLine.NextExpression(p,final) : E.Constant(_line!.Next(Number(p),final).ToFraction());
        var range=_variableRange is not null ? _variableRange.NextExpression(tr,final) : E.Constant(_range!.Next(Number(tr),final).ToFraction());
        var point=Publish(line,range,price,final);
        if (final) { _previousRangePrice=p; _started=true; }
        return point;
    }
    internal static (double[] Upper,double[] Middle,double[] Lower,Signal[] Trades) Calculate(StockData data,MovingAvgType kind,int length,double mult)
    {
        var (prices,highs,lows,_,_)=CalculationsHelper.GetInputValuesList(data);
        for (var i=0;i<prices.Count;i++) Validate(prices[i],highs[i],lows[i]);
        using var window=new VariableBandWindow(kind,length,mult);
        var upper=new double[prices.Count]; var middle=new double[prices.Count]; var lower=new double[prices.Count]; var trades=new Signal[prices.Count];
        if (!ComponentAverage.HasOverrides)
        {
            for (var i=0;i<prices.Count;i++) (upper[i],middle[i],lower[i],trades[i])=window.Next(prices[i],highs[i],lows[i],true);
        }
        else
        {
            var caller=data.CaptureInputSeries();
            try
            {
                var ranges=prices.Select((p,i)=>TrueRange(highs[i],lows[i],F.Of(i==0 ? p : prices[i-1])).Publish()).ToArray();
                var line=ComponentAverage.Take(SpanCompat.AsReadOnlySpan(prices),Math.Max(1,length))?.ToArray()
                    ?? CalculationsHelper.GetMovingAverageList(data,kind,length,prices).ToArray();
                data.RestoreInputSeries(caller);
                var range=ComponentAverage.Take(ranges,Math.Max(1,length))?.ToArray()
                    ?? CalculationsHelper.GetMovingAverageList(data,kind,length,ranges.ToList()).ToArray();
                for (var i=0;i<prices.Count;i++) (upper[i],middle[i],lower[i],trades[i])=window.Publish(E.Constant(F.Of(line[i])),E.Constant(F.Of(range[i])),prices[i],true);
            }
            finally { data.RestoreInputSeries(caller); }
        }
        return (upper,middle,lower,trades);
    }
    internal void Reset()
    {
        _variableLine?.Reset();_variableRange?.Reset();_line?.Reset();_range?.Reset();
        _previousRangePrice=_previousDifference=default;_previousPrice=_previousUpper=_previousLower=0;_started=false;
    }
    public void Dispose() { Reset(); _variableLine?.Dispose();_variableRange?.Dispose();_line?.Dispose();_range?.Dispose(); }
}

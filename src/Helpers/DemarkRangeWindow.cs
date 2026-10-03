namespace OoplesFinance.StockIndicators.Helpers;
// Binary64-rounded changes retain an extended upper exponent until the bounded ratio is published.
internal sealed class DemarkRangeWindow
{
    private readonly int _length;
    private readonly Queue<(RocBankValue Signed,RocBankValue Absolute)> _window = new();
    private readonly double[] _high = new double[8], _low = new double[8], _close = new double[8];
    private int _head, _count;
    private ExactMeanAccumulator _numerator, _denominator;
    internal DemarkRangeWindow(int length) => _length = Math.Max(1,length);
    private double At(double[] values,int lag) => _count >= lag ? values[(_head+8-lag)%8] : 0;
    private static RocBankValue Difference(double a,double b)
    { var sum=new ExactMeanAccumulator();sum.Add(a);sum.Add(b,-1);return RocBankValue.Round(sum); }
    internal double Next(double high,double low,double close,bool commit)
    {
        var overlap=(high>=At(_low,5)||high>=At(_low,6))&&(low<=At(_high,5)||low<=At(_high,6));
        var previousOverlap=At(_high,2)>=At(_close,8)&&(At(_low,2)<=At(_close,7)||At(_low,2)<=At(_close,8));
        var sum=new ExactMeanAccumulator();Difference(high,At(_high,2)).AddTo(ref sum);Difference(low,At(_low,2)).AddTo(ref sum);
        var change=RocBankValue.Round(sum);var signed=overlap||previousOverlap?default:change;
        var absolute=new RocBankValue(Math.Abs(change.Mantissa),change.UpperShift);
        var numerator=_numerator;var denominator=_denominator;
        if(_window.Count==_length){var old=_window.Peek();old.Signed.AddTo(ref numerator,-100);old.Absolute.AddTo(ref denominator,-1);}
        signed.AddTo(ref numerator,100);absolute.AddTo(ref denominator);
        var result=denominator.IsExactlyZero?0:numerator.Ratio(denominator);
        if(commit)
        {
            if(_window.Count==_length)_window.Dequeue();_window.Enqueue((signed,absolute));_numerator=numerator;_denominator=denominator;
            _high[_head]=high;_low[_head]=low;_close[_head]=close;_head=(_head+1)%8;_count=Math.Min(8,_count+1);
        }
        return result;
    }
    internal void Reset(){_window.Clear();_head=_count=0;_numerator=_denominator=default;}
}

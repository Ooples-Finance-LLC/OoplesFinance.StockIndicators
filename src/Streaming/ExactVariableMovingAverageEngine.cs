using OoplesFinance.StockIndicators.Helpers;
using E = OoplesFinance.StockIndicators.Helpers.VariableAverageExpression;
using F = OoplesFinance.StockIndicators.Helpers.UltimatePowerWeights.Fraction;

namespace OoplesFinance.StockIndicators.Streaming;

internal sealed class ExactVariableMovingAverageEngine : IDisposable
{
    private readonly E _gain, _retained;
    private readonly bool _retainsHistory;
    private readonly Extrema _high, _low;
    private E _up=E.Zero, _down=E.Zero, _positive=E.Zero, _negative=E.Zero, _index=E.Zero, _average=E.Zero;
    private F _previous;
    private long _count;
    private bool _hasStrength;
    internal ExactVariableMovingAverageEngine(int length)
    {
        length=Math.Max(1,length);
        _retainsHistory=length>1;
        _gain=E.Constant((F)1/length); _retained=E.One-_gain;
        _high=new(length,true); _low=new(length,false);
    }
    private E Smooth(E previous,E value) => _retained*previous+_gain*value;
    internal double Next(double value,bool final)
    {
        StreamingInputValidation.Finite(value,nameof(value));
        return NextExpression(F.Of(value),final).Publish();
    }
    internal E NextExpression(F value,bool final)
    {
        var change=_count==0 ? (F)0 : value-_previous;
        var up=Smooth(_up,E.Constant(change.Sign>0 ? change : 0));
        var down=Smooth(_down,E.Constant(change.Sign<0 ? -change : 0));
        var total=up+down; var quiet=total.CompareTo(E.Zero)==0;
        var positive=Smooth(_positive,quiet ? E.Zero : up/total);
        var negative=Smooth(_negative,quiet ? E.Zero : down/total);
        var sum=positive+negative;
        var strength=sum.CompareTo(E.Zero)==0 ? E.Zero : (positive-negative).Abs()/sum;
        var index=Smooth(_index,strength);
        // For length>1 the index remains strictly below one. Once positive it
        // remains positive. These facts certify the direction at exact endpoints.
        var trend=_retainsHistory && ReferenceEquals(strength,E.One) ? 1
            : _retainsHistory && _hasStrength && ReferenceEquals(strength,E.Zero) ? -1 : (int?)null;
        var high=_high.Prepare(index,_count,trend); var low=_low.Prepare(index,_count,trend);
        var spread=high.Value-low.Value;
        // Equal extrema choose the newest sample in both deques, so the low
        // identity also covers a zero spread. Endpoint positions need no division.
        var position=ReferenceEquals(index,low.Value) ? E.Zero : ReferenceEquals(index,high.Value) ? E.One
            : spread.CompareTo(E.Zero)==0 ? E.Zero : (index-low.Value)/spread;
        var gain=_gain*position; var price=E.Constant(value);
        var average=_count==0 ? price : (E.One-gain)*_average+gain*price;
        if (final)
        {
            _high.Commit(high,index,_count); _low.Commit(low,index,_count);
            _up=up; _down=down; _positive=positive; _negative=negative; _index=index;
            _average=average; _previous=value; _count++;
            _hasStrength |= !ReferenceEquals(strength,E.Zero);
        }
        return average;
    }
    internal void Reset()
    {
        _up=_down=_positive=_negative=_index=_average=E.Zero;
        _previous=default; _count=0; _hasStrength=false; _high.Reset(); _low.Reset();
    }
    public void Dispose() => Reset();
    private sealed class Extrema
    {
        private readonly int _length;
        private readonly bool _maximum;
        private readonly LinkedList<(E Value,long Index)> _values=new();
        internal Extrema(int length,bool maximum) { _length=length; _maximum=maximum; }
        private bool Better(E a,E b) => _maximum ? a.CompareTo(b)>=0 : a.CompareTo(b)<=0;
        internal (E Value,int Expired,int Dropped) Prepare(E value,long index,int? trend)
        {
            var first=_values.First; var expired=0;
            while (first is not null && first.Value.Index<=index-_length) { first=first.Next; expired++; }
            var last=_values.Last; var dropped=0;
            while (last is not null && last.Value.Index>index-_length
                && (trend.HasValue && last.Value.Index==index-1 ? (_maximum ? trend.Value>0 : trend.Value<0) : Better(value,last.Value.Value)))
            { last=last.Previous; dropped++; }
            return (_values.Count==expired+dropped ? value : first!.Value.Value,expired,dropped);
        }
        internal void Commit((E Value,int Expired,int Dropped) plan,E value,long index)
        {
            for (var i=0;i<plan.Expired;i++) _values.RemoveFirst();
            for (var i=0;i<plan.Dropped;i++) _values.RemoveLast();
            _values.AddLast((value,index));
        }
        internal void Reset() => _values.Clear();
    }
}

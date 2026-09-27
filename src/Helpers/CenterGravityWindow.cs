using System.Numerics;
namespace OoplesFinance.StockIndicators.Helpers;
internal sealed class CenterGravityWindow : IDisposable
{
    private readonly int _length;
    private readonly Queue<double> _values;
    private BigInteger _sum,_weighted;
    internal CenterGravityWindow(int length,int capacityHint=int.MaxValue){_length=Math.Max(1,length);_values=new(Math.Min(64,Math.Min(_length,Math.Max(1,capacityHint))));}
    internal double Next(double price,bool commit)
    {
        var value=ExactVarianceWindow.Units(price);var expired=_values.Count>=_length?ExactVarianceWindow.Units(_values.Peek()):BigInteger.Zero;
        var sum=_sum+value-expired;var weighted=_weighted+_sum+value-expired*(_length+1L);
        var result=0d;
        if(!sum.IsZero)
        {
            var ratio=RocBankValue.RoundUnits((-weighted*sum.Sign)<<1074,BigInteger.Abs(sum));
            var center=ExactVarianceWindow.Units((_length+1L)/2d);
            result=ExactMeanAccumulator.UnitRatio(ratio+center,BigInteger.One);
        }
        if(commit){_sum=sum;_weighted=weighted;if(_values.Count==_length)_values.Dequeue();_values.Enqueue(price);}return result;
    }
    internal void Reset(){_sum=_weighted=BigInteger.Zero;_values.Clear();}
    public void Dispose()=>_values.Clear();
}

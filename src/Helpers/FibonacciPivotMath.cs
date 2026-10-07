namespace OoplesFinance.StockIndicators.Helpers;
internal static class FibonacciPivotMath
{
    private static RocBankValue Combine(RocBankValue a,RocBankValue b,int weight=1,int divisor=1)
    {
        var sum=new ExactMeanAccumulator();a.AddTo(ref sum);b.AddTo(ref sum,weight);return RocBankValue.Round(sum,count:divisor);
    }
    private static RocBankValue Scale(RocBankValue range,double multiplier)
    {
        var product=new ExactMeanAccumulator();product.AddProduct(range.Mantissa,multiplier);product.ScaleByPowerOfTwo(range.UpperShift);return RocBankValue.Round(product);
    }
    internal static double[] Levels(double high,double low,double close)
    {
        var total=new ExactMeanAccumulator();total.Add(high);total.Add(low);total.Add(close);var pivot=RocBankValue.Round(total,count:3);
        var range=Combine(new(high),new(low),-1);var first=Scale(range,0.382);var second=Scale(range,MathHelper.InversePhi);
        var s1=Combine(pivot,first,-1);var s2=Combine(pivot,second,-1);var s3=Combine(pivot,range,-1);
        var r1=Combine(pivot,first);var r2=Combine(pivot,second);var r3=Combine(pivot,range);
        return new[]{pivot.Publish(),s1.Publish(),s2.Publish(),s3.Publish(),r1.Publish(),r2.Publish(),r3.Publish(),
            Combine(s3,s2,divisor:2).Publish(),Combine(s2,s1,divisor:2).Publish(),Combine(s1,pivot,divisor:2).Publish(),
            Combine(r1,pivot,divisor:2).Publish(),Combine(r2,r1,divisor:2).Publish(),Combine(r3,r2,divisor:2).Publish()};
    }
}

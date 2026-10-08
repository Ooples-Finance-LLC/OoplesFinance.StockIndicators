namespace OoplesFinance.StockIndicators.Helpers;
internal static class FloorPivotMath
{
    internal static readonly string[] Keys={"Pivot","S1","S2","S3","R1","R2","R3","M1","M2","M3","M4","M5","M6"};
    private static RocBankValue Combine(RocBankValue a,RocBankValue b,int weight=1,int divisor=1)
    {
        var sum=new ExactMeanAccumulator();a.AddTo(ref sum);b.AddTo(ref sum,weight);return RocBankValue.Round(sum,count:divisor);
    }
    internal static double[] Levels(double high,double low,double close)
    {
        var total=new ExactMeanAccumulator();total.Add(high);total.Add(low);total.Add(close);var pivot=RocBankValue.Round(total,count:3);
        var twice=new ExactMeanAccumulator();pivot.AddTo(ref twice,2);var left=twice;left.Add(high,-1);var right=twice;right.Add(low,-1);
        var s1=RocBankValue.Round(left);var r1=RocBankValue.Round(right);
        var range=Combine(new(high),new(low),-1);
        var s2=Combine(pivot,range,-1);var r2=Combine(pivot,range);
        var s3=Combine(s1,range,-1);var r3=Combine(r1,range);
        return new[]{pivot.Publish(),s1.Publish(),s2.Publish(),s3.Publish(),r1.Publish(),r2.Publish(),r3.Publish(),
            Combine(s3,s2,divisor:2).Publish(),Combine(s2,s1,divisor:2).Publish(),Combine(s1,pivot,divisor:2).Publish(),
            Combine(r1,pivot,divisor:2).Publish(),Combine(r2,r1,divisor:2).Publish(),Combine(r3,r2,divisor:2).Publish()};
    }
}

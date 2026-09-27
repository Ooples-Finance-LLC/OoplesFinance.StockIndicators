namespace OoplesFinance.StockIndicators.Helpers;
internal static class CamarillaPivotMath
{
    internal static readonly string[] Keys={"Pivot","S1","S2","S3","S4","S5","R1","R2","R3","R4","R5","M1","M2","M3","M4","M5","M6"};
    private static RocBankValue Combine(RocBankValue a,RocBankValue b,int weight=1,int divisor=1)
    {
        var sum=new ExactMeanAccumulator();a.AddTo(ref sum);b.AddTo(ref sum,weight);return RocBankValue.Round(sum,count:divisor);
    }
    private static RocBankValue Scale(RocBankValue value,double multiplier)
    {
        var product=new ExactMeanAccumulator();product.AddProduct(value.Mantissa,multiplier);product.ScaleByPowerOfTwo(value.UpperShift);return RocBankValue.Round(product);
    }
    internal static double[] Levels(double high,double low,double close)
    {
        var total=new ExactMeanAccumulator();total.Add(high);total.Add(low);total.Add(close);var pivot=RocBankValue.Round(total,count:3);
        var range=Combine(new(high),new(low),-1);var factors=new[]{1.1/12,1.1/6,0.275,0.55};var support=new RocBankValue[5];var resistance=new RocBankValue[5];
        for(var i=0;i<factors.Length;i++){var width=Scale(range,factors[i]);support[i]=Combine(new(close),width,-1);resistance[i]=Combine(new(close),width);}
        var ratio=MassIndexSum.Ratio(new(high),new(low));resistance[4]=Scale(ratio,close);
        var gap=Combine(resistance[4],new(close),-1);support[4]=Combine(new(close),gap,-1);
        return new[]{pivot.Publish(),support[0].Publish(),support[1].Publish(),support[2].Publish(),support[3].Publish(),support[4].Publish(),
            resistance[0].Publish(),resistance[1].Publish(),resistance[2].Publish(),resistance[3].Publish(),resistance[4].Publish(),
            Combine(support[2],support[1],divisor:2).Publish(),Combine(support[1],support[0],divisor:2).Publish(),
            Combine(resistance[1],resistance[0],divisor:2).Publish(),Combine(resistance[2],resistance[1],divisor:2).Publish(),
            Combine(resistance[2],resistance[3],divisor:2).Publish(),Combine(support[3],support[2],divisor:2).Publish()};
    }
}

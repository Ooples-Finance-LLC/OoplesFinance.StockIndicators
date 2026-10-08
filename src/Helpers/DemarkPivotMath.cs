namespace OoplesFinance.StockIndicators.Helpers;
internal static class DemarkPivotMath
{
    internal static double[] Levels(double open,double high,double low,double close)
    {
        var total=new ExactMeanAccumulator();total.Add(high);total.Add(low);total.Add(close);
        total.Add(close<open?low:close>open?high:close);
        var pivot=RocBankValue.Round(total,count:4);var half=RocBankValue.Round(total,count:2);
        var support=new ExactMeanAccumulator();half.AddTo(ref support);support.Add(high,-1);
        var resistance=new ExactMeanAccumulator();half.AddTo(ref resistance);resistance.Add(low,-1);
        return new[]{pivot.Publish(),RocBankValue.Round(support).Publish(),RocBankValue.Round(resistance).Publish()};
    }
}

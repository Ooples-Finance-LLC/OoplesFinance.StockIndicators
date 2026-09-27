namespace OoplesFinance.StockIndicators.Helpers;
internal static class PivotAverageMath
{
    internal static (double P1,double P2,double P3) Values(double high,double low,double close,double open)
    {
        var extrema=new ExactMeanAccumulator();extrema.Add(high);extrema.Add(low);
        var completed=extrema;completed.Add(close);
        var combined=completed;combined.Add(open);
        var opening=extrema;opening.Add(open);
        return (completed.Mean(3),combined.Mean(4),opening.Mean(3));
    }
}

using OoplesFinance.StockIndicators.Core;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Validation;
namespace OoplesFinance.StockIndicators.Tests.Unit.ValidationTests;
public sealed class PivotCoreNumericalTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void CoresMatchIndependentPreviousPeriodMeans(bool woodie)
    {
        foreach(var scale in new[]{1d,double.Epsilon,double.MaxValue/8})
            Check(Enumerable.Range(0,15).Select(i=>new Bar(DateTime.UnixEpoch.AddDays(i),0,(i%5+2)*scale,-(i%3+1)*scale,(i%3-1)*scale,1)).ToArray());
        Check(Array.Empty<Bar>());
        Check(Enumerable.Range(0,5).Select(i=>new Bar(DateTime.UnixEpoch.AddDays(i),double.MaxValue,double.MaxValue,double.MaxValue,double.MaxValue,1)).ToArray());
        Check(Enumerable.Range(0,9).Select(i=>new Bar(DateTime.UnixEpoch.AddDays(i),0,double.MaxValue,-double.MaxValue,i%2==0?double.MaxValue:-double.MaxValue,1)).ToArray());
        void Check(Bar[] bars)
        {
            var expected=(woodie?BuiltInFormulaReferences.WoodiePivotOutputs(bars):BuiltInFormulaReferences.FibonacciPivotOutputs(bars))["Pivot"];
            var high=bars.Select(b=>b.High).ToArray();var low=bars.Select(b=>b.Low).ToArray();var close=bars.Select(b=>b.Close).ToArray();var output=new double[bars.Length];
            if(woodie)TrendCore.WoodiePivotPoint(high,low,close,output);else TrendCore.FibonacciPivotPoint(high,low,close,output);
            Assert.Equal(expected,output);
        }
    }
}

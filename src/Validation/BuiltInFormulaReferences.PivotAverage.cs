using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Builder.Specs;
namespace OoplesFinance.StockIndicators.Validation;
internal static partial class BuiltInFormulaReferences
{
    internal static IReadOnlyDictionary<string,double[]> PivotAverageOutputs(IReadOnlyList<Bar> bars,IBuiltInIndicator indicator)
    {
        var options=(PivotPointAverageSpecOptions)indicator.CreateOptions();return PivotAverageOutputs(bars,options.Length,AverageKind(options,1),options.InputLength);
    }
    internal static IReadOnlyDictionary<string,double[]> PivotAverageOutputs(IReadOnlyList<Bar> bars,int length,int kind,InputLength period)
    {
        DateTime Key(DateTime time)
        {
            if(period==InputLength.Year)return new DateTime(time.Year,1,1);
            if(period==InputLength.Month)return new DateTime(time.Year,time.Month,1);
            if(period==InputLength.Week){var date=time.Date;while(date.DayOfWeek!=DayOfWeek.Monday)date=date.AddDays(-1);return date;}
            var unit=period==InputLength.Minute?TimeSpan.TicksPerMinute:period==InputLength.Hour?TimeSpan.TicksPerHour:TimeSpan.TicksPerDay;
            return new DateTime(time.Ticks/unit*unit);
        }
        var keys=new[]{"Pivot1","Signal1","Pivot2","Signal2","Pivot3","Signal3"};var output=keys.ToDictionary(k=>k,k=>new double[bars.Count]);
        var groups=bars.Select((bar,index)=>(bar,index)).GroupBy(x=>Key(x.bar.Time)).ToArray();var pivots=new[]{new double[groups.Length],new double[groups.Length],new double[groups.Length]};
        for(var g=0;g<groups.Length;g++)
        {
            var previous=g==0?null:groups[g-1].Select(x=>x.bar).ToArray();var high=previous?.Max(b=>b.High)??0;var low=previous?.Min(b=>b.Low)??0;var close=previous?.Last().Close??0;var open=groups[g].First().bar.Open;
            pivots[0][g]=ExactPriceMean(high,low,close);pivots[1][g]=ExactPriceMean(high,low,close,open);pivots[2][g]=ExactPriceMean(high,low,open);
        }
        for(var slot=0;slot<3;slot++)
        {
            var signals=SmoothRocBankStage(pivots[slot].Select(ReferenceFraction.FromDouble).ToArray(),Math.Max(1,length),kind).Select(v=>v.ToDouble()).ToArray();
            for(var g=0;g<groups.Length;g++)foreach(var entry in groups[g]){output[keys[slot*2]][entry.index]=pivots[slot][g];output[keys[slot*2+1]][entry.index]=signals[g];}
        }
        return output;
    }
}

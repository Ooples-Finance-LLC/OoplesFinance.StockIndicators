using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.Validation;

internal static partial class BuiltInFormulaReferences
{
    internal static IReadOnlyDictionary<string,double[]> VervoortCandleOutputs(IReadOnlyList<Bar> bars,IBuiltInIndicator indicator,bool selected=false)
    {
        var options=indicator.CreateOptions();var longTerm=indicator.BatchName==IndicatorName.VervoortHeikenAshiLongTermCandlestickOscillator;
        return VervoortCandleOutputs(bars,longTerm,Integer(options,"Length",longTerm?55:34),Number(options,1.1,"Factor"),selected:selected);
    }
    internal static IReadOnlyDictionary<string,double[]> VervoortCandleOutputs(IReadOnlyList<Bar> bars,bool longTerm,int length,double factor=1.1,ICollection<Signal>? signals=null,bool selected=false)
    {
        ReferenceFraction R(double value)=>ReferenceFraction.FromDouble(value);
        ReferenceFraction Round(ReferenceFraction value)=>value.RoundExtendedBinary64();
        var full=bars.Select(b=>selected?R(b.Close):Round((R(b.Open)+R(b.High)+R(b.Low)+R(b.Close))/R(4))).ToArray();
        var open=new ReferenceFraction[bars.Count];var close=new ReferenceFraction[bars.Count];
        for(var i=0;i<bars.Count;i++)
        {
            open[i]=i==0?R(0):Round((full[i-1]+open[i-1])/R(2));
            var high=R(bars[i].High);var low=R(bars[i].Low);
            close[i]=Round((full[i]+open[i]+(high.CompareTo(open[i])>0?high:open[i])+(low.CompareTo(open[i])<0?low:open[i]))/R(4));
        }
        ReferenceFraction[] Tema(ReferenceFraction[] input)
        {
            var first=SmoothRocBankStage(input,length,3,Round);var second=SmoothRocBankStage(first,length,3,Round);var third=SmoothRocBankStage(second,length,3,Round);
            return input.Select((_,i)=>Round(R(3)*first[i]-R(3)*second[i]+third[i])).ToArray();
        }
        ReferenceFraction[] Smooth(ReferenceFraction[] input)
        {var first=Tema(input);if(longTerm)return first;var second=Tema(first);return first.Select((v,i)=>Round(R(2)*v-second[i])).ToArray();}
        ReferenceFraction[] Project(ReferenceFraction[] input)
        {var first=Smooth(input);var second=Smooth(first);return first.Select((v,i)=>Round(R(2)*v-second[i])).ToArray();}
        var transformed=Project(close);var midpoint=Project(bars.Select(b=>Round((R(b.High)+R(b.Low))/R(2))).ToArray());
        var upSeed=new bool[bars.Count];var downSeed=new bool[bars.Count];var upKeep=new bool[bars.Count];var downKeep=new bool[bars.Count];
        var upTrend=new bool[bars.Count];var downTrend=new bool[bars.Count];var result=new double[bars.Count];
        for(var i=0;i<bars.Count;i++)
        {
            var b=bars[i];var previousHigh=i==0?0:bars[i-1].High;var previousLow=i==0?0:bars[i-1].Low;var previousClose=i==0?0:bars[i-1].Close;
            var previousUp=i==0 || close[i-1].CompareTo(open[i-1])>=0;var previousDown=i>0 && close[i-1].CompareTo(open[i-1])<0;
            var body=R(b.Close)-R(b.Open);if(body.Sign<0)body=R(0)-body;
            var narrow=body.CompareTo((R(b.High)-R(b.Low))*R(longTerm?factor:.35))<0;
            var rising=b.Close>=b.Open || b.Close>=previousClose;var falling=b.Close<b.Open || b.Close<previousClose;
            upSeed[i]=close[i].CompareTo(open[i])>=0 && previousUp || midpoint[i].CompareTo(transformed[i])>=0
                || longTerm && (R(b.Close).CompareTo(close[i])>=0 || b.High>previousHigh || b.Low>previousLow);
            downSeed[i]=close[i].CompareTo(open[i])<0 && previousDown || midpoint[i].CompareTo(transformed[i])<0;
            upKeep[i]=longTerm?upSeed[i] || i>0 && upSeed[i-1] && rising:(upSeed[i] || i>0 && upSeed[i-1]) && rising;
            downKeep[i]=longTerm?downSeed[i] || i>0 && downSeed[i-1] && falling:(downSeed[i] || i>0 && downSeed[i-1]) && falling;
            upTrend[i]=upKeep[i] || i>0 && upKeep[i-1] && narrow && b.High>=previousLow;
            downTrend[i]=longTerm?(downKeep[i] || i>0 && downKeep[i-1]) && narrow && b.Low<=previousHigh
                :downKeep[i] || i>0 && downKeep[i-1] && narrow && b.Low<=previousHigh;
            var previous=i==0?0:result[i-1];var up=i>0 && downTrend[i-1] && !downTrend[i] && upTrend[i];var down=i>0 && upTrend[i-1] && !upTrend[i] && downTrend[i];
            result[i]=up?1:down?-1:previous;
            signals?.Add(result[i]>0?result[i]>previous?Signal.StrongBuy:Signal.Buy:result[i]<0?result[i]<previous?Signal.StrongSell:Signal.Sell:Signal.None);
        }
        return Outputs((longTerm?"Vhaltco":"Vhaco",result));
    }
}

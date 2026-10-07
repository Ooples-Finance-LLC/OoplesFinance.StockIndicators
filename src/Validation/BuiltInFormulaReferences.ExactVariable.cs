using System.Numerics;
using OoplesFinance.StockIndicators.Indicators;
using R = OoplesFinance.StockIndicators.Validation.ReferenceFraction;

namespace OoplesFinance.StockIndicators.Validation;

internal static partial class BuiltInFormulaReferences
{
    private sealed class VariableReferencePrecisionException : Exception { }
    private readonly struct VariableReferenceBound
    {
        internal readonly R Low, High;
        internal VariableReferenceBound(R low,R high) { Low=low;High=high; }
        internal static VariableReferenceBound Point(R value)=>new(value,value);
        internal bool IsZero=>Low.Sign==0 && High.Sign==0;
    }
    // Vector stages and direct window extrema are independent of the production
    // expression graph and monotone deques. All bounds use ReferenceFraction.
    private sealed class VariableReferenceIntervals
    {
        private readonly int _bits;
        private static readonly R Zero=new(0),One=new(1);
        internal VariableReferenceIntervals(int bits)=>_bits=bits;
        private static int Bits(BigInteger value)
        {
            var bytes=BigInteger.Abs(value).ToByteArray();var last=bytes.Length-1;
            while(last>0 && bytes[last]==0) last--;
            var result=last*8;for(var top=bytes[last];top!=0;top>>=1) result++;
            return result;
        }
        private R Rounded(R value,bool up)
        {
            if(value.Sign==0) return value;
            var (n,d)=value.Components;var exponent=Bits(n)-Bits(d)-_bits;
            var numerator=exponent<0 ? n<<-exponent : n;
            var denominator=exponent>0 ? d<<exponent : d;
            var q=BigInteger.DivRem(numerator,denominator,out var remainder);
            if(up && remainder.Sign>0) q++;
            if(!up && remainder.Sign<0) q--;
            return exponent>=0 ? new R(q<<exponent) : new R(q)/new R(BigInteger.One<<-exponent);
        }
        private VariableReferenceBound Bound(R low,R high)
        {
            if(low.CompareTo(high)==0)
            {
                var (n,d)=low.Components;
                if(Bits(n)+Bits(d)<=384) return VariableReferenceBound.Point(low);
            }
            return new(Rounded(low,false),Rounded(high,true));
        }
        private VariableReferenceBound Add(VariableReferenceBound a,VariableReferenceBound b)=>Bound(a.Low+b.Low,a.High+b.High);
        private VariableReferenceBound Subtract(VariableReferenceBound a,VariableReferenceBound b)=>Bound(a.Low-b.High,a.High-b.Low);
        private VariableReferenceBound Multiply(VariableReferenceBound a,VariableReferenceBound b)
        {
            var values=new[] { a.Low*b.Low,a.Low*b.High,a.High*b.Low,a.High*b.High };
            return Bound(values.Min(),values.Max());
        }
        private VariableReferenceBound Divide(VariableReferenceBound a,VariableReferenceBound b)
        {
            if(b.Low.Sign<=0 && b.High.Sign>=0) throw new VariableReferencePrecisionException();
            var values=new[] { a.Low/b.Low,a.Low/b.High,a.High/b.Low,a.High/b.High };
            return Bound(values.Min(),values.Max());
        }
        private VariableReferenceBound Absolute(VariableReferenceBound value)
        {
            var a=value.Low.Abs();var b=value.High.Abs();
            return new(value.Low.Sign<=0 && value.High.Sign>=0 ? Zero : (a.CompareTo(b)<0 ? a : b),a.CompareTo(b)>0 ? a : b);
        }
        internal VariableReferenceBound[] Values(R[] prices,int length)
        {
            length=Math.Max(1,length);
            var zero=VariableReferenceBound.Point(Zero);var one=VariableReferenceBound.Point(One);
            var period=VariableReferenceBound.Point(new R(length));var retained=VariableReferenceBound.Point(new R(length-1));
            VariableReferenceBound[] Smooth(VariableReferenceBound[] input)
            {
                var output=new VariableReferenceBound[input.Length];var prior=zero;
                for(var i=0;i<input.Length;i++) output[i]=prior=Divide(Add(Multiply(prior,retained),input[i]),period);
                return output;
            }
            var changes=prices.Select((p,i)=>i==0 ? Zero : p-prices[i-1]).ToArray();
            var up=Smooth(changes.Select(v=>VariableReferenceBound.Point(v.Sign>0 ? v : Zero)).ToArray());
            var down=Smooth(changes.Select(v=>VariableReferenceBound.Point(v.Sign<0 ? Zero-v : Zero)).ToArray());
            var positive=Smooth(up.Select((v,i)=>v.IsZero ? zero : down[i].IsZero ? one : Divide(v,Add(v,down[i]))).ToArray());
            var negative=Smooth(down.Select((v,i)=>v.IsZero ? zero : up[i].IsZero ? one : Divide(v,Add(v,up[i]))).ToArray());
            var strengths=positive.Select((v,i)=>v.IsZero && negative[i].IsZero ? zero
                : v.IsZero || negative[i].IsZero ? one : Divide(Absolute(Subtract(v,negative[i])),Add(v,negative[i]))).ToArray();
            var index=Smooth(strengths);var result=new VariableReferenceBound[prices.Length];
            var average=prices.Length==0 ? zero : VariableReferenceBound.Point(prices[0]);
            for(var i=0;i<prices.Length;i++)
            {
                var start=Math.Max(0,i-length+1);var current=index[i];
                var minCurrent=true;var maxCurrent=true;var strictMaximum=false;
                var low=current;var high=current;
                for(var j=start;j<i;j++)
                {
                    minCurrent &= current.High.CompareTo(index[j].Low)<=0;
                    maxCurrent &= current.Low.CompareTo(index[j].High)>=0;
                    strictMaximum |= current.Low.CompareTo(index[j].High)>0;
                    low=new(low.Low.CompareTo(index[j].Low)<0 ? low.Low : index[j].Low,low.High.CompareTo(index[j].High)<0 ? low.High : index[j].High);
                    high=new(high.Low.CompareTo(index[j].Low)>0 ? high.Low : index[j].Low,high.High.CompareTo(index[j].High)>0 ? high.High : index[j].High);
                }
                VariableReferenceBound position;
                if(minCurrent) position=zero;
                else if(maxCurrent && strictMaximum) position=one;
                else
                {
                    position=Divide(Subtract(current,low),Subtract(high,low));
                    position=new(position.Low.Sign<0 ? Zero : position.Low,position.High.CompareTo(One)>0 ? One : position.High);
                }
                var gain=Divide(position,period);
                average=Add(Multiply(Subtract(one,gain),average),Multiply(gain,VariableReferenceBound.Point(prices[i])));
                result[i]=average;
            }
            return result;
        }
        internal VariableReferenceBound Combine(VariableReferenceBound center,VariableReferenceBound range,R multiplier)
            => Add(center,Multiply(range,VariableReferenceBound.Point(multiplier)));
    }
    private static R[] ExactVariableReference(R[] prices,int length)
    {
        length=Math.Max(1,length);var zero=new R(0);var one=new R(1);var n=new R(length);
        R[] Smooth(R[] input)
        {
            var result=new R[input.Length];var prior=zero;
            for(var i=0;i<input.Length;i++) result[i]=prior=(prior*(n-one)+input[i])/n;
            return result;
        }
        var changes=prices.Select((p,i)=>i==0 ? zero : p-prices[i-1]).ToArray();
        var up=Smooth(changes.Select(v=>v.Sign>0 ? v : zero).ToArray());
        var down=Smooth(changes.Select(v=>v.Sign<0 ? zero-v : zero).ToArray());
        var positive=Smooth(up.Select((v,i)=>(v+down[i]).Sign==0 ? zero : v/(v+down[i])).ToArray());
        var negative=Smooth(down.Select((v,i)=>(v+up[i]).Sign==0 ? zero : v/(v+up[i])).ToArray());
        var index=Smooth(positive.Select((v,i)=>(v+negative[i]).Sign==0 ? zero : (v-negative[i]).Abs()/(v+negative[i])).ToArray());
        var result=new R[prices.Length];var average=prices.Length==0 ? zero : prices[0];
        for(var i=0;i<prices.Length;i++)
        {
            var sample=index.Skip(Math.Max(0,i-length+1)).Take(Math.Min(length,i+1)).ToArray();
            var low=sample.Min();var high=sample.Max();
            var gain=high.CompareTo(low)==0 ? zero : (index[i]-low)/(high-low)/n;
            result[i]=average=(one-gain)*average+gain*prices[i];
        }
        return result;
    }
    private static bool TryVariablePublish(VariableReferenceBound[] values,out double[] output)
    {
        output=new double[values.Length];var complete=true;
        for(var i=0;i<values.Length;i++)
        {
            var low=values[i].Low.ToDouble();var high=values[i].High.ToDouble();
            if(!low.Equals(high)) { output[i]=double.NaN;complete=false; } // NOSONAR: S1244 - Distinct rounded endpoints cannot certify a uniquely rounded output.
            else output[i]=low;
        }
        return complete;
    }
    internal static double[] CertifiedVariableReference(double[] values,int length)
    {
        var prices=values.Select(R.FromDouble).ToArray();
        double[]? certified=null;
        for(var bits=128;bits<=4096;bits*=2)
        {
            try
            {
                var complete=TryVariablePublish(new VariableReferenceIntervals(bits).Values(prices,length),out var result);
                certified=result;if(complete) return result;
            }
            catch(VariableReferencePrecisionException) { }
        }
        if(certified is null) return ExactVariableReference(prices,length).Select(v=>v.ToDouble()).ToArray();
        var last=Array.FindLastIndex(certified,double.IsNaN);
        var exact=ExactVariableReference(prices.Take(last+1).ToArray(),length);
        for(var i=0;i<=last;i++) if(double.IsNaN(certified[i])) certified[i]=exact[i].ToDouble();
        return certified;
    }
    internal static Dictionary<string,double[]> CertifiedVariableBandsReference(IReadOnlyList<Bar> bars,int length,MovingAvgType kind,double mult,bool selected=false)
    {
        length=Math.Max(1,length);
        var prices=bars.Select(b=>R.FromDouble(b.Close)).ToArray();var ranges=new R[bars.Count];
        for(var i=0;i<bars.Count;i++)
        {
            var high=bars[i].High;var low=bars[i].Low;
            if(selected)
            {
                var tolerance=1e-12*Math.Max(Math.Abs(high),Math.Abs(low));
                if(bars[i].Close<low-tolerance || bars[i].Close>high+tolerance)
                { high=Math.Max(bars[i].Close,i==0 ? bars[i].Close : bars[i-1].Close);low=Math.Min(bars[i].Close,i==0 ? bars[i].Close : bars[i-1].Close); }
            }
            var h=R.FromDouble(high);var l=R.FromDouble(low);var previous=prices[i==0 ? i : i-1];
            ranges[i]=new[] { h-l,(h-previous).Abs(),(l-previous).Abs() }.Max();
        }
        var multiplier=R.FromDouble(mult);var variable=kind==MovingAvgType.VariableMovingAverage;
        var ordinaryLine=variable ? null : RationalAverage(prices,length,kind);
        var ordinaryRange=variable ? null : RationalAverage(ranges,length,kind);
        Dictionary<string,double[]>? certified=null;
        for(var bits=128;bits<=4096;bits*=2)
        {
            try
            {
                var arithmetic=new VariableReferenceIntervals(bits);
                var line=variable ? arithmetic.Values(prices,length) : ordinaryLine!.Select(VariableReferenceBound.Point).ToArray();
                var range=variable ? arithmetic.Values(ranges,length) : ordinaryRange!.Select(VariableReferenceBound.Point).ToArray();
                var upper=line.Select((v,i)=>arithmetic.Combine(v,range[i],multiplier)).ToArray();
                var lower=line.Select((v,i)=>arithmetic.Combine(v,range[i],new R(0)-multiplier)).ToArray();
                var middleComplete=TryVariablePublish(line,out var m);
                var upperComplete=TryVariablePublish(upper,out var u);
                var lowerComplete=TryVariablePublish(lower,out var l);
                certified=new() { ["UpperBand"]=u,["MiddleBand"]=m,["LowerBand"]=l };
                if(middleComplete && upperComplete && lowerComplete) return certified;
            }
            catch(VariableReferencePrecisionException) { }
        }
        certified ??= new() { ["UpperBand"]=Enumerable.Repeat(double.NaN,prices.Length).ToArray(),
            ["MiddleBand"]=Enumerable.Repeat(double.NaN,prices.Length).ToArray(),["LowerBand"]=Enumerable.Repeat(double.NaN,prices.Length).ToArray() };
        var last=certified.Values.Max(values=>Array.FindLastIndex(values,double.IsNaN));
        var exactLine=ordinaryLine ?? ExactVariableReference(prices.Take(last+1).ToArray(),length);
        var exactRange=ordinaryRange ?? ExactVariableReference(ranges.Take(last+1).ToArray(),length);
        for(var i=0;i<=last;i++)
        {
            if(double.IsNaN(certified["MiddleBand"][i])) certified["MiddleBand"][i]=exactLine[i].ToDouble();
            if(double.IsNaN(certified["UpperBand"][i])) certified["UpperBand"][i]=(exactLine[i]+multiplier*exactRange[i]).ToDouble();
            if(double.IsNaN(certified["LowerBand"][i])) certified["LowerBand"][i]=(exactLine[i]-multiplier*exactRange[i]).ToDouble();
        }
        return certified;
    }
}

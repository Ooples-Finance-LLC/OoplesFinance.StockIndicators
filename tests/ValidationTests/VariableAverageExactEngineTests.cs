using OoplesFinance.StockIndicators.Streaming;
using OoplesFinance.StockIndicators.Validation;
using E = OoplesFinance.StockIndicators.Helpers.VariableAverageExpression;
using F = OoplesFinance.StockIndicators.Helpers.UltimatePowerWeights.Fraction;

namespace OoplesFinance.StockIndicators.Tests.Unit.ValidationTests;

public sealed class VariableAverageExactEngineTests
{
    [Fact]
    public void IndependentFractionalPathsRetainZeroDifference()
    {
        var x=E.Constant(F.Of(double.MaxValue));
        // The two paths have different outward rounding errors. Subtracting
        // intervals must pair the left lower bound with the right upper bound.
        var difference=x*E.Constant((F)1/2)+x*E.Constant((F)1/3)-x*E.Constant((F)5/6);
        Assert.Equal(0,difference.CompareTo(E.Zero));
        Assert.Equal(0d,difference.Publish());
    }
    [Fact]
    public void ExpressionsCertifyRationalCancellationAndScale()
    {
        foreach (var scale in new[] { double.Epsilon,1d,double.MaxValue })
        {
            var x=E.Constant(F.Of(scale)); var third=E.Constant((F)1/3);
            var expression=(x*third+x*third+x*third)-x;
            Assert.Equal(0,expression.CompareTo(E.Zero));
            Assert.Equal(0d,expression.Publish());
            Assert.Equal(scale,(x*E.Constant(2)-x).Publish());
        }
        var a=E.Constant((F)1/3); var b=E.Constant((F)1/7);
        Assert.Equal(1d,((a-b)/(a-b)).Publish());
    }
    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(6)]
    public void ExactDefinitionMatchesAcrossSignsAndScales(int length)
    {
        foreach (var scale in new[] { double.Epsilon,1d,double.MaxValue/16 })
        {
            var prices=new[] { 1d,3,-1,-2,4,4,2,-3,5,1,-4,2 }.Select(v=>v*scale).ToArray();
            var expected=Reference(prices,length);
            Assert.Equal(expected,BuiltInFormulaReferences.CertifiedVariableReference(prices,length));
            using var state=new ExactVariableMovingAverageEngine(length);
            for (var replay=0;replay<2;replay++)
            {
                state.Reset();
                for (var i=0;i<prices.Length;i++)
                {
                    state.Next(17,false);
                    Assert.Equal(expected[i],state.Next(prices[i],false));
                    Assert.Equal(expected[i],state.Next(prices[i],true));
                }
            }
        }
    }
    [Fact]
    public void NarrowMonotoneIndexRetainsItsExactEndpointPosition()
    {
        using var state=new ExactVariableMovingAverageEngine(6);
        var expected=new ReferenceFraction(0); var six=new ReferenceFraction(6);
        for (var i=0;i<1200;i++)
        {
            expected=(expected*new ReferenceFraction(5)+new ReferenceFraction(i))/six;
            Assert.Equal(expected.ToDouble(),state.Next(i,true));
        }
    }
    [Fact]
    public void DeclaredHugePeriodIsLazyAndRejectedInputIsAtomic()
    {
        using var state=new ExactVariableMovingAverageEngine(int.MaxValue);
        using var control=new ExactVariableMovingAverageEngine(int.MaxValue);
        Assert.Equal(1,state.Next(1,true)); control.Next(1,true);
        foreach (var value in new[] { double.NaN,double.PositiveInfinity,double.NegativeInfinity })
            Assert.Throws<ArgumentOutOfRangeException>(()=>state.Next(value,true));
        Assert.Equal(control.Next(2,true),state.Next(2,true));
    }
    private static double[] Reference(double[] prices,int length)
    {
        var zero=new ReferenceFraction(0); var one=new ReferenceFraction(1); var n=new ReferenceFraction(length);
        ReferenceFraction R(double v)=>ReferenceFraction.FromDouble(v);
        var changes=prices.Select((v,i)=>i==0 ? zero : R(v)-R(prices[i-1])).ToArray();
        ReferenceFraction[] Smooth(ReferenceFraction[] values)
        {
            var result=new ReferenceFraction[values.Length]; var prior=zero;
            for (var i=0;i<values.Length;i++) result[i]=prior=(prior*(n-one)+values[i])/n;
            return result;
        }
        var up=Smooth(changes.Select(v=>v.Sign>0 ? v : zero).ToArray());
        var down=Smooth(changes.Select(v=>v.Sign<0 ? zero-v : zero).ToArray());
        var positive=Smooth(up.Select((v,i)=>(v+down[i]).Sign==0 ? zero : v/(v+down[i])).ToArray());
        var negative=Smooth(down.Select((v,i)=>(v+up[i]).Sign==0 ? zero : v/(v+up[i])).ToArray());
        var index=Smooth(positive.Select((v,i)=>(v+negative[i]).Sign==0 ? zero : (v-negative[i]).Abs()/(v+negative[i])).ToArray());
        var average=prices.Length==0 ? zero : R(prices[0]); var output=new double[prices.Length];
        for (var i=0;i<prices.Length;i++)
        {
            var sample=index.Skip(Math.Max(0,i-length+1)).Take(Math.Min(length,i+1)).ToArray();
            var low=sample.Min();var high=sample.Max();
            var gain=high.CompareTo(low)==0 ? zero : (index[i]-low)/(high-low)/n;
            average=(one-gain)*average+gain*R(prices[i]); output[i]=average.ToDouble();
        }
        return output;
    }
}

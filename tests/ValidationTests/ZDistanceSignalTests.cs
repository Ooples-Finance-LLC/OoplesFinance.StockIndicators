using System.Numerics;
using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Validation;
using Number = OoplesFinance.StockIndicators.Helpers.PeriodicChannelWindow.Number;

namespace OoplesFinance.StockIndicators.Tests.Unit.ValidationTests;

public sealed class ZDistanceSignalTests
{
    public static IEnumerable<object[]> Cases => ZDistanceNumericalTests.Cases;
    [Theory,MemberData(nameof(Cases))]
    public Task EveryConfigurationPassesNumericalFixtures(IndicatorValidationCase c) => new OrdinalFamilyNumericalTests().PublicConfigurationsPassEveryNumericalClass(c);
    [Theory,MemberData(nameof(Cases))]
    public void EveryOutputRejectsFaults(IndicatorValidationCase c) => new OrdinalFamilyNumericalTests().EveryPublishedOutputRejectsAnInjectedValueFault(c);
    private static Bar B(double value,double volume=1)=>new(DateTime.UnixEpoch,value,value,value,value,volume);
    private static StockData Data(Bar[] bars)=>new(bars.Select(b=>b.Open),bars.Select(b=>b.High),bars.Select(b=>b.Low),bars.Select(b=>b.Close),bars.Select(b=>b.Volume),bars.Select(b=>b.Time));
    [Fact]
    public void AllRadicalSignsAndPermutationsMatchIndependentElimination()
    {
        foreach(var a in new[]{0,1,2,4,9})foreach(var b in new[]{0,1,3,4,9})foreach(var c in new[]{0,1,5,4,9})
        foreach(var sa in new[]{-1,1})foreach(var sb in new[]{-1,1})foreach(var sc in new[]{-1,1})
        {
            var actual=ZDistanceRoot.SumSign(new(Number.Integer(a),sa),new(Number.Integer(b),sb),new(Number.Integer(c),sc));
            var expected=BuiltInFormulaReferences.ZDistanceReferenceSign((new ReferenceFraction(a),sa),(new ReferenceFraction(b),sb),(new ReferenceFraction(c),sc));
            Assert.Equal(expected,actual);
            var approximate=sa*Math.Sqrt(a)+sb*Math.Sqrt(b)+sc*Math.Sqrt(c);
            if(Math.Abs(approximate)>1e-12)Assert.Equal(Math.Sign(approximate),actual);
        }
    }
    [Fact]
    public void SubnormalSizedPerturbationsResolveExactRadicalTies()
    {
        var denominator=BigInteger.One<<2048;
        foreach(var offset in new[]{-1,0,1})
        {
            var square=Number.Integer(9*denominator+offset).Divide(Number.Integer(denominator));
            var one=new ZDistanceRoot(Number.Integer(1),1);var two=new ZDistanceRoot(Number.Integer(4),1);var three=new ZDistanceRoot(square,-1);
            Assert.Equal(-offset,ZDistanceRoot.SumSign(one,two,three));
            Assert.Equal(-offset,ZDistanceRoot.SumSign(one,three,two));
            Assert.Equal(-offset,ZDistanceRoot.SumSign(three,two,one));
            Assert.Equal(offset,ZDistanceRoot.SumSign(one.Times(-1),two.Times(-1),three.Times(-1)));
        }
    }
    [Fact]
    public void PublishedScoreTiesStillCastTheirExactSignal()
    {
        foreach(var scale in new[]{double.Epsilon,1d,Math.Pow(2,900)})foreach(var rising in new[]{false,true})
        {
            var bars=new[]{B(scale),B(3*scale),B(5*scale),B(7*scale,rising?Math.BitDecrement(1d):Math.BitIncrement(1d))};
            var result=Data(bars).CalculateZDistanceFromVwapIndicator(length:2);
            Assert.Equal(1,result.OutputValues["Zscore"][2]);Assert.Equal(1,result.OutputValues["Zscore"][3]);
            Assert.Equal(rising?Signal.StrongBuy:Signal.Sell,result.SignalsList[3]);
            Assert.Equal(BuiltInFormulaReferences.ZDistanceSignals(bars,length:2),result.SignalsList);
        }
    }
    [Theory]
    [InlineData(MovingAvgType.VolumeWeightedAveragePrice)]
    [InlineData(MovingAvgType.SimpleMovingAverage)]
    [InlineData(MovingAvgType.WeightedMovingAverage)]
    [InlineData(MovingAvgType.ExponentialMovingAverage)]
    [InlineData(MovingAvgType.WildersSmoothingMethod)]
    public void ExactSignalsPreservePreviewResetAndSelectedInputs(MovingAvgType kind)
    {
        var bars=Enumerable.Range(0,24).Select(i=>B((i%7-3)*(double.MaxValue/4),i%3==0?-double.MaxValue:double.Epsilon)).ToArray();
        var expected=BuiltInFormulaReferences.ZDistanceSignals(bars,kind,3);
        var result=Data(bars).CalculateZDistanceFromVwapIndicator(kind,3);Assert.Equal(expected,result.SignalsList);
        var selected=Data(bars.Select(_=>B(100)).ToArray());
        for(var i=0;i<bars.Length;i++)selected.Volumes[i]=bars[i].Volume;
        selected.SetCustomValues(bars.Select(b=>b.Close).ToList());selected.CalculateZDistanceFromVwapIndicator(kind,3);Assert.Equal(expected,selected.SignalsList);
        using var kernel=new ZDistanceWindow(kind,3);
        for(var pass=0;pass<2;pass++)
        {
            kernel.Next(17,9,true);kernel.Reset();Assert.Equal(Signal.None,kernel.LastSignal);
            for(var i=0;i<bars.Length;i++)
            {
                kernel.Next(-1,3,false);
                foreach(var final in new[]{false,false,true}){kernel.Next(bars[i].Close,bars[i].Volume,final);Assert.Equal(expected[i],kernel.LastSignal);}
            }
        }
    }
}

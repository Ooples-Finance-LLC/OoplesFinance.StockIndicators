using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Validation;

namespace OoplesFinance.StockIndicators.Tests.Unit.ModelsTests;

[Collection("IndicatorValuesDispatch")]
public sealed class StrengthSharedExecutionTests
{
    private static Bar[] Data(int count) => Enumerable.Range(0,count).Select(i =>
        new Bar(DateTime.UnixEpoch.AddMinutes(i),32+i%7,75+i%17,16+i%3,48+i%31+(i%7)/8d,i+1)).ToArray();
    private static IIndicator Create(int kind,int period,int lag=5,int unstable=0) => kind switch
    {
        <3 => new WilderStrengthOscillator(period,(WilderStrengthConvention)kind,unstable),
        8 => new SumSeededDirectionalIndex(Math.Max(2,period)),
        _ => new NullableStrengthOscillator(period,(NullableStrengthConvention)(kind-3),kind is 6 or 7 ? lag : 1)
    };
    private static async Task CheckModes(Bar[] bars,IIndicator owner)
    {
        var expected=Expected(bars,owner);
        foreach(var mode in Enum.GetValues<IndicatorHistoryMode>()) await CheckOwned(bars,owner,expected,mode);
    }
    public static IEnumerable<object[]> Kinds => Enumerable.Range(0,9).Select(k => new object[] { k });
    [Theory,MemberData(nameof(Kinds))]
    public async Task EveryConventionRetainsStartupRecurrenceAndOwnedColumns(int kind)
    {
        foreach(int period in new[] { 1,3,20,63 })
        foreach(int count in new[] { 0,1,2,19,20,21,65 })
            await CheckModes(Data(count),Create(kind,period));
        foreach(int count in new[] { 513,10000 }) await CheckModes(Data(count),Create(kind,20));
        await CheckReference(Data(65),Create(kind,3,2,2));
        var flat=Enumerable.Repeat(new Bar(default,10,10,10,10,1),65).ToArray();
        await CheckReference(flat,Create(kind,3));
        await CheckReference(flat.Concat(Data(65)).ToArray(),Create(kind,3));
        if(kind<3) Assert.Equal(kind==1 ? 100d : 0,Expected(flat,Create(kind,3))[0][^1]);
        else Assert.All(Expected(flat,Create(kind,3))[kind==8 ? 5 : 1],v => Assert.Equal(0d,v));
    }
    [Fact]
    public async Task HugePeriodsLagSuppressionAndScaledPricesStayExact()
    {
        foreach(int kind in Enumerable.Range(0,9))
        {
            await CheckModes(Data(65),Create(kind,int.MaxValue,int.MaxValue,int.MaxValue));
            foreach(double scale in new[] { double.Epsilon,Math.ScaleB(1d,400),1e-200,-0d })
                await CheckReference(Data(65).Select(b => new Bar(b.Time,b.Open*scale,b.High*scale,b.Low*scale,b.Close*scale,1)).ToArray(),Create(kind,3,2));
        }
        foreach(var convention in Enum.GetValues<WilderStrengthConvention>())
            await CheckModes(Data(65),new WilderStrengthOscillator(1,convention,int.MaxValue));
        var extremes=Enumerable.Range(0,65).Select(i => { double v=i%2==0 ? double.MaxValue : -double.MaxValue;return new Bar(default,v,double.MaxValue,-double.MaxValue,v,1); }).ToArray();
        foreach(int kind in Enumerable.Range(0,9)) await CheckReference(extremes,Create(kind,3,2));
    }
    [Theory]
    [InlineData(NullableStrengthConvention.RelativeStrength)]
    [InlineData(NullableStrengthConvention.RelativeMomentum)]
    public async Task UnboundedRatioOverflowRetainsInputPriority(NullableStrengthConvention convention)
    {
        var bars=Enumerable.Repeat(new Bar(default,1,1,1,1,1),513).ToArray();
        bars[0]=new(default,0,0,0,0,1);bars[1]=new(default,-double.Epsilon,-double.Epsilon,-double.Epsilon,-double.Epsilon,1);
        var owner=new NullableStrengthOscillator(2,convention);Assert.True(double.IsInfinity(Expected(bars,owner)[0][2]));
        foreach(var mode in Enum.GetValues<IndicatorHistoryMode>())
        {
            var shared=new StockIndicatorBuilder().ConfigureSource(Bars.From(bars)).ConfigureIndicators(owner).ConfigureHistory(mode);
            var ordinary=new StockIndicatorBuilder().ConfigureSource(Bars.From(bars)).ConfigureIndicators(owner).ConfigureHistory(mode).ConfigureBehavior(_ => { });
            var expected=await Assert.ThrowsAsync<IndicatorOutputException>(() => ordinary.BuildAsync());var actual=await Assert.ThrowsAsync<IndicatorOutputException>(() => shared.BuildAsync());Assert.Equal(expected.Message,actual.Message);
            var valid=bars[500];bars[500]=new(valid.Time,valid.Open,valid.High,valid.Low,valid.Close,double.NaN);
            await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => shared.BuildAsync());bars[500]=valid;
        }
        foreach(var bounded in new[] { NullableStrengthConvention.RelativeStrengthIndex,NullableStrengthConvention.RelativeMomentumIndex,NullableStrengthConvention.NetMomentum })
            await CheckReference(bars,new NullableStrengthOscillator(2,bounded));
    }
    [Fact]
    public async Task MissingAdxSeedNeverRecoversWhileDirectionalColumnsDo()
    {
        var bars=Enumerable.Repeat(new Bar(default,10,10,10,10,1),32).Concat(Data(1001)).ToArray();
        var owner=new SumSeededDirectionalIndex(3);var expected=Expected(bars,owner);
        Assert.Contains(1d,expected[5]);Assert.All(expected[8],v => Assert.Equal(0d,v));Assert.All(expected[9],v => Assert.Equal(0d,v));
        await CheckModes(bars,owner);await CheckReference(bars,owner);
    }
    [Fact]
    public async Task MixedChainedRootsRetainCancellationAndInputRecovery()
    {
        var bars=Data(513);var roots=new[] { Create(1,3),Create(3,3),Create(6,3,2),Create(8,3) };
        foreach(var mode in Enum.GetValues<IndicatorHistoryMode>())
        {
            var builder=new StockIndicatorBuilder().ConfigureSource(Bars.From(bars)).ConfigureIndicators(roots.Concat(new[] { roots[0] }).ToArray()).ConfigureHistory(mode);
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => builder.BuildAsync(new CancellationToken(true)));
            var valid=bars[124];bars[124]=new(valid.Time,valid.Open,valid.High,valid.Low,valid.Close,double.NaN);
            await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => builder.BuildAsync());bars[124]=valid;
            using var run=await builder.ConfigureExecution(IndicatorExecutionBackend.Auto).BuildAsync();Assert.Equal(IndicatorExecutionBackend.Cpu,builder.LastExecution!.Backend);
            foreach(var root in roots) CheckColumns(run,root,Expected(bars,root));
            await Assert.ThrowsAnyAsync<NotSupportedException>(() => builder.ConfigureExecution(IndicatorExecutionBackend.Gpu).BuildAsync());
        }
        foreach(var root in roots)
        {
            ((MultiOutputIndicatorBase)root).Of(new FirstValueEma(5));
            using var ordinary=await new StockIndicatorBuilder().ConfigureSource(Bars.From(bars)).ConfigureIndicators(root).ConfigureBehavior(_ => { }).BuildAsync();
            foreach(var mode in Enum.GetValues<IndicatorHistoryMode>()) await CheckOwned(bars,root,root.Outputs.Select(o => ordinary[o].ToArray()).ToArray(),mode);
        }
    }
    private static double[][] Expected(Bar[] bars,IIndicator owner)
    {
        object state=owner is IndicatorBase scalar ? scalar.CreateState() : ((MultiOutputIndicatorBase)owner).CreateState();
        using var lifetime=state as IDisposable;
        var result=Enumerable.Range(0,owner.Outputs.Count).Select(_ => new double[bars.Length]).ToArray();
        var values=new double[result.Length];
        for (int i=0;i<bars.Length;i++)
        {
            if (state is IIndicatorState single) values[0]=single.Update(bars[i]);
            else { Array.Clear(values); ((IMultiOutputState)state).Update(bars[i],values); }
            for (int slot=0;slot<values.Length;slot++) result[slot][i]=values[slot];
        }
        return result;
    }
    private static async Task CheckReference(Bar[] bars,IIndicator owner)
    {
        var expected=Expected(bars,owner);
        foreach (var rule in ((IIndicatorValidationContract)owner).ValidationRules)
            rule.Check(new IndicatorValidationContext("Strength shared execution",bars,expected,0));
        foreach (var mode in Enum.GetValues<IndicatorHistoryMode>()) await CheckOwned(bars,owner,expected,mode);
    }
    private static void CheckColumns(IIndicatorRun run,IIndicator owner,double[][] expected)
    {
        for (int slot=0;slot<expected.Length;slot++)
            Assert.Equal(expected[slot].Select(BitConverter.DoubleToInt64Bits),run[owner.Outputs[slot]].ToArray().Select(BitConverter.DoubleToInt64Bits));
    }
    private static async Task CheckOwned(Bar[] bars,IIndicator owner,double[][] expected,IndicatorHistoryMode mode)
    {
        var input=bars.ToArray();
        using var run=await new StockIndicatorBuilder().ConfigureSource(Bars.From(input)).ConfigureIndicators(owner).ConfigureHistory(mode).BuildAsync();
        Array.Clear(input); run.Dispose(); CheckColumns(run,owner,expected);
        if (bars.Length>0) Assert.Equal(bars[^1],run.Latest.Bar);
        if (mode==IndicatorHistoryMode.Full)
        {
            int row=0; await foreach (var snapshot in run) Assert.Equal(bars[row++],snapshot.Bar);
            Assert.Equal(bars.Length,row);
        }
    }
}

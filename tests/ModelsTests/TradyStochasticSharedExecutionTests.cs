using AiDotNet.Tensors.Helpers;
using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Validation;

namespace OoplesFinance.StockIndicators.Tests.Unit.ModelsTests;

[Collection("IndicatorValuesDispatch")]
public sealed class TradyStochasticSharedExecutionTests
{
    private static IIndicator Create(int kind,int period,int k=3,int d=3) => kind switch
    {
        0 => new FiftySeedStochastic(period),
        1 => new StochasticSmaKdj(period,k,d),
        2 => new StochasticSmaDifference(period,k,d),
        _ => new WindowStochasticMomentum(period)
    };
    private static Bar[] Data(int count) => Enumerable.Range(0,count).Select(i =>
    {
        double o=i%79==0 ? 0 : new[] { 16d,32,96,48,36 }[i%5]+(i%13)/8d;
        double c=i%67==0 ? 0 : new[] { 16d,48,240,120,90 }[i%5]+(i%17)/8d;
        return new Bar(DateTime.UnixEpoch.AddMinutes(i),o,Math.Max(o,c)+5,Math.Min(o,c)-5,c,i+1);
    }).ToArray();
    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(2)] [InlineData(3)]
    public async Task EveryColumnRetainsExactWorkerBoundariesAndOwnedHistory(int kind)
    {
        int prior=CpuParallelSettings.MaxDegreeOfParallelism;
        try
        {
            foreach (var (period,k,d) in new[] { (1,1,1),(3,1,3),(20,3,3),(3,20,5),(5,3,63) })
            foreach (int count in new[] { 511,512,513,1001 })
            {
                var bars=Data(count); var owner=Create(kind,period,k,d); var expected=Expected(bars,owner);
                foreach (int workers in new[] { 1,2,8 })
                foreach (var mode in Enum.GetValues<IndicatorHistoryMode>())
                {
                    CpuParallelSettings.MaxDegreeOfParallelism=workers;
                    await CheckOwned(bars,owner,expected,mode);
                }
            }
            var large=Data(10000); var largeOwner=Create(kind,20,3,3); var largeExpected=Expected(large,largeOwner);
            foreach (var mode in Enum.GetValues<IndicatorHistoryMode>()) await CheckOwned(large,largeOwner,largeExpected,mode);
        }
        finally { CpuParallelSettings.MaxDegreeOfParallelism=prior; }
    }
    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(2)] [InlineData(3)]
    public async Task StartupMaximumPeriodsAndScaledInputsMatchIndependentReference(int kind)
    {
        foreach (var (period,k,d) in new[] { (1,1,1),(3,5,2),(20,3,63),(int.MaxValue,1,1),(1,int.MaxValue,1),(1,1,int.MaxValue),(int.MaxValue,int.MaxValue,int.MaxValue) })
        foreach (int count in new[] { 0,1,2,3,4,19,20,21,65 })
            await CheckReference(Data(count),Create(kind,period,k,d));
        foreach (double scale in new[] { double.Epsilon,Math.ScaleB(1d,400),1e-200,-0d })
            await CheckReference(Data(129).Select(b => new Bar(b.Time,b.Open*scale,b.High*scale,b.Low*scale,b.Close*scale,1)).ToArray(),Create(kind,3));
        await CheckReference(Enumerable.Repeat(new Bar(default,10,15,5,10,1),513).ToArray(),Create(kind,20));
    }
    [Fact]
    public async Task SecondaryJOverflowRetainsInputPriority()
    {
        int prior=CpuParallelSettings.MaxDegreeOfParallelism;
        try
        {
            CpuParallelSettings.MaxDegreeOfParallelism=8;
            var bars=Enumerable.Repeat(new Bar(default,0,1,0,0,1),1001).ToArray();
            bars[125]=new(default,0,1,0,1e306,1);
            var owner=new StochasticSmaKdj(1,1,2);var values=Expected(bars,owner);
            Assert.All(values[0],v => Assert.True(double.IsFinite(v)));
            Assert.All(values[1],v => Assert.True(double.IsFinite(v)));
            Assert.True(double.IsInfinity(values[2][125]));
            foreach (var mode in Enum.GetValues<IndicatorHistoryMode>())
            {
                var shared=new StockIndicatorBuilder().ConfigureSource(Bars.From(bars)).ConfigureIndicators(owner).ConfigureHistory(mode);
                var ordinary=new StockIndicatorBuilder().ConfigureSource(Bars.From(bars)).ConfigureIndicators(owner).ConfigureHistory(mode).ConfigureBehavior(_ => { });
                var expected=await Assert.ThrowsAsync<IndicatorOutputException>(() => ordinary.BuildAsync());
                var actual=await Assert.ThrowsAsync<IndicatorOutputException>(() => shared.BuildAsync());Assert.Equal(expected.Message,actual.Message);
                var valid=bars[900];bars[900]=new(valid.Time,valid.Open,valid.High,valid.Low,valid.Close,double.NaN);
                await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => shared.BuildAsync());bars[900]=valid;
            }
        }
        finally { CpuParallelSettings.MaxDegreeOfParallelism=prior; }
    }
    [Fact]
    public async Task ChainedSourcesRetainHighLowWindows()
    {
        var bars=Data(1001);
        foreach (int kind in new[] { 0,1,2,3 })
        {
            var owner=Create(kind,3);
            if(owner is IndicatorBase scalar) scalar.Of(new FirstValueEma(5));
            else ((MultiOutputIndicatorBase)owner).Of(new FirstValueEma(5));
            using var ordinary=await new StockIndicatorBuilder().ConfigureSource(Bars.From(bars)).ConfigureIndicators(owner).ConfigureBehavior(_ => { }).BuildAsync();
            var expected=owner.Outputs.Select(o => ordinary[o].ToArray()).ToArray();
            foreach(var mode in Enum.GetValues<IndicatorHistoryMode>()) await CheckOwned(bars,owner,expected,mode);
        }
    }
    [Fact]
    public async Task MixedRootsPreserveValidationAndIdentity()
    {
        var bars=Data(1001);
        foreach (var roots in new[] { Enumerable.Range(0,4).Select(k => Create(k,3)).ToArray(),new IIndicator[] { Create(0,3,63),Create(0,63,3),new RollingPriceSum(7) } })
        {
            var expected=roots.Select(r => Expected(bars,r)).ToArray();
            foreach (var mode in Enum.GetValues<IndicatorHistoryMode>())
            {
                var builder=new StockIndicatorBuilder().ConfigureSource(Bars.From(bars)).ConfigureIndicators(roots.Concat(new[] { roots[0] }).ToArray()).ConfigureHistory(mode);
                await Assert.ThrowsAnyAsync<OperationCanceledException>(() => builder.BuildAsync(new CancellationToken(true)));
                var valid=bars[124]; bars[124]=new(valid.Time,valid.Open,valid.High,valid.Low,valid.Close,double.NaN);
                await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => builder.BuildAsync());
                bars[124]=valid;
                using var run=await builder.ConfigureExecution(IndicatorExecutionBackend.Auto).BuildAsync();
                Assert.Equal(IndicatorExecutionBackend.Cpu,builder.LastExecution!.Backend);
                for (int i=0;i<roots.Length;i++) CheckColumns(run,roots[i],expected[i]);
                await Assert.ThrowsAnyAsync<NotSupportedException>(() => builder.ConfigureExecution(IndicatorExecutionBackend.Gpu).BuildAsync());
            }
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
            rule.Check(new IndicatorValidationContext("Trady stochastic shared execution",bars,expected,0));
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

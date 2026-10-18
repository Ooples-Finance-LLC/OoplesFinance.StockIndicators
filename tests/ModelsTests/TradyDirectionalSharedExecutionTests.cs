using AiDotNet.Tensors.Helpers;
using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Validation;

namespace OoplesFinance.StockIndicators.Tests.Unit.ModelsTests;

[Collection("IndicatorValuesDispatch")]
public sealed class TradyDirectionalSharedExecutionTests
{
    public static IEnumerable<object[]> Measures => Enum.GetValues<DirectionalWindowMeasure>().Select(m => new object[] { m });
    private static Bar[] Data(int count) => Enumerable.Range(0,count).Select(i =>
    {
        double c=i%96<32 ? 100+i%32 : i%96<64 ? 100 : 132-i%32;
        return new Bar(DateTime.UnixEpoch.AddMinutes(i),c,c+5,c-5,c,i+1);
    }).ToArray();

    [Theory,MemberData(nameof(Measures))]
    public async Task SharedValuesAndPresenceSurviveWorkersAndOwnedLifecycle(DirectionalWindowMeasure measure)
    {
        int prior=CpuParallelSettings.MaxDegreeOfParallelism;
        try
        {
            foreach (int period in new[] { 1,20 })
            foreach (int count in new[] { 511,512,513,1001 })
            {
                var bars=Data(count);
                var owner=new WindowDirectionalMeasure(measure,period,3);
                var expected=Expected(bars,owner);
                foreach (int workers in new[] { 1,2,8 })
                foreach (var mode in Enum.GetValues<IndicatorHistoryMode>())
                {
                    CpuParallelSettings.MaxDegreeOfParallelism=workers;
                    await CheckOwned(bars,owner,expected,mode);
                }
            }
            await CheckReference(Data(10000),new WindowDirectionalMeasure(measure,20,20));
        }
        finally { CpuParallelSettings.MaxDegreeOfParallelism=prior; }
    }

    [Theory,MemberData(nameof(Measures))]
    public async Task StartupHugePeriodsTinyAndHugePricesMatchIndependentReference(DirectionalWindowMeasure measure)
    {
        foreach (var (period,lag) in new[] { (1,0),(3,1),(20,63),(int.MaxValue,0),(3,int.MaxValue) })
        foreach (int count in new[] { 0,1,2,3,4,20,65 })
            await CheckReference(Data(count),new WindowDirectionalMeasure(measure,period,lag));
        foreach (double scale in new[] { double.Epsilon,Math.ScaleB(1d,1016),1e-200,-0d })
            await CheckReference(Data(513).Select(b => new Bar(b.Time,b.Open*scale,b.High*scale,b.Low*scale,b.Close*scale,1)).ToArray(),new WindowDirectionalMeasure(measure,3,5));
        await CheckReference(Enumerable.Repeat(new Bar(default,10,15,5,10,1),513).ToArray(),new WindowDirectionalMeasure(measure,3,0));
    }

    [Theory]
    [InlineData(DirectionalWindowMeasure.PositiveMovement)]
    [InlineData(DirectionalWindowMeasure.NegativeMovement)]
    public async Task RawWorkerFirstRowRetainsPredecessorAndInputValidationPrecedesOverflow(DirectionalWindowMeasure measure)
    {
        int prior=CpuParallelSettings.MaxDegreeOfParallelism;
        try
        {
            var owner=new WindowDirectionalMeasure(measure,int.MaxValue,int.MaxValue);
            foreach (int boundary in new[] { 125,250,500,875 })
            {
                var bars=Data(1001);
                bars[boundary-1]=new(default,0,100,-100,0,1);
                bars[boundary]=new(default,0,107,-111,0,1);
                var expected=Expected(bars,owner);
                Assert.Equal(measure==DirectionalWindowMeasure.PositiveMovement ? 7d : 11d,expected[0][boundary]);
                Assert.Equal(1d,expected[1][boundary]);
                foreach (int workers in new[] { 1,2,8 })
                foreach (var mode in Enum.GetValues<IndicatorHistoryMode>())
                {
                    CpuParallelSettings.MaxDegreeOfParallelism=workers;
                    await CheckOwned(bars,owner,expected,mode);
                }
            }
            CpuParallelSettings.MaxDegreeOfParallelism=8;
            var overflow=Data(1001);
            overflow[124]=new(default,0,double.MaxValue,-double.MaxValue,0,1);
            overflow[125]=new(default,0,-double.MaxValue,double.MaxValue,0,1);
            foreach (var mode in Enum.GetValues<IndicatorHistoryMode>())
            {
                var shared=new StockIndicatorBuilder().ConfigureSource(Bars.From(overflow)).ConfigureIndicators(owner).ConfigureHistory(mode);
                var ordinary=new StockIndicatorBuilder().ConfigureSource(Bars.From(overflow)).ConfigureIndicators(owner).ConfigureHistory(mode).ConfigureBehavior(_ => { });
                var expected=await Assert.ThrowsAsync<IndicatorOutputException>(() => ordinary.BuildAsync());
                var actual=await Assert.ThrowsAsync<IndicatorOutputException>(() => shared.BuildAsync());
                Assert.Equal(expected.Message,actual.Message);
                var valid=overflow[900]; overflow[900]=new(valid.Time,valid.Open,valid.High,valid.Low,valid.Close,double.NaN);
                await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => shared.BuildAsync());
                overflow[900]=valid;
            }
        }
        finally { CpuParallelSettings.MaxDegreeOfParallelism=prior; }
    }

    [Fact]
    public async Task MixedRootsRetainEveryPresenceColumnAndRecoverAfterCancellationAndInvalidVolume()
    {
        var bars=Data(1001);
        var roots=Enum.GetValues<DirectionalWindowMeasure>().Select(m => new WindowDirectionalMeasure(m,3,5)).ToArray();
        var expected=roots.Select(r => Expected(bars,r)).ToArray();
        foreach (var mode in Enum.GetValues<IndicatorHistoryMode>())
        {
            var builder=new StockIndicatorBuilder().ConfigureSource(Bars.From(bars)).ConfigureIndicators(roots.Cast<IIndicator>().Concat(new[] { roots[0] }).ToArray()).ConfigureHistory(mode);
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

    private static double[][] Expected(Bar[] bars,WindowDirectionalMeasure owner)
    {
        var state=(IMultiOutputState)owner.CreateState();
        var expected=new[] { new double[bars.Length],new double[bars.Length] };
        Span<double> values=stackalloc double[2];
        for (int i=0;i<bars.Length;i++) { state.Update(bars[i],values); expected[0][i]=values[0]; expected[1][i]=values[1]; }
        return expected;
    }
    private static async Task CheckReference(Bar[] bars,WindowDirectionalMeasure owner)
    {
        var expected=Expected(bars,owner);
        foreach (var rule in owner.ValidationRules)
            rule.Check(new IndicatorValidationContext("Trady directional shared execution",bars,expected,0));
        foreach (var mode in Enum.GetValues<IndicatorHistoryMode>()) await CheckOwned(bars,owner,expected,mode);
    }
    private static void CheckColumns(IIndicatorRun run,WindowDirectionalMeasure owner,double[][] expected)
    {
        for (int slot=0;slot<2;slot++)
            Assert.Equal(expected[slot].Select(BitConverter.DoubleToInt64Bits),run[owner.Outputs[slot]].ToArray().Select(BitConverter.DoubleToInt64Bits));
    }
    private static async Task CheckOwned(Bar[] bars,WindowDirectionalMeasure owner,double[][] expected,IndicatorHistoryMode mode)
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

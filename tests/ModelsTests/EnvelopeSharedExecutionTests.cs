using AiDotNet.Tensors.Helpers;
using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Validation;

namespace OoplesFinance.StockIndicators.Tests.Unit.ModelsTests;

[Collection("IndicatorValuesDispatch")]
public sealed class EnvelopeSharedExecutionTests
{
    private static Bar[] Data(int count) => Enumerable.Range(0,count).Select(i =>
        new Bar(DateTime.UnixEpoch.AddMinutes(i),32+i%7,75+i%17,16+i%3,48+i%31+(i%7)/8d,i+1)).ToArray();
    public static IEnumerable<object[]> Averages => Enum.GetValues<EnvelopeAverage>().Select(m => new object[] { m });
    [Theory,MemberData(nameof(Averages))]
    public async Task AverageModesRetainWorkerBoundariesAndAllColumns(EnvelopeAverage average)
    {
        int prior=CpuParallelSettings.MaxDegreeOfParallelism;
        try
        {
            foreach (int period in new[] { 3,20,63 })
            foreach (int count in new[] { 511,512,513,1001 })
            {
                var bars=Data(count);var owner=new WindowAverageEnvelope(period,2.5,average);var expected=Expected(bars,owner);
                foreach (int workers in new[] { 1,2,8 })
                {
                    CpuParallelSettings.MaxDegreeOfParallelism=workers;
                    foreach (var mode in Enum.GetValues<IndicatorHistoryMode>()) await CheckOwned(bars,owner,expected,mode);
                }
            }
            var large=Data(10000);var largeOwner=new WindowAverageEnvelope(20,2.5,average);var largeExpected=Expected(large,largeOwner);
            foreach (var mode in Enum.GetValues<IndicatorHistoryMode>()) await CheckOwned(large,largeOwner,largeExpected,mode);
            foreach (double width in new[] { double.Epsilon,Math.BitDecrement(100d),100d,150d })
                await CheckReference(Data(65).Select(b => new Bar(b.Time,-b.Open,-b.Low,-b.High,-b.Close,1)).ToArray(),new WindowAverageEnvelope(3,width,average));
        }
        finally { CpuParallelSettings.MaxDegreeOfParallelism=prior; }
    }
    [Theory]
    [InlineData(AtrBandCenterMode.Simple)]
    [InlineData(AtrBandCenterMode.MeanSeededExponential)]
    [InlineData(AtrBandCenterMode.FirstSeededExponential)]
    public async Task AtrCentersRetainEveryPresenceColumnAndDisabledWidth(AtrBandCenterMode center)
    {
        foreach (bool width in new[] { false,true })
        foreach (var (first,second) in new[] { (1,1),(3,20),(20,3),(20,20) })
        foreach (double multiplier in new[] { -2d,0,2 })
        {
            var bars=Data(513);var owner=new WindowAtrBands(first,second,multiplier,center,width);var expected=Expected(bars,owner);
            foreach (var mode in Enum.GetValues<IndicatorHistoryMode>()) await CheckOwned(bars,owner,expected,mode);
        }
        foreach (bool width in new[] { false,true })
        {
            var owner=new WindowAtrBands(20,20,2,center,width);var bars=Data(10000);var expected=Expected(bars,owner);
            foreach (var mode in Enum.GetValues<IndicatorHistoryMode>()) await CheckOwned(bars,owner,expected,mode);
            await CheckReference(Data(65),new WindowAtrBands(3,5,-2,center,width));
            await CheckReference(Enumerable.Repeat(new Bar(default,0,1,-1,0,1),65).ToArray(),new WindowAtrBands(3,5,2,center,width));
        }
    }
    [Fact]
    public async Task StartupHugePeriodsScaledAndSignedZeroInputs()
    {
        foreach (var average in Enum.GetValues<EnvelopeAverage>())
        foreach (int period in new[] { average is EnvelopeAverage.Alma or EnvelopeAverage.Hma or EnvelopeAverage.Epma ? 2 : 1,20,4097,int.MaxValue })
        foreach (int count in new[] { 0,1,2,5,21 })
        {
            var bars=Data(count);var owner=new WindowAverageEnvelope(period,2.5,average);var expected=Expected(bars,owner);
            foreach (var mode in Enum.GetValues<IndicatorHistoryMode>()) await CheckOwned(bars,owner,expected,mode);
        }
        foreach (var center in Enum.GetValues<AtrBandCenterMode>())
        foreach (var (first,second) in new[] { (1,int.MaxValue),(int.MaxValue,1) })
        {
            var bars=Data(21);var owner=new WindowAtrBands(first,second,2,center);var expected=Expected(bars,owner);
            foreach (var mode in Enum.GetValues<IndicatorHistoryMode>()) await CheckOwned(bars,owner,expected,mode);
        }
        foreach (double scale in new[] { double.Epsilon,Math.ScaleB(1d,400),1e-200,-0d })
        {
            var bars=Data(65).Select(b => new Bar(b.Time,b.Open*scale,b.High*scale,b.Low*scale,b.Close*scale,1)).ToArray();
            await CheckReference(bars,new WindowAverageEnvelope(3));
            await CheckReference(bars,new WindowAtrBands(3,5));
        }
    }
    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task SecondaryOverflowRetainsRawInputPriority(bool atr)
    {
        var bars=Enumerable.Repeat(atr ? new Bar(default,double.Epsilon,1,0,double.Epsilon,1)
            : new Bar(default,double.MaxValue,double.MaxValue,double.MaxValue,double.MaxValue,1),513).ToArray();
        IIndicator owner=atr ? new WindowAtrBands(1,1) : new WindowAverageEnvelope(3);
        var values=Expected(bars,owner);
        Assert.All(values[0],v => Assert.True(double.IsFinite(v)));
        Assert.Contains(values[atr ? 3 : 1],double.IsInfinity);
        foreach (var mode in Enum.GetValues<IndicatorHistoryMode>())
        {
            var shared=new StockIndicatorBuilder().ConfigureSource(Bars.From(bars)).ConfigureIndicators(owner).ConfigureHistory(mode);
            var ordinary=new StockIndicatorBuilder().ConfigureSource(Bars.From(bars)).ConfigureIndicators(owner).ConfigureHistory(mode).ConfigureBehavior(_ => { });
            var expected=await Assert.ThrowsAsync<IndicatorOutputException>(() => ordinary.BuildAsync());
            var actual=await Assert.ThrowsAsync<IndicatorOutputException>(() => shared.BuildAsync());Assert.Equal(expected.Message,actual.Message);
            var valid=bars[500];bars[500]=new(valid.Time,valid.Open,valid.High,valid.Low,valid.Close,double.NaN);
            await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => shared.BuildAsync());bars[500]=valid;
        }
    }
    [Fact]
    public async Task MixedChainedCancellationAndInputRecovery()
    {
        var bars=Data(513);
        var roots=new IIndicator[] { new WindowAverageEnvelope(3,2.5,EnvelopeAverage.Hma),new WindowAtrBands(3,5),new WindowAtrBands(5,3,2,AtrBandCenterMode.Simple,false) };
        foreach (var mode in Enum.GetValues<IndicatorHistoryMode>())
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
            rule.Check(new IndicatorValidationContext("Envelope shared execution",bars,expected,0));
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

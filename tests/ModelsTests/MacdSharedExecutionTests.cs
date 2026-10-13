using AiDotNet.Tensors.Helpers;
using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Validation;

namespace OoplesFinance.StockIndicators.Tests.Unit.ModelsTests;

[Collection("IndicatorValuesDispatch")]
public sealed class MacdSharedExecutionTests
{
    private static Bar[] Data(int count) => Enumerable.Range(0,count).Select(i =>
        new Bar(DateTime.UnixEpoch.AddMinutes(i),32+i%7,75+i%17,16+i%3,48+i%31+(i%7)/8d,i%11==0 ? 0 : i+1)).ToArray();
    public static IEnumerable<object[]> Methods => Enum.GetValues<ClassicAverageMethod>().Select(m => new object[] { m });
    private static async Task CheckModes(Bar[] bars,IIndicator owner)
    {
        var expected=Expected(bars,owner);
        foreach(var mode in Enum.GetValues<IndicatorHistoryMode>()) await CheckOwned(bars,owner,expected,mode);
    }
    [Theory,MemberData(nameof(Methods))]
    public async Task ClassicalMethodsRetainAlignedStartupWorkersAndOwnership(ClassicAverageMethod method)
    {
        int prior=CpuParallelSettings.MaxDegreeOfParallelism;
        try
        {
            bool finite=method is ClassicAverageMethod.Sma or ClassicAverageMethod.Wma or ClassicAverageMethod.Trima;
            foreach(var (seed,suppression) in new[] { (false,0),(true,2) })
            foreach(int count in finite ? new[] { 511,512,513,1001 } : new[] { 513 })
            {
                var bars=Data(count);var owner=new ClassicMacd(3,20,5,method,method,method,seed,suppression);var expected=Expected(bars,owner);
                foreach(int workers in new[] { 1,2,8 })
                {
                    CpuParallelSettings.MaxDegreeOfParallelism=workers;
                    foreach(var mode in Enum.GetValues<IndicatorHistoryMode>()) await CheckOwned(bars,owner,expected,mode);
                }
            }
            await CheckModes(Data(10000),new ClassicMacd(12,26,9,method,method,method));
            await CheckReference(Data(65),new ClassicMacd(3,5,2,method,method,method,true,2));
        }
        finally { CpuParallelSettings.MaxDegreeOfParallelism=prior; }
    }
    [Fact]
    public async Task MixedFiniteStagesRetainReversedPeriodsAndIdentity()
    {
        int prior=CpuParallelSettings.MaxDegreeOfParallelism;
        try
        {
            CpuParallelSettings.MaxDegreeOfParallelism=8;
            foreach(var f in new[] { ClassicAverageMethod.Sma,ClassicAverageMethod.Wma,ClassicAverageMethod.Trima })
            foreach(var s in new[] { ClassicAverageMethod.Sma,ClassicAverageMethod.Wma,ClassicAverageMethod.Trima })
            foreach(var g in new[] { ClassicAverageMethod.Sma,ClassicAverageMethod.Wma,ClassicAverageMethod.Trima })
                await CheckModes(Data(1001),new ClassicMacd(20,3,5,f,s,g));
            await CheckModes(Data(513),new ClassicMacd(1,1,1,ClassicAverageMethod.Mama,ClassicAverageMethod.T3,ClassicAverageMethod.Kama,true,2));
        }
        finally { CpuParallelSettings.MaxDegreeOfParallelism=prior; }
    }
    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task AlignedEmaSelectionsRetainSeedsSuppressionAndFixedCoefficients(bool fixedCoefficients)
    {
        foreach(bool first in new[] { false,true })
        foreach(int suppression in new[] { 0,2 })
        foreach(int selection in Enumerable.Range(1,7))
            await CheckModes(Data(513),new AlignedEmaMacd(12,26,3,first,suppression,fixedCoefficients,(EmaDifferenceSelection)selection));
        await CheckModes(Data(10000),new AlignedEmaMacd(fixedCoefficients:fixedCoefficients));
        await CheckReference(Data(65),new AlignedEmaMacd(12,26,3,true,2,fixedCoefficients));
    }
    [Fact]
    public async Task EveryEmaSelectionRetainsPriceVolumePercentageAndAllTenColumns()
    {
        foreach(bool first in new[] { false,true })
        foreach(bool percentage in new[] { false,true })
        foreach(bool volume in new[] { false,true })
        foreach(int selection in Enumerable.Range(1,31))
            await CheckModes(Data(65),new EmaDifferenceSignal(3,5,2,first,percentage,volume,(EmaDifferenceSelection)selection));
        await CheckModes(Data(10000),new EmaDifferenceSignal());
        await CheckModes(Data(10000),new EmaDifferenceSignal(percentage:true,volume:true));
        var zero=Enumerable.Repeat(new Bar(default,0,0,0,0,0),65).Concat(Data(65)).ToArray();
        await CheckReference(zero,new EmaDifferenceSignal(3,5,2,true,true,true));
    }
    [Fact]
    public async Task StartupHugePeriodsAndScaledInputsRetainExactReferences()
    {
        foreach(int period in new[] { 2,3,4097,int.MaxValue })
        foreach(int count in new[] { 0,1,3,21 })
        foreach(IIndicator owner in new IIndicator[] { new ClassicMacd(1,period,period),new AlignedEmaMacd(2,period,period),new EmaDifferenceSignal(1,period,period) })
            await CheckModes(Data(count),owner);
        foreach(double scale in new[] { double.Epsilon,Math.ScaleB(1d,400),1e-200,-0d })
        foreach(IIndicator owner in new IIndicator[] { new ClassicMacd(3,5,2),new AlignedEmaMacd(2,5,2),new EmaDifferenceSignal(3,5,2) })
            await CheckReference(Data(65).Select(b => new Bar(b.Time,b.Open*scale,b.High*scale,b.Low*scale,b.Close*scale,b.Volume*scale)).ToArray(),owner);
    }
    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task HiddenOverflowCancelsAndSelectedOverflowKeepsInputPriority(bool aligned)
    {
        var bars=Enumerable.Range(0,1001).Select(i => { double v=i<19 ? -double.MaxValue : double.MaxValue;return new Bar(default,v,v,v,v,1); }).ToArray();
        IIndicator hidden=aligned ? new AlignedEmaMacd(2,20,1,true,selection:EmaDifferenceSelection.Histogram)
            : new EmaDifferenceSignal(1,10,1,true,selection:EmaDifferenceSelection.Histogram);
        await CheckModes(bars,hidden);Assert.All(Expected(bars,hidden)[2],v => Assert.Equal(0d,v));
        IIndicator selected=aligned ? new AlignedEmaMacd(2,20,1,true) : new EmaDifferenceSignal(1,10,1,true);
        foreach(var mode in Enum.GetValues<IndicatorHistoryMode>())
        {
            var shared=new StockIndicatorBuilder().ConfigureSource(Bars.From(bars)).ConfigureIndicators(selected).ConfigureHistory(mode);
            var ordinary=new StockIndicatorBuilder().ConfigureSource(Bars.From(bars)).ConfigureIndicators(selected).ConfigureHistory(mode).ConfigureBehavior(_ => { });
            var expected=await Assert.ThrowsAsync<IndicatorOutputException>(() => ordinary.BuildAsync());var actual=await Assert.ThrowsAsync<IndicatorOutputException>(() => shared.BuildAsync());Assert.Equal(expected.Message,actual.Message);
            var valid=bars[900];bars[900]=new(valid.Time,valid.Open,valid.High,valid.Low,valid.Close,double.NaN);
            await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => shared.BuildAsync());bars[900]=valid;
        }
    }
    [Fact]
    public async Task MixedChainedRootsRetainCancellationInputRecoveryAndIdentity()
    {
        var bars=Data(513);var roots=new IIndicator[] { new ClassicMacd(3,5,2),new AlignedEmaMacd(2,5,2),new EmaDifferenceSignal(3,5,2,selection:EmaDifferenceSelection.Histogram) };
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
    [Fact]
    public async Task ClassicalBoundedOverflowRetainsInputPriority()
    {
        var bars=Enumerable.Range(0,1001).Select(i => { double v=i<19 ? -double.MaxValue : double.MaxValue;return new Bar(default,v,v,v,v,1); }).ToArray();
        var owner=new ClassicMacd(1,3,1);Assert.True(double.IsInfinity(Expected(bars,owner)[0][19]));
        foreach(var mode in Enum.GetValues<IndicatorHistoryMode>())
        {
            var shared=new StockIndicatorBuilder().ConfigureSource(Bars.From(bars)).ConfigureIndicators(owner).ConfigureHistory(mode);
            var ordinary=new StockIndicatorBuilder().ConfigureSource(Bars.From(bars)).ConfigureIndicators(owner).ConfigureHistory(mode).ConfigureBehavior(_ => { });
            var expected=await Assert.ThrowsAsync<IndicatorOutputException>(() => ordinary.BuildAsync());var actual=await Assert.ThrowsAsync<IndicatorOutputException>(() => shared.BuildAsync());Assert.Equal(expected.Message,actual.Message);
            var valid=bars[900];bars[900]=new(valid.Time,valid.Open,valid.High,valid.Low,valid.Close,double.NaN);
            await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => shared.BuildAsync());bars[900]=valid;
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
            rule.Check(new IndicatorValidationContext("MACD shared execution",bars,expected,0));
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

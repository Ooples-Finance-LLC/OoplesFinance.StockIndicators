using AiDotNet.Tensors.Helpers;
using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Validation;

namespace OoplesFinance.StockIndicators.Tests.Unit.ModelsTests;

[Collection("IndicatorValuesDispatch")]
public sealed class ClassicAverageSharedExecutionTests
{
    private static IIndicator Create(int kind, ClassicAverageMethod method,int first=3,int second=20,bool seed=false,int suppression=0)
        => kind==0 ? new ClassicMovingAverage(first,method,seed,suppression)
            : new ClassicPriceOscillator(first,second,method,kind==2,seed,suppression);
    private static Bar[] Data(int count) => Enumerable.Range(0,count).Select(i =>
        new Bar(DateTime.UnixEpoch.AddMinutes(i),32+i%7,75+i%17,16+i%3,48+i%31+(i%7)/8d,i+1)).ToArray();
    public static IEnumerable<object[]> Methods => Enum.GetValues<ClassicAverageMethod>().Select(m => new object[] { m });
    [Theory,MemberData(nameof(Methods))]
    public async Task MethodsRetainExactWorkersStartupAndOwnership(ClassicAverageMethod method)
    {
        int prior=CpuParallelSettings.MaxDegreeOfParallelism;
        try
        {
            bool finite=method is ClassicAverageMethod.Sma or ClassicAverageMethod.Wma or ClassicAverageMethod.Trima;
            foreach (int kind in new[] { 0,1,2 })
            foreach (var (first,second,seed,suppression) in new[] { (3,20,false,0),(20,3,true,2) })
            foreach (int count in finite ? new[] { 511,512,513,1001 } : new[] { 513 })
            {
                var bars=Data(count);var owner=Create(kind,method,first,second,seed,suppression);var expected=Expected(bars,owner);
                foreach (int workers in new[] { 1,2,8 })
                {
                    CpuParallelSettings.MaxDegreeOfParallelism=workers;
                    foreach (var mode in Enum.GetValues<IndicatorHistoryMode>()) await CheckOwned(bars,owner,expected,mode);
                }
            }
            if (finite)
                foreach (int kind in new[] { 0,1,2 })
                {
                    var bars=Data(10000);var owner=Create(kind,method,12,26);
                    foreach (var mode in Enum.GetValues<IndicatorHistoryMode>()) await CheckOwned(bars,owner,Expected(bars,owner),mode);
                }
        }
        finally { CpuParallelSettings.MaxDegreeOfParallelism=prior; }
    }
    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(2)]
    public async Task EdgePeriodsAndIndependentReferences(int kind)
    {
        foreach (var method in Enum.GetValues<ClassicAverageMethod>())
        {
            foreach (var (first,second) in new[] { (1,1),(1,3),(3,3),(int.MaxValue,1),(1,int.MaxValue) })
            foreach (int count in new[] { 0,1,2,5,21 })
            {
                var owner=Create(kind,method,first,second,true,2);var bars=Data(count);
                foreach (var mode in Enum.GetValues<IndicatorHistoryMode>()) await CheckOwned(bars,owner,Expected(bars,owner),mode);
            }
            await CheckReference(Data(65),Create(kind,method,3,5,true,2));
        }
        foreach (var method in new[] { ClassicAverageMethod.Sma,ClassicAverageMethod.Wma,ClassicAverageMethod.Trima })
        foreach (double scale in new[] { double.Epsilon,Math.ScaleB(1d,400),1e-200,-0d })
            await CheckReference(Data(513).Select(b => new Bar(b.Time,b.Open*scale,b.High*scale,b.Low*scale,b.Close*scale,1)).ToArray(),Create(kind,method,3,5));
    }
    [Fact]
    public async Task OverflowAndLaterInvalidInputRetainOrdinaryPriority()
    {
        var bars=Enumerable.Repeat(new Bar(default,1,1,1,1,1),1001).ToArray();
        bars[124]=new(default,-double.MaxValue,-double.MaxValue,-double.MaxValue,-double.MaxValue,1);
        bars[123]=bars[124];
        bars[125]=new(default,double.MaxValue,double.MaxValue,double.MaxValue,double.MaxValue,1);
        var owner=new ClassicPriceOscillator(1,3);
        Assert.Contains(Expected(bars,owner)[0],double.IsInfinity);
        foreach (var mode in Enum.GetValues<IndicatorHistoryMode>())
        {
            var shared=new StockIndicatorBuilder().ConfigureSource(Bars.From(bars)).ConfigureIndicators(owner).ConfigureHistory(mode);
            var ordinary=new StockIndicatorBuilder().ConfigureSource(Bars.From(bars)).ConfigureIndicators(owner).ConfigureHistory(mode).ConfigureBehavior(_ => { });
            var expected=await Assert.ThrowsAsync<IndicatorOutputException>(() => ordinary.BuildAsync());
            var actual=await Assert.ThrowsAsync<IndicatorOutputException>(() => shared.BuildAsync());
            Assert.Equal(expected.Message,actual.Message);
            var valid=bars[900];bars[900]=new(valid.Time,valid.Open,valid.High,valid.Low,valid.Close,double.NaN);
            await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => shared.BuildAsync());bars[900]=valid;
        }
    }
    [Fact]
    public async Task MixedAndChainedRootsRetainContractsAndRecovery()
    {
        var bars=Data(513);
        var roots=new IIndicator[] { Create(0,ClassicAverageMethod.Mama),Create(1,ClassicAverageMethod.Sma),Create(2,ClassicAverageMethod.T3) };
        foreach (var mode in Enum.GetValues<IndicatorHistoryMode>())
        {
            var builder=new StockIndicatorBuilder().ConfigureSource(Bars.From(bars)).ConfigureIndicators(roots.Concat(new[] { roots[0] }).ToArray()).ConfigureHistory(mode);
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => builder.BuildAsync(new CancellationToken(true)));
            using var run=await builder.ConfigureExecution(IndicatorExecutionBackend.Auto).BuildAsync();
            Assert.Equal(IndicatorExecutionBackend.Cpu,builder.LastExecution!.Backend);
            foreach(var root in roots) CheckColumns(run,root,Expected(bars,root));
            await Assert.ThrowsAnyAsync<NotSupportedException>(() => builder.ConfigureExecution(IndicatorExecutionBackend.Gpu).BuildAsync());
        }
        foreach (var root in roots)
        {
            ((MultiOutputIndicatorBase)root).Of(new FirstValueEma(5));
            using var ordinary=await new StockIndicatorBuilder().ConfigureSource(Bars.From(bars)).ConfigureIndicators(root).ConfigureBehavior(_ => { }).BuildAsync();
            foreach (var mode in Enum.GetValues<IndicatorHistoryMode>()) await CheckOwned(bars,root,root.Outputs.Select(o => ordinary[o].ToArray()).ToArray(),mode);
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
            rule.Check(new IndicatorValidationContext("Average oscillator shared execution",bars,expected,0));
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

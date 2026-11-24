using System.Reflection;
using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Validation;

namespace OoplesFinance.StockIndicators.Tests.Unit.ModelsTests;

public sealed class MassNormalizedStageExecutionTests
{
    private static readonly double[] Alphas = [.2, .1, .4, .6];
    private static IIndicatorState Create(bool quadruple) => (IIndicatorState)(quadruple
        ? (IndicatorBase)new MassNormalizedQuadrupleEma(Alphas[0], Alphas[1], Alphas[2], Alphas[3])
        : new MassNormalizedEma(Alphas[0])).CreateState();

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void CutoffTransitionsAndResetMatchIndependentReference(bool quadruple)
    {
        var state = Create(quadruple);
        var prices = Enumerable.Range(0, 400).Select(i => i % 23 == 0 ? -0d
            : 100 + (i * 17 % 43) / 8d).ToArray();
        foreach (var input in new[] { prices, prices.Reverse().ToArray() })
        {
            var bars = input.Select(v => new Bar(default, 0, 1, -1, v, 1)).ToArray();
            var expected = MassNormalizedReference.Calculate(bars, quadruple ? Alphas : [Alphas[0]]);
            state.Reset();
            for (int i = 0; i < bars.Length; i++)
                Bits(expected[i], state.Update(in bars[i]));
        }
    }

    [Fact]
    public void SteadyStateNegativeUnderflowRetainsItsSign()
    {
        var state = new MassNormalizedStage(.2);
        for (int i = 0; i < 1000; i++) state.Next(0);
        Bits(-0d, state.Next(-double.Epsilon));
        Bits(0d, state.Next(0));
    }

    [Fact]
    public void FirstUpdateIsExactEvenWhenAlphaTimesInputWouldUnderflow()
    {
        foreach (double alpha in new[] { double.Epsilon, .1, Math.BitDecrement(1d), 1d })
        foreach (double input in new[] { double.Epsilon, -double.Epsilon, double.MaxValue, -double.MaxValue, -0d })
        {
            var state = new MassNormalizedStage(alpha);
            Bits(input == 0 ? 0 : input, state.Next(input));
            state.Reset();
            Bits(input == 0 ? 0 : -input, state.Next(-input));
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void OrdinarySteadyStateUpdatesAllocateNoPerBarObjects(bool quadruple)
    {
        var state = Create(quadruple);
        var bars = Enumerable.Range(0, 97).Select(i => new Bar(default, 0, 1, -1, 100 + i / 8d, 1)).ToArray();
        double result = 0;
        for (int i = 0; i < 1000; i++) result = state.Update(in bars[i % bars.Length]);
        var before = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 0; i < 4000; i++) result = state.Update(in bars[i % bars.Length]);
        Assert.Equal(0, GC.GetAllocatedBytesForCurrentThread() - before);
        Assert.True(double.IsFinite(result));
    }

    private delegate bool Combination(double a, double b, double c, double d, out double value);

    [Fact]
    public void GuardedQuadrupleCombinationMatchesIndependentFractions()
    {
        var method = typeof(MassNormalizedQuadrupleEma).GetNestedType("State", BindingFlags.NonPublic)!
            .GetMethod("TryCombine", BindingFlags.NonPublic | BindingFlags.Static)!;
        var combine = method.CreateDelegate<Combination>();
        int accepted = 0, rejected = 0;
        void Check(double a, double b, double c, double d, bool requireCertificate = false)
        {
            bool certified = combine(a, b, c, d, out var actual);
            if (requireCertificate) Assert.True(certified);
            if (!certified) { rejected++; return; }
            accepted++;
            var exact = ReferenceFraction.FromDouble(a) * new ReferenceFraction(4)
                - ReferenceFraction.FromDouble(b) * new ReferenceFraction(6)
                + ReferenceFraction.FromDouble(c) * new ReferenceFraction(4)
                - ReferenceFraction.FromDouble(d);
            Bits(exact.ToDouble(), actual);
        }
        for (int signs = 0; signs < 16; signs++)
            Check((signs & 1) == 0 ? 0d : -0d, (signs & 2) == 0 ? 0d : -0d,
                (signs & 4) == 0 ? 0d : -0d, (signs & 8) == 0 ? 0d : -0d, true);
        Check(1, 1, 1, Math.BitDecrement(1d), true);
        double odd = Math.BitIncrement(1d);
        Check(odd, odd, odd, Math.BitDecrement(1d), true);
        Check(double.MaxValue, double.MaxValue, double.MaxValue, double.MaxValue, true);
        Assert.False(combine(double.MaxValue, -double.MaxValue, double.MaxValue, -double.MaxValue, out _));
        var random = new Random(261113);
        for (int i = 0; i < 2000; i++)
        {
            int exponent = random.Next(-1074, 1024);
            double Next() => Math.ScaleB(random.NextDouble() * 2 - 1, exponent);
            Check(Next(), Next(), Next(), Next());
        }
        Assert.True(accepted > 0 && rejected > 0);
    }

    [Theory]
    [InlineData(.2, .2, .2, .2)]
    [InlineData(.1, .3, .7, .9)]
    [InlineData(1, 1, 1, 1)]
    public void QuadrupleCombinationPreservesExtremeTrajectoriesAndReset(double a, double b, double c, double d)
    {
        var state = (IIndicatorState)new MassNormalizedQuadrupleEma(a, b, c, d).CreateState();
        foreach (int exponent in new[] { -1074, -1022, -500, 0, 500, 1000 })
        {
            double scale = Math.ScaleB(1d, exponent);
            var bars = Enumerable.Range(0, 768).Select(i => new Bar(default, 0, 1, -1,
                i % 19 == 0 ? -0d : ((i * 17 % 43) - 21) / 32d * scale, 1)).ToArray();
            var expected = MassNormalizedReference.Calculate(bars, [a, b, c, d]);
            state.Reset();
            for (int i = 0; i < bars.Length; i++) Bits(expected[i], state.Update(in bars[i]));
        }
    }

    [Theory]
    [InlineData(1000)]
    [InlineData(10000)]
    public async Task DefaultQuadrupleLongHistoriesMatchIndependentReference(int count)
    {
        var bars = Enumerable.Range(0, count).Select(i =>
        {
            double price = 100 + (i * 17 % 101) / 8d;
            return new Bar(DateTime.UnixEpoch.AddMinutes(i), price, price, price, price, 1);
        }).ToArray();
        var expected = MassNormalizedReference.Calculate(bars, [.2, .2, .2, .2]);
        foreach (var history in Enum.GetValues<IndicatorHistoryMode>())
        {
            var indicator = new MassNormalizedQuadrupleEma();
            using var run = await new StockIndicatorBuilder().ConfigureSource(Bars.From(bars))
                .ConfigureIndicators(indicator).ConfigureHistory(history)
                .ConfigureExecution(IndicatorExecutionBackend.Cpu).BuildAsync();
            Assert.Equal(expected.Select(BitConverter.DoubleToInt64Bits),
                run[indicator].ToArray().Select(BitConverter.DoubleToInt64Bits));
        }
    }

    private static void Bits(double expected, double actual) =>
        Assert.Equal(BitConverter.DoubleToInt64Bits(expected), BitConverter.DoubleToInt64Bits(actual));
}

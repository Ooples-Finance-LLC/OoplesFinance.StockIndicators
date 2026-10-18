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

    private static void Bits(double expected, double actual) =>
        Assert.Equal(BitConverter.DoubleToInt64Bits(expected), BitConverter.DoubleToInt64Bits(actual));
}

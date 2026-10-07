using System.Reflection;
using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Indicators;
using Xunit;
using OoplesFinance.StockIndicators.Validation;

namespace OoplesFinance.StockIndicators.CompetitorTests;

public sealed class EngulfingPatternTests
{
    public static IEnumerable<object[]> Examples()
    {
        yield return new object[] { 3d, 2d, 1d, 4d, 100d };
        yield return new object[] { 3d, 2d, 2d, 4d, 100d };
        yield return new object[] { 3d, 2d, 1d, 3d, 100d };
        yield return new object[] { 3d, 2d, 2d, 3d, 0d };
        yield return new object[] { 2d, 3d, 4d, 1d, -100d };
        yield return new object[] { 2d, 3d, 3d, 1d, -100d };
        yield return new object[] { 2d, 3d, 4d, 2d, -100d };
        yield return new object[] { 2d, 3d, 3d, 2d, 0d };
        yield return new object[] { 2d, 2d, 3d, 1d, -100d };
        yield return new object[] { 2d, 2d, 1d, 3d, 0d };
        yield return new object[] { double.MaxValue / 2, -double.MaxValue / 2, -double.MaxValue, double.MaxValue, 100d };
        yield return new object[] { 2 * double.Epsilon, double.Epsilon, 0d, 3 * double.Epsilon, 100d };
        yield return new object[] { 2d, 3d, 2d, 4d, 0d };
    }

    [Theory]
    [MemberData(nameof(Examples))]
    public async Task EndpointRulesWorkThroughPublicRuntime(double previousOpen, double previousClose, double open, double close, double expected)
    {
        var bars = new[] { Make(0, 1, 1), Make(1, previousOpen, previousClose), Make(2, open, close) };
        var indicator = new EngulfingPattern();
        for (var repeat = 0; repeat < 2; repeat++)
        {
            using var run = await new StockIndicatorBuilder().ConfigureSource(Bars.From(bars)).ConfigureIndicators(indicator).BuildAsync();
            Assert.Equal(new[] { 0d, 0d, expected }, run[indicator].ToArray());
        }
    }

    [Fact]
    public void ResetRestoresLookbackAndPreviousBody()
    {
        var indicator = new EngulfingPattern();
        var factory = typeof(EngulfingPattern).GetMethod("CreateState", BindingFlags.NonPublic | BindingFlags.Instance)!;
        var state = (IIndicatorState)factory.Invoke(indicator, null)!;
        for (var repeat = 0; repeat < 3; repeat++)
        {
            Assert.Equal(0, state.Update(Make(0, 1, 1)));
            Assert.Equal(0, state.Update(Make(1, 3, 2)));
            Assert.Equal(100, state.Update(Make(2, 1, 4)));
            state.Reset();
        }
    }

    [Fact]
    public async Task PublicValidationContractCoversLifecycleAndFormula()
    {
        var report = await IndicatorValidation.ValidateAsync(new IndicatorValidationCase(
            typeof(EngulfingPattern), "default", () => new EngulfingPattern()));
        Assert.True(report.IsValid, string.Join(Environment.NewLine, report.Failures));
    }

    private static Bar Make(int i, double open, double close) => new(new DateTime(2020, 1, 1).AddDays(i), open, Math.Max(open, close), Math.Min(open, close), close, 1);
}

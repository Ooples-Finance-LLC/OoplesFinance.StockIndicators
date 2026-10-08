using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Streaming;
using OoplesFinance.StockIndicators.Validation;

namespace OoplesFinance.StockIndicators.Tests.Unit.ValidationTests;

public sealed class FractalNumericalTests
{
    public static IEnumerable<object[]> Cases => IndicatorValidationDiscovery.Discover(new[] { typeof(IIndicator).Assembly })
        .Where(c => c.Factory() is IBuiltInIndicator builtIn && builtIn.BatchName == IndicatorName.WilliamsFractals)
        .Select(c => new object[] { c });

    [Theory, MemberData(nameof(Cases))]
    public async Task EveryAliasReceivesEveryNumericalClass(IndicatorValidationCase testCase)
    {
        var report = await IndicatorValidation.ValidateAsync(testCase);
        report.ThrowIfInvalid();
        foreach (var fixture in IndicatorAdversarialCases.Generate(256, 244))
            Assert.Contains(report.FixtureEvidence, f => f.Name == fixture.Name && f.Completed && f.Passed);
    }

    [Theory]
    [InlineData(new[] { 0, 0, 1 })]
    [InlineData(new[] { 0, 0, 1, 1 })]
    [InlineData(new[] { 0, 0, 1, 0, 1 })]
    [InlineData(new[] { 0, 0, 1, 1, 0, 1 })]
    [InlineData(new[] { 0, 0, 1, 0, 1, 0, 1 })]
    public async Task PlateauShapesDistinguishAdjacentValuesAtEveryMagnitude(int[] shape)
    {
        foreach (var peak in new[] { double.Epsilon, 1d, double.MaxValue })
        foreach (var sign in new[] { 1, -1 })
        foreach (var length in new[] { 1, 2, 4 })
        foreach (var tiedConfirmation in new[] { false, true })
        foreach (var olderTooHigh in new[] { false, true })
        {
            if (olderTooHigh && peak == double.MaxValue) continue;
            var below = Math.BitDecrement(peak);
            var prices = shape.Select(bit => bit == 1 ? peak : below)
                .Concat(Enumerable.Repeat(below, Math.Max(2, length))).ToArray();
            if (tiedConfirmation) prices[shape.Length + 1] = peak;
            if (olderTooHigh) prices[shape.Length - 2] = Math.BitIncrement(peak);
            var bars = prices.Select((value, i) => new Bar(DateTime.UnixEpoch.AddMinutes(i),
                sign * value, sign * value, sign * value, sign * value, 1)).ToArray();
            var expectedUp = !tiedConfirmation && !olderTooHigh && sign == 1 ? 1d : 0;
            var expectedDown = !tiedConfirmation && !olderTooHigh && sign == -1 ? 1d : 0;
            IIndicator[] indicators = { new WilliamsFractals(length), new WilliamsFractalUp(length),
                new WilliamsFractalsUp(length), new WilliamsFractalDown(length), new WilliamsFractalsDown(length) };
            using var run = await new StockIndicatorBuilder().ConfigureSource(Bars.From(bars)).ConfigureIndicators(indicators).BuildAsync();
            foreach (var indicator in indicators)
            {
                var builtIn = (IBuiltInIndicator)indicator;
                var keys = indicator.Outputs.Count == 1 ? new[] { builtIn.BatchOutputKey ?? "UpFractal" } : GeneratedIndicatorOutputs.KeysFor(builtIn.BatchName);
                for (var slot = 0; slot < keys.Count; slot++)
                    Assert.Equal(keys[slot] == "UpFractal" ? expectedUp : expectedDown, run[indicator.Outputs[slot]].ToArray()[^1]);
            }
            var data = new StockData(bars.Select(b => b.Open), bars.Select(b => b.High), bars.Select(b => b.Low),
                bars.Select(b => b.Close), bars.Select(b => b.Volume), bars.Select(b => b.Time));
            data.CalculateWilliamsFractals(length);
            Assert.Equal(expectedUp, data.OutputValues["UpFractal"][^1]);
            Assert.Equal(expectedDown, data.OutputValues["DnFractal"][^1]);
            using var state = new WilliamsFractalsState(length);
            for (var replay = 0; replay < 2; replay++)
            {
                state.Reset();
                for (var i = 0; i < bars.Length; i++)
                foreach (var commit in new[] { false, true })
                {
                    var b = bars[i];
                    var native = new OhlcvBar("FRACTAL", BarTimeframe.Minutes(1), b.Time, b.Time, b.Open, b.High, b.Low, b.Close, b.Volume, true);
                    var actual = state.Update(native, commit, true);
                    if (i != bars.Length - 1) continue;
                    Assert.Equal(expectedUp, actual.Outputs!["UpFractal"]);
                    Assert.Equal(expectedDown, actual.Outputs!["DnFractal"]);
                }
            }
        }
    }
}

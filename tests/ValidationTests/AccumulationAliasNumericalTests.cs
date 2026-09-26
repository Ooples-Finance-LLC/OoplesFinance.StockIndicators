using OoplesFinance.StockIndicators.Builder.Compute;
using OoplesFinance.StockIndicators.Builder.Specs;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Validation;

namespace OoplesFinance.StockIndicators.Tests.Unit.ValidationTests;

public sealed class AccumulationAliasNumericalTests
{
    [Fact]
    public void DirectAliasArmsRespectSelectedPricesAndSignalPeriod()
    {
        var prices = new[] { 2d, 5, 3, 7, -2, 4, 4, 0 };
        var bars = prices.Select((p, i) => new Bar(DateTime.UnixEpoch.AddMinutes(i), 1, 10, -5, p, i + 1)).ToArray();
        foreach (IBuiltInIndicator indicator in new IBuiltInIndicator[] { new WilliamsAD(3), new Adl(3) })
        {
            var expected = indicator is WilliamsAD
                ? BuiltInFormulaReferences.WilliamsAccumulationOutputs(bars, indicator)
                : BuiltInFormulaReferences.MoneyFlowAccumulationOutputs(bars, indicator);
            foreach (var pair in expected)
            {
                var data = new StockData(bars.Select(b => b.Open), bars.Select(b => b.High), bars.Select(b => b.Low),
                    bars.Select(_ => 42d), bars.Select(b => b.Volume), bars.Select(b => b.Time));
                data.CustomValuesList = prices.ToList();
                using var context = new ComputeContext();
                using var actual = IndicatorCompute.ComputeArm(data, new IndicatorSpec(indicator.BatchName, indicator.CreateOptions(), pair.Key), context);
                Assert.NotNull(actual);
                Assert.Equal(pair.Value, actual.Value.ToArray());
            }
        }
    }
}

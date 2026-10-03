using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Helpers;
using OoplesFinance.StockIndicators.Streaming;

namespace OoplesFinance.StockIndicators.Tests.Unit.StreamingTests;

public sealed class CustomInputRangeTests : GlobalTestData
{
    public static IEnumerable<object[]> States()
    {
        yield return new object[] { typeof(AccumulationDistributionLineState) };
        yield return new object[] { typeof(AdaptiveMovingAverageState) };
        yield return new object[] { typeof(BelkhayateTimingState) };
        yield return new object[] { typeof(CamarillaPivotPointsState) };
        yield return new object[] { typeof(ChaikinMoneyFlowState) };
        yield return new object[] { typeof(ChaikinOscillatorState) };
        yield return new object[] { typeof(ChaikinVolatilityState) };
        yield return new object[] { typeof(ChartmillValueIndicatorState) };
        yield return new object[] { typeof(ChopZoneState) };
        yield return new object[] { typeof(ChoppinessIndexState) };
        yield return new object[] { typeof(DailyAveragePriceDeltaState) };
        yield return new object[] { typeof(DampingIndexState) };
        yield return new object[] { typeof(DemarkPivotPointsState) };
        yield return new object[] { typeof(DemarkPressureRatioV1State) };
        yield return new object[] { typeof(DemarkPressureRatioV2State) };
        yield return new object[] { typeof(DemarkRangeExpansionIndexState) };
        yield return new object[] { typeof(DynamicPivotPointsState) };
        yield return new object[] { typeof(EarningSupportResistanceLevelsState) };
        yield return new object[] { typeof(EaseOfMovementState) };
        yield return new object[] { typeof(EhlersDetrendedLeadingIndicatorState) };
        yield return new object[] { typeof(EhlersRelativeVigorIndexState) };
        yield return new object[] { typeof(ElderMarketThermometerState) };
        yield return new object[] { typeof(ErgodicCandlestickOscillatorState) };
        yield return new object[] { typeof(FibonacciPivotPointsState) };
        yield return new object[] { typeof(FiniteVolumeElementsState) };
        yield return new object[] { typeof(FloorPivotPointsState) };
        yield return new object[] { typeof(GopalakrishnanRangeIndexState) };
        yield return new object[] { typeof(InsyncIndexState) };
        yield return new object[] { typeof(InternalBarStrengthIndicatorState) };
        yield return new object[] { typeof(MassIndexState) };
        yield return new object[] { typeof(NormalizedRelativeVigorIndexState) };
        yield return new object[] { typeof(PivotPointAverageState) };
        yield return new object[] { typeof(PrettyGoodOscillatorState) };
        yield return new object[] { typeof(PriceCycleOscillatorState) };
        yield return new object[] { typeof(ProjectedSupportAndResistanceState) };
        yield return new object[] { typeof(ReallySimpleIndicatorState) };
        yield return new object[] { typeof(RelativeVigorIndexState) };
        yield return new object[] { typeof(RelativeVolatilityIndexHighState) };
        yield return new object[] { typeof(RelativeVolatilityIndexLowState) };
        yield return new object[] { typeof(RepulseState) };
        yield return new object[] { typeof(RetrospectiveCandlestickChartState) };
        yield return new object[] { typeof(RexOscillatorState) };
        yield return new object[] { typeof(ShinoharaIntensityRatioState) };
        yield return new object[] { typeof(SmoothedWilliamsAccumulationDistributionState) };
        yield return new object[] { typeof(StandardPivotPointsState) };
        yield return new object[] { typeof(StochasticCustomOscillatorState) };
        yield return new object[] { typeof(StochasticMomentumIndexState) };
        yield return new object[] { typeof(TechnicalRatingsState) };
        yield return new object[] { typeof(TironeLevelsState) };
        yield return new object[] { typeof(TraderPressureIndexState) };
        yield return new object[] { typeof(TrendTriggerFactorState) };
        yield return new object[] { typeof(TwiggsMoneyFlowState) };
        yield return new object[] { typeof(UltimateOscillatorState) };
        yield return new object[] { typeof(VolatilityRatioState) };
        yield return new object[] { typeof(VolumeAccumulationOscillatorState) };
        yield return new object[] { typeof(VolumeAccumulationPercentState) };
        yield return new object[] { typeof(VortexIndicatorState) };
        yield return new object[] { typeof(WilliamsAccumulationDistributionState) };
        yield return new object[] { typeof(WoodiePivotPointsState) };
        yield return new object[] { typeof(StochasticOscillatorState) };
        yield return new object[] { typeof(IchimokuCloudState) };
        yield return new object[] { typeof(AverageDirectionalIndexState) };
        yield return new object[] { typeof(ElderRayIndexState) };
        yield return new object[] { typeof(WilliamsRState) };
    }

    [Theory]
    [MemberData(nameof(States))]
    public void MixedRangesMatchBatchAfterPreviewsAndReset(Type type)
    {
        var ticks = StockTestData.Take(251).ToList();
        // Switch scales repeatedly, including on the first bar after reset.
        var values = ticks.Select((t, i) => i % 3 == 1 ? (t.High + t.Low) / 2 : Math.Log(t.Close) + i % 7).ToList();
        var construction = StreamingCustomInputTests.FindDefaultConstruction(type)!.Value;
        var inner = StreamingCustomInputTests.Build(construction);
        var method = IndicatorInvoker.GetMethod(inner.Name)!;
        var args = method.GetParameters().Select(p => p.DefaultValue).ToArray();
        var data = new StockData(ticks);
        data.SetCustomValues(values);
        args[0] = data;
        method.Invoke(null, args);
        double selected = 0;
        using var state = new CustomInputState(inner, _ => selected);
        for (var pass = 0; pass < 2; pass++)
        {
            state.Reset();
            for (var i = 0; i < ticks.Count; i++)
            {
                var t = ticks[i];
                var bar = new OhlcvBar("TEST", BarTimeframe.Tick, t.Date, t.Date,
                    t.Open, t.High, t.Low, t.Close, t.Volume, true);
                selected = -1000 - i;
                state.Update(bar, false, false);
                selected = values[i];
                var preview = state.Update(bar, false, true);
                var result = state.Update(bar, true, true);
                result.Value.Should().Be(preview.Value, $"{type.Name} preview at {i}");
                result.Outputs!.Keys.Should().BeEquivalentTo(data.OutputValues.Keys);
                foreach (var (key, output) in result.Outputs)
                {
                    output.Should().Be(preview.Outputs![key], $"{type.Name}/{key} preview at {i}");
                    IndicatorRunner.IsClose(data.OutputValues[key][i], output).Should().BeTrue(
                        $"{type.Name}/{key} bar {i}, pass {pass}: expected {data.OutputValues[key][i]:R}, got {output:R}");
                }
            }
        }
    }
}

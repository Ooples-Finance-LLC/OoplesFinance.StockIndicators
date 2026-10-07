using System.Reflection;
using OoplesFinance.StockIndicators.Indicators;

namespace OoplesFinance.StockIndicators.Validation;

public static partial class IndicatorValidationDiscovery
{
    private static int MinimumPeriod(Type type, string parameter)
    {
        if (type == typeof(WindowSampleShape)) return 3;
        if (type == typeof(AlignedEmaMacd)) return parameter == "signalPeriod" ? 1 : 2;
        if (type == typeof(SeededPriceMomentum)) return parameter == "period" ? 2 : 1;
        return type == typeof(ReverseEngineeringRsi) || type == typeof(GaussianWeightedAverage)
            || type == typeof(Indicators.HullWindow) || type == typeof(NormalizedWindowEntropy)
            || type == typeof(RangeFractalAverage) || type == typeof(SeededAtrTrailingStop)
            || type == typeof(StandardDeviationWithDetails) || type == typeof(SumSeededDirectionalIndex)
            || type == typeof(WindowChoppinessIndex) || type == typeof(WindowLogVolatility)
            || type == typeof(WindowMoneyFlowIndex) || type == typeof(WindowRegressionStatistics)
            ? 2 : 1;
    }

    private static object? ComparisonRequiredArgument(Type type, ParameterInfo parameter)
    {
        if (type == typeof(CandleArithmetic) || type == typeof(PriceCircularTransform)
            || type == typeof(PriceRoundingTransform) || type == typeof(PriceTranscendentalTransform)
            || type == typeof(PriorSeededDirectionalMeasure) || type == typeof(WindowDirectionalMeasure))
            return parameter.ParameterType.IsEnum ? Enum.GetValues(parameter.ParameterType).GetValue(0) : null;
        return null;
    }

    private static IIndicator CreateAutomatic(Type type, ConstructorInfo constructor, object?[] arguments)
    {
        var parameters = constructor.GetParameters();
        int Slot(string name) => Array.FindIndex(parameters, p => p.Name == name);
        int Value(string name) => (int)arguments[Slot(name)]!;
        void AtLeast(string name, int minimum) => arguments[Slot(name)] = Math.Max(Value(name), minimum);

        // These formulas require ordered periods. Vary the selected period while keeping
        // the dependent stages inside the public constructor's domain.
        if (type == typeof(AlligatorWithDetails) || type == typeof(GatorWithDetails))
        {
            AtLeast("teethPeriod", Value("lipsPeriod") + 1);
            AtLeast("jawPeriod", Value("teethPeriod") + 1);
        }
        if (type == typeof(AwesomeWithDetails)) AtLeast("slowPeriod", Value("fastPeriod") + 1);
        if (type == typeof(WindowUltimateOscillator))
        {
            AtLeast("middlePeriod", Value("shortPeriod") + 1);
            AtLeast("longPeriod", Value("middlePeriod") + 1);
        }
        if (type == typeof(RateOfChangeRmsBands))
            arguments[Slot("deviationPeriod")] = Math.Min(Value("period"), Value("deviationPeriod"));
        return (IIndicator)constructor.Invoke(arguments);
    }

    private static IEnumerable<IndicatorValidationCase> ComparisonDependencyCases(Type type)
    {
        if (type == typeof(NormalizedConvolution))
        {
            yield return new(type, "default", () => new NormalizedConvolution(new[] { 1d, 2, 3 }));
            yield return new(type, "minimum-periods", () => new NormalizedConvolution(new[] { 1d }));
            yield return new(type, "signed-kernel", () => new NormalizedConvolution(new[] { -1d, 2, 1 }));
        }
        if (type == typeof(VariablePeriodClassicAverage))
        {
            yield return new(type, "default", () => new VariablePeriodClassicAverage(bar => bar.Volume));
            yield return new(type, "minimum-periods", () => new VariablePeriodClassicAverage(_ => 1, 1, 1));
            yield return new(type, "clamped-periods", () => new VariablePeriodClassicAverage(bar => bar.Close, 2, 7));
        }
    }
}

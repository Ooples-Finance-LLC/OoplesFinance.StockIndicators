using OoplesFinance.StockIndicators.Indicators;
namespace OoplesFinance.StockIndicators.Validation;
internal static partial class BuiltInFormulaReferences
{
    internal static IReadOnlyDictionary<string, double[]> RelativeVolatilityOutputs(IReadOnlyList<Bar> bars, IBuiltInIndicator indicator)
    { var o = indicator.CreateOptions(); return RelativeVolatilityOutputs(bars, indicator.BatchName == IndicatorName.RelativeVolatilityIndexV2, Integer(o, "Length", 10), Integer(o, "SmoothLength", 14), AverageKind(o, 6)); }
    internal static IReadOnlyDictionary<string, double[]> RelativeVolatilityOutputs(IReadOnlyList<Bar> bars, bool paired, int length, int smoothLength, int kind, double[][]? external = null)
    {
        double[] Side(double[] input, int offset)
        {
            length = Math.Max(1, length); var values = input.Select(ReferenceFraction.FromDouble).ToArray(); var up = new ReferenceFraction[input.Length]; var down = new ReferenceFraction[input.Length]; var zero = new ReferenceFraction(0);
            for (var i = 0; i < input.Length; i++)
            {
                var deviation = zero;
                if (i + 1 >= length) { var window = values.Skip(i + 1 - length).Take(length).ToArray(); var mean = window.Aggregate(zero, (a,b) => a + b) / new ReferenceFraction(length); var variance = window.Aggregate(zero, (a,b) => a + (b - mean) * (b - mean)) / new ReferenceFraction(length); deviation = ReferenceFraction.FromDouble(variance.SqrtToDouble()); }
                var previous = i == 0 ? 0 : input[i - 1]; up[i] = input[i] > previous ? deviation : zero; down[i] = input[i] < previous ? deviation : zero;
            }
            var u = external is null ? SmoothRocBankStage(up, Math.Max(1, smoothLength), kind) : external[offset].Select(ReferenceFraction.FromDouble).ToArray(); var d = external is null ? SmoothRocBankStage(down, Math.Max(1, smoothLength), kind) : external[offset + 1].Select(ReferenceFraction.FromDouble).ToArray(); var output = new double[input.Length];
            for (var i = 0; i < input.Length; i++) { var total = u[i] + d[i]; output[i] = d[i].Sign == 0 ? 100 : u[i].Sign == 0 || total.Sign == 0 ? 0 : Math.Max(0, Math.Min(100, (new ReferenceFraction(100) * u[i] / total).ToDouble())); } return output;
        }
        var first = Side(bars.Select(b => paired ? b.High : b.Close).ToArray(), 0); if (!paired) return Outputs(("Rvi", first)); var second = Side(bars.Select(b => b.Low).ToArray(), 2); var result = first.Select((v,i) => ((ReferenceFraction.FromDouble(v) + ReferenceFraction.FromDouble(second[i])) / new ReferenceFraction(2)).ToDouble()).ToArray(); return Outputs(("Rvi", result));
    }
}

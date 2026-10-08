using OoplesFinance.StockIndicators.Helpers;

namespace OoplesFinance.StockIndicators.Builder.Compute;

internal static partial class IndicatorCompute
{
    private static ComputeBuffer ComputeOpenCloseAverageFast(StockData data, ComputeContext context, int length, MovingAvgType maType, int lag, string outputKey)
    {
        var input = data.ChainedValues.Count > 0 ? data.ChainedValues : data.InputValues;
        var custom = ComponentAverage.HasOverrides || !StrengthWindow.Supports(maType);
        using var customer = context.Rent(input.Count);
        if (custom)
        {
            using var changes = context.Rent(input.Count);
            for (var i = 0; i < input.Count; i++) changes.WritableSpan[i] = OpenCloseAverageWindow.Difference(i >= lag ? data.OpenPrices[i - lag] : 0, input[i]).Publish();
            MovingAverage(data, maType, length, changes.Span, customer.WritableSpan);
        }
        var result = context.Rent(input.Count); using var window = new OpenCloseAverageWindow(maType, length, lag);
        for (var i = 0; i < input.Count; i++)
        {
            var value = window.Next(data.OpenPrices[i], input[i], true, custom ? customer.Span[i] : null);
            result.WritableSpan[i] = outputKey == "Histogram" ? value.Histogram : outputKey == "Delta" ? value.Line : value.Signal;
        }
        return result;
    }
}

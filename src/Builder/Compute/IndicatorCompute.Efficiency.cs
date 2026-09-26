namespace OoplesFinance.StockIndicators.Builder.Compute;

internal static partial class IndicatorCompute
{
    private static ComputeBuffer ComputeKendallCorrelation(StockData data, ComputeContext context, int length)
    {
        var values = data.ChainedValues.Count > 0 ? data.ChainedValues : data.InputValues;
        var result = context.Rent(values.Count);
        using var window = new KendallCorrelationWindow(length);
        for (var i = 0; i < values.Count; i++) result.WritableSpan[i] = window.Next(values[i], true);
        return result;
    }

    private static ComputeBuffer ComputeLogisticCorrelation(StockData data, ComputeContext context, int length, double k)
    {
        var values = data.ChainedValues.Count > 0 ? data.ChainedValues : data.InputValues;
        var result = context.Rent(values.Count);
        using var window = new LogisticCorrelationWindow(length, k);
        for (var i = 0; i < values.Count; i++) result.WritableSpan[i] = window.Next(values[i], true);
        return result;
    }

    internal static ComputeBuffer ComputeEfficientPrice(StockData data, ComputeContext context, int length)
    {
        var input = data.ChainedValues.Count > 0 ? data.ChainedValues : data.InputValues;
        var window = new EfficiencyOutputWindow(length); var buffer = context.Rent(input.Count);
        for (var i = 0; i < input.Count; i++) buffer.WritableSpan[i] = window.Next(input[i], true);
        return buffer;
    }

    internal static ComputeBuffer ComputeEfficientAutoLine(StockData data, ComputeContext context,
        int length, double fastAlpha, double slowAlpha)
    {
        var input = data.ChainedValues.Count > 0 ? data.ChainedValues : data.InputValues;
        var window = new EfficiencyOutputWindow(length, true, fastAlpha, slowAlpha); var buffer = context.Rent(input.Count);
        for (var i = 0; i < input.Count; i++) buffer.WritableSpan[i] = window.Next(input[i], true);
        return buffer;
    }
}

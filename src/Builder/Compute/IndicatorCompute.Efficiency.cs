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

    private static ComputeBuffer ComputeEfficientPrice(StockData data, ComputeContext context, int length)
    {
        var values = data.ChainedValues.Count > 0 ? data.ChainedValues : data.InputValues;
        var input = OoplesFinance.StockIndicators.Compatibility.SpanCompat.AsReadOnlySpan(values);
        using var efficiency = context.Rent(values.Count);
        EfficiencyRatio(input, length, efficiency.WritableSpan);
        var result = context.Rent(values.Count);
        double cumulative = 0;
        for (var i = 0; i < values.Count; i++)
        {
            if (i >= length) cumulative += (input[i] - input[i - length]) * efficiency.Span[i];
            result.WritableSpan[i] = cumulative;
        }
        return result;
    }

    private static ComputeBuffer ComputeEfficientAutoLine(StockData data, ComputeContext context,
        int length, double fastAlpha, double slowAlpha)
    {
        var values = data.ChainedValues.Count > 0 ? data.ChainedValues : data.InputValues;
        var input = OoplesFinance.StockIndicators.Compatibility.SpanCompat.AsReadOnlySpan(values);
        using var efficiency = context.Rent(values.Count);
        EfficiencyRatio(input, length, efficiency.WritableSpan);
        var result = context.Rent(values.Count);
        double previous = 0;
        for (var i = 0; i < values.Count; i++)
        {
            var er = efficiency.Span[i];
            var deviation = er * fastAlpha + (1 - er) * slowAlpha;
            if (i < 9 || input[i] > previous + deviation || input[i] < previous - deviation)
                previous = input[i];
            result.WritableSpan[i] = previous;
        }
        return result;
    }
}

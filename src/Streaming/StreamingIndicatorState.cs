using System.Collections.Generic;
using OoplesFinance.StockIndicators.Enums;

namespace OoplesFinance.StockIndicators.Streaming;

public interface IStreamingIndicatorState
{
    IndicatorName Name { get; }
    void Reset();
    StreamingIndicatorStateResult Update(OhlcvBar bar, bool isFinal, bool includeOutputs);
}

public readonly struct StreamingIndicatorStateResult
{
    public StreamingIndicatorStateResult(double value, IReadOnlyDictionary<string, double>? outputs)
    {
        Value = value;
        Outputs = outputs;
    }

    public double Value { get; }
    public IReadOnlyDictionary<string, double>? Outputs { get; }
}

public sealed class StreamingIndicatorStateUpdate
{
    public StreamingIndicatorStateUpdate(string symbol, BarTimeframe timeframe, bool isFinalBar,
        IndicatorName indicator, double value, IReadOnlyDictionary<string, double>? outputs)
    {
        Symbol = symbol;
        Timeframe = timeframe;
        IsFinalBar = isFinalBar;
        Indicator = indicator;
        Value = value;
        Outputs = outputs;
    }

    public string Symbol { get; }
    public BarTimeframe Timeframe { get; }
    public bool IsFinalBar { get; }
    public IndicatorName Indicator { get; }
    public double Value { get; }
    public IReadOnlyDictionary<string, double>? Outputs { get; }
}

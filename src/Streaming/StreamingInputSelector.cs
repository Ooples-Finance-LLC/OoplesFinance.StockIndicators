using System;
using OoplesFinance.StockIndicators.Enums;

namespace OoplesFinance.StockIndicators.Streaming;

public static class StreamingInputSelector
{
    public static double GetValue(OhlcvBar bar, InputName inputName)
    {
        switch (inputName)
        {
            case InputName.AdjustedClose:
            case InputName.Close:
                return bar.Close;
            case InputName.Open:
                return bar.Open;
            case InputName.High:
                return bar.High;
            case InputName.Low:
                return bar.Low;
            case InputName.Volume:
                return bar.Volume;
            case InputName.TypicalPrice:
                return (bar.High + bar.Low + bar.Close) / 3;
            case InputName.FullTypicalPrice:
                return (bar.Open + bar.High + bar.Low + bar.Close) / 4;
            case InputName.MedianPrice:
                return (bar.High + bar.Low) / 2;
            case InputName.WeightedClose:
                return (bar.High + bar.Low + (bar.Close * 2)) / 4;
            case InputName.AveragePrice:
                return (bar.Open + bar.Close) / 2;
            case InputName.Midpoint:
            case InputName.Midprice:
                throw new NotSupportedException("InputName.Midpoint and InputName.Midprice require rolling windows. Use a custom selector.");
            default:
                return bar.Close;
        }
    }
}

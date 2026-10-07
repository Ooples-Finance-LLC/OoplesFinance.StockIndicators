using OoplesFinance.StockIndicators.Helpers;
using System;
using OoplesFinance.StockIndicators.Enums;

namespace OoplesFinance.StockIndicators.Streaming;

internal static class StreamingInputSelector
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
                return PriceMean.Of(bar.High, bar.Low, bar.Close);
            case InputName.FullTypicalPrice:
                return PriceMean.Of(bar.Open, bar.High, bar.Low, bar.Close);
            case InputName.MedianPrice:
                return PriceMean.Of(bar.High, bar.Low);
            case InputName.WeightedClose:
                return PriceMean.Of(bar.High, bar.Low, bar.Close, bar.Close);
            case InputName.AveragePrice:
                return PriceMean.Of(bar.Open, bar.Close);
            case InputName.Midpoint:
            case InputName.Midprice:
                throw new NotSupportedException("InputName.Midpoint and InputName.Midprice require rolling windows. Use a custom selector.");
            default:
                return bar.Close;
        }
    }
}

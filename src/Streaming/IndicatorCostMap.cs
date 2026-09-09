using System;
using OoplesFinance.StockIndicators.Enums;
using OoplesFinance.StockIndicators.Helpers;

namespace OoplesFinance.StockIndicators.Streaming;

public static class IndicatorCostMap
{
    public static IndicatorCost GetCost(IndicatorName name)
    {
        if (name == IndicatorName.None)
        {
            return IndicatorCost.Unknown;
        }

        if (IsLowCost(name))
        {
            return IndicatorCost.Low;
        }

        if (IsHighCost(name))
        {
            return IndicatorCost.High;
        }

        var type = name.GetIndicatorType();
        if (type == IndicatorType.Cycle)
        {
            return IndicatorCost.High;
        }

        if (type == IndicatorType.SupportAndResistance)
        {
            return IndicatorCost.Low;
        }

        return type == default ? IndicatorCost.Unknown : IndicatorCost.Medium;
    }

    private static bool IsLowCost(IndicatorName name)
    {
        switch (name)
        {
            case IndicatorName.SimpleMovingAverage:
            case IndicatorName.ExponentialMovingAverage:
            case IndicatorName.WeightedMovingAverage:
            case IndicatorName.AverageTrueRange:
            case IndicatorName.RateOfChange:
            case IndicatorName.OnBalanceVolume:
            case IndicatorName.MoneyFlowIndex:
                return true;
            default:
                return false;
        }
    }

    private static bool IsHighCost(IndicatorName name)
    {
        var text = name.ToString();
        return Contains(text, "ehlers")
            || Contains(text, "hilbert")
            || Contains(text, "fourier")
            || Contains(text, "spectrum")
            || Contains(text, "mesa");
    }

    private static bool Contains(string value, string token)
    {
        return value.IndexOf(token, StringComparison.OrdinalIgnoreCase) >= 0;
    }
}

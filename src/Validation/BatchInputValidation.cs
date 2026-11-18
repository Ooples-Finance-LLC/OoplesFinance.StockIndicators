using OoplesFinance.StockIndicators.Models;

namespace OoplesFinance.StockIndicators.Validation;

internal static class BatchInputValidation
{
    internal static void Validate(StockData data)
    {
        if (data.Count < 0) throw new ArgumentOutOfRangeException(nameof(data), "Input count cannot be negative.");
        Check(data.OpenSpan, nameof(data.OpenPrices));
        Check(data.HighSpan, nameof(data.HighPrices));
        Check(data.LowSpan, nameof(data.LowPrices));
        Check(data.CloseSpan, nameof(data.ClosePrices));
        Check(data.VolumeSpan, nameof(data.Volumes));
        if (data.DateSpan.Length != data.Count)
            throw new ArgumentException("Dates must contain exactly Count observations.", nameof(data));
        Check(data.ChainedSpanOrInput,
            data.ChainedValues.Count > 0 ? nameof(data.CustomValuesList) : nameof(data.InputValues));

        void Check(ReadOnlySpan<double> values, string field)
        {
            if (values.Length != data.Count)
                throw new ArgumentException(field + " must contain exactly Count observations.", nameof(data));
            for (var i = 0; i < values.Length; i++)
                if (double.IsNaN(values[i]) || double.IsInfinity(values[i]))
                    throw new ArgumentOutOfRangeException(nameof(data), values[i], field + " must be finite at bar " + i + ".");
        }
    }
}

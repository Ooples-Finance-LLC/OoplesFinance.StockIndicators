using OoplesFinance.StockIndicators.Models;

namespace OoplesFinance.StockIndicators.Validation;

internal static class BatchInputValidation
{
    internal static void Validate(StockData data)
    {
        if (data.Count < 0) throw new ArgumentOutOfRangeException(nameof(data), "Input count cannot be negative.");
        Check(data.OpenPrices, nameof(data.OpenPrices));
        Check(data.HighPrices, nameof(data.HighPrices));
        Check(data.LowPrices, nameof(data.LowPrices));
        Check(data.ClosePrices, nameof(data.ClosePrices));
        Check(data.Volumes, nameof(data.Volumes));
        if (data.Dates.Count != data.Count)
            throw new ArgumentException("Dates must contain exactly Count observations.", nameof(data));
        Check(data.CustomValuesList.Count > 0 ? data.CustomValuesList : data.InputValuesForValidation,
            data.CustomValuesList.Count > 0 ? nameof(data.CustomValuesList) : nameof(data.InputValues));

        void Check(IReadOnlyList<double> values, string field)
        {
            if (values.Count != data.Count)
                throw new ArgumentException(field + " must contain exactly Count observations.", nameof(data));
            for (var i = 0; i < values.Count; i++)
                if (double.IsNaN(values[i]) || double.IsInfinity(values[i]))
                    throw new ArgumentOutOfRangeException(nameof(data), values[i], field + " must be finite at bar " + i + ".");
        }
    }
}

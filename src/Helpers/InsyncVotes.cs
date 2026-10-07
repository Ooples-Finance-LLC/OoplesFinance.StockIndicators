namespace OoplesFinance.StockIndicators.Helpers;

// Distinct component values remain distinct at a vote boundary, including values
// outside binary64's published range.
internal static class InsyncVotes
{
    internal static double Band(double value, double lower, double upper) =>
        value < lower ? -5 : value > upper ? 5 : 0;
    internal static double Direction(double value, double mean) => value < mean
        ? mean < 0 ? -5 : 0 : mean > 0 ? 5 : 0;
    internal static double InverseDirection(double value, double mean) => value > mean
        ? mean > 0 ? 5 : 0 : mean < 0 ? -5 : 0;
    internal static double Band(RocBankValue value, double lower, double upper)
    {
        var units = InsyncWindow.Units(value);
        return units < ExactVarianceWindow.Units(lower) ? -5 : units > ExactVarianceWindow.Units(upper) ? 5 : 0;
    }
    internal static double Direction(RocBankValue value, RocBankValue mean)
    {
        var v = InsyncWindow.Units(value); var m = InsyncWindow.Units(mean);
        return v < m ? m.Sign < 0 ? -5 : 0 : m.Sign > 0 ? 5 : 0;
    }
    internal static double InverseDirection(RocBankValue value, RocBankValue mean)
    {
        var v = InsyncWindow.Units(value); var m = InsyncWindow.Units(mean);
        return v > m ? m.Sign > 0 ? 5 : 0 : m.Sign < 0 ? -5 : 0;
    }
}

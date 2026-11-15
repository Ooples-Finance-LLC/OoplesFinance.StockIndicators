namespace OoplesFinance.StockIndicators.Helpers;

internal static class CandleDifferenceUpdate
{
    internal static void Add(ref ExactMeanAccumulator sum, double upper, double lower, int weight)
    {
#if !NETFRAMEWORK
        if (Binary64ArithmeticCertificate.TryDifference(upper, lower, out double difference))
        {
            sum.Add(difference, weight);
            return;
        }
#endif
        sum.Add(upper, weight);
        sum.Add(lower, -weight);
    }
}

namespace OoplesFinance.StockIndicators.Builder;

/// <summary>Controls retained finite bar snapshots, independently of completed indicator series.</summary>
public enum IndicatorHistoryMode
{
    /// <summary>Retain every bar for snapshot replay (the default).</summary>
    Full,
    /// <summary>Retain completed series and the latest snapshot; finite snapshot replay is unavailable.
    /// Live feeds continue to deliver new snapshots normally.</summary>
    LatestOnly
}

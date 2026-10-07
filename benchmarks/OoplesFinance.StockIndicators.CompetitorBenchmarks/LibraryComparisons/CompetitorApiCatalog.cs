using System.Reflection;
using System.Text.RegularExpressions;

namespace OoplesFinance.StockIndicators.CompetitorBenchmarks;

internal sealed record CompetitorApi(string Id, string Kind, string[] EntryPoints);

internal static class CompetitorApiCatalog
{
    internal static CompetitorApi[] Discover()
    {
        var entries = new List<CompetitorApi>();
        AddMethods(typeof(Skender.Stock.Indicators.Indicator), "Skender", method => method.Name.StartsWith("Get", StringComparison.Ordinal));
        AddMethods(typeof(TALib.Functions), "TaLib.Functions", method => !method.Name.EndsWith("Lookback", StringComparison.Ordinal));
        AddMethods(typeof(TALib.Candles), "TaLib.Candles", method => !method.Name.EndsWith("Lookback", StringComparison.Ordinal));

        var trady = Assembly.Load("Trady.Analysis").GetExportedTypes()
            .Where(type => !type.IsAbstract && type.Namespace is "Trady.Analysis.Indicator" or "Trady.Analysis.Candlestick");
        foreach (var group in trady.GroupBy(type => Regex.Replace(type.FullName!, "`[0-9]+", "", RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1))
            .Replace("Trady.Analysis.", "Trady.", StringComparison.Ordinal).Replace("ByTuple", "", StringComparison.Ordinal)))
            entries.Add(new(group.Key, group.Key.Contains("Candlestick.", StringComparison.Ordinal) ? "candlestick" : "indicator",
                group.Select(type => type.FullName!).OrderBy(name => name, StringComparer.Ordinal).ToArray()));

        foreach (var type in typeof(QuanTAlib.Sma).Assembly.GetExportedTypes().Where(type => !type.IsAbstract
            && (typeof(QuanTAlib.AbstractBase).IsAssignableFrom(type) || typeof(QuanTAlib.AbstractBarBase).IsAssignableFrom(type))))
            entries.Add(new("QuanTAlib." + type.Name, "indicator", [type.FullName!]));
        return entries.OrderBy(entry => entry.Id, StringComparer.Ordinal).ToArray();

        void AddMethods(Type type, string prefix, Func<MethodInfo, bool> select)
        {
            foreach (var group in type.GetMethods(BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly)
                .Where(select).GroupBy(method => method.Name))
                entries.Add(new(prefix + "." + group.Key, prefix.EndsWith("Candles", StringComparison.Ordinal) ? "candlestick" : "indicator",
                    group.Select(method => method.ToString()!).OrderBy(name => name, StringComparer.Ordinal).ToArray()));
        }
    }

    // Confirmed by executable tests against Trady.Analysis 3.2.8. Counterparts stay
    // pending; throwing stubs cannot produce comparable values or useful timings.
    internal static readonly IReadOnlySet<string> Unimplemented = new HashSet<string>(StringComparer.Ordinal)
    {
        "Trady.Indicator.ZigZag",
        "Trady.Candlestick.Hammer",
        "Trady.Candlestick.HangingMan",
        "Trady.Candlestick.HaramiCross",
        "Trady.Candlestick.InvertedHammer",
        "Trady.Candlestick.LongLeggedDoji",
        "Trady.Candlestick.Marubozu",
        "Trady.Candlestick.PiercingLine",
        "Trady.Candlestick.ShootingStar",
        "Trady.Candlestick.ShortShadow",
        "Trady.Candlestick.SpinningTop",
        "Trady.Candlestick.Stars",
        "Trady.Candlestick.StickSandwich",
        "Trady.Candlestick.ThreeBlackCrows",
        "Trady.Candlestick.ThreeWhiteSoldiers",
        "Trady.Candlestick.UpsideGapTwoCrows"
    };

    // These APIs transform data or execute a caller's formula; they are not fixed indicators.
    internal static readonly IReadOnlyDictionary<string, string> Exclusions = new Dictionary<string, string>
    {
        ["Skender.GetBaseQuote"] = "Quote representation conversion.",
        ["Trady.Indicator.FuncAnalyzable"] = "Executes a caller-supplied function, not a fixed indicator.",
        ["Trady.Indicator.Stochastics"] = "Naming container with no calculation API; Fast, Slow and Full nested indicator families are inventoried separately.",
        ["Trady.Indicator.StochasticsOscillator"] = "Naming container with no calculation API; Fast, Slow and Full nested oscillator families are inventoried separately."
    };
}

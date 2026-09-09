using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Builder.Notifications;
using OoplesFinance.StockIndicators.Builder.Trading;
using Xunit;

namespace OoplesFinance.StockIndicators.Tests.ValidationTests;

/// <summary>
/// Tests for signal-name wildcard matching, exercised through the two public
/// surfaces that rely on it: auto-trade rules and notification routes.
/// </summary>
public class SignalPatternMatchingTests
{
    private static bool RuleMatches(string pattern, string signalName) =>
        new ExtendedAutoTradeRule { SignalPattern = pattern }.Matches(signalName);

    private static bool RouteMatches(string pattern, string signalName) =>
        new NotificationRoute { SignalPattern = pattern }
            .Matches(new NotificationEvent(new SignalHandle(0), signalName, 0, DateTime.UtcNow));

    [Theory]
    // Exact matches, case-insensitive.
    [InlineData("MACD Bullish Cross", "MACD Bullish Cross", true)]
    [InlineData("macd bullish cross", "MACD Bullish Cross", true)]
    [InlineData("MACD Bullish Cross", "MACD Bearish Cross", false)]
    // Match-everything.
    [InlineData("*", "anything at all", true)]
    [InlineData("*", "", true)]
    // Trailing wildcard (prefix match).
    [InlineData("RSI*", "RSI Oversold", true)]
    [InlineData("RSI*", "MACD Cross", false)]
    [InlineData("RSI*", "RSI", true)]
    // Leading wildcard (suffix match) - was unsupported and never matched.
    [InlineData("*Oversold", "RSI Oversold", true)]
    [InlineData("*Oversold", "RSI Overbought", false)]
    // Surrounding wildcards (contains) - was unsupported and never matched.
    [InlineData("*Oversold*", "RSI Oversold", true)]
    [InlineData("*Oversold*", "RSI Oversold Cross", true)]
    [InlineData("*oversold*", "RSI OVERSOLD Cross", true)]
    [InlineData("*Oversold*", "RSI Overbought", false)]
    // Interior wildcards.
    [InlineData("RSI*Cross", "RSI Oversold Cross", true)]
    [InlineData("RSI*Cross*", "RSI Oversold Crossover", true)]
    [InlineData("RSI*Cross", "MACD Oversold Cross", false)]
    // A wildcard matches an empty run.
    [InlineData("RSI*Cross", "RSICross", true)]
    // Empty patterns never match.
    [InlineData("", "RSI Oversold", false)]
    public void Patterns_MatchTheSameWayForRulesAndRoutes(string pattern, string signalName, bool expected)
    {
        Assert.Equal(expected, RuleMatches(pattern, signalName));
        Assert.Equal(expected, RouteMatches(pattern, signalName));
    }

    [Theory]
    // Regex metacharacters must be treated as literal text. A pattern-to-regex
    // translation that forgets to escape turns these into wildcards or, for the
    // unbalanced bracket, throws.
    [InlineData("RSI (14)*", "RSI (14) Oversold", true)]
    [InlineData("RSI (14)*", "RSI 914 Oversold", false)]
    [InlineData("Price > $5.00*", "Price > $5.00 Breakout", true)]
    [InlineData("Price > $5.00*", "Price > $5X00 Breakout", false)]
    [InlineData("Signal [A]*", "Signal [A] Fired", true)]
    [InlineData("Signal [*", "Signal [A] Fired", true)]
    [InlineData("A+B*", "A+B Cross", true)]
    [InlineData("A+B*", "AAB Cross", false)]
    public void RegexMetacharactersAreMatchedLiterally(string pattern, string signalName, bool expected)
    {
        Assert.Equal(expected, RuleMatches(pattern, signalName));
        Assert.Equal(expected, RouteMatches(pattern, signalName));
    }

    [Fact]
    public void ConsecutiveWildcardsBehaveAsOne()
    {
        Assert.True(RuleMatches("**", "anything"));
        Assert.True(RuleMatches("RSI**Cross", "RSI Oversold Cross"));
        Assert.True(RouteMatches("**", "anything"));
        Assert.True(RouteMatches("RSI**Cross", "RSI Oversold Cross"));
    }

    [Fact]
    public void BacktrackingPatternCompletesPromptly()
    {
        // A pattern of this shape is the classic catastrophic-backtracking trigger
        // for a regex-based implementation. The linear matcher must stay fast.
        var signalName = new string('a', 2000) + "b";
        Assert.True(RuleMatches("*a*a*a*a*a*b", signalName));
        Assert.False(RuleMatches("*a*a*a*a*a*c", signalName));
    }
}

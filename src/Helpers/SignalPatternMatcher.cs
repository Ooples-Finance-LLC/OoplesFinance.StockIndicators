//     Ooples Finance Stock Indicator Library
//     https://ooples.github.io/OoplesFinance.StockIndicators/
//
//     Copyright © Franklin Moormann, 2020-2022
//     cheatcountry@gmail.com
//
//     This library is free software and it uses the Apache 2.0 license
//     so if you are going to re-use or modify my code then I just ask
//     that you include my copyright info and my contact info in a comment

namespace OoplesFinance.StockIndicators.Helpers;

/// <summary>
/// Matches signal names against simple wildcard patterns.
/// </summary>
/// <remarks>
/// <para>
/// The single supported metacharacter is <c>*</c>, which matches any run of
/// characters including an empty one. It may appear anywhere in the pattern and
/// any number of times, so <c>RSI*</c>, <c>*Oversold</c>, <c>*Oversold*</c> and
/// <c>RSI*Cross*</c> all behave as expected. Every other character is matched
/// literally, case-insensitively.
/// </para>
/// <para>
/// This is deliberately not implemented by translating the pattern into a regular
/// expression. Doing so requires escaping the literal portion, and forgetting that
/// step silently turns ordinary signal names such as <c>RSI (14)</c> or
/// <c>Price &gt; $5.00</c> into regex metacharacters. The linear scan below also
/// removes any possibility of catastrophic backtracking.
/// </para>
/// </remarks>
internal static class SignalPatternMatcher
{
    /// <summary>
    /// Determines whether <paramref name="value"/> matches <paramref name="pattern"/>.
    /// </summary>
    /// <param name="pattern">The wildcard pattern. An empty or null pattern never matches.</param>
    /// <param name="value">The signal name to test.</param>
    /// <returns><c>true</c> when the value matches the pattern; otherwise <c>false</c>.</returns>
    public static bool IsMatch(string? pattern, string? value)
    {
        if (string.IsNullOrEmpty(pattern) || value is null)
        {
            return false;
        }

        // A pattern of only wildcards matches anything, including the empty string.
        if (IsAllWildcards(pattern))
        {
            return true;
        }

        return MatchCore(pattern, value);
    }

    private static bool IsAllWildcards(string pattern)
    {
        foreach (var c in pattern)
        {
            if (c != '*')
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Greedy wildcard match with backtracking limited to the most recent <c>*</c>.
    /// Runs in linear time for the patterns this library uses and never recurses.
    /// </summary>
    private static bool MatchCore(string pattern, string value)
    {
        var patternIndex = 0;
        var valueIndex = 0;
        var lastStarIndex = -1;
        var resumeIndex = 0;

        while (valueIndex < value.Length)
        {
            if (patternIndex < pattern.Length && pattern[patternIndex] == '*')
            {
                // Remember where to resume if the rest of the pattern fails to match.
                lastStarIndex = patternIndex;
                resumeIndex = valueIndex;
                patternIndex++;
            }
            else if (patternIndex < pattern.Length && CharsEqual(pattern[patternIndex], value[valueIndex]))
            {
                patternIndex++;
                valueIndex++;
            }
            else if (lastStarIndex >= 0)
            {
                // Let the last '*' absorb one more character and try again.
                patternIndex = lastStarIndex + 1;
                resumeIndex++;
                valueIndex = resumeIndex;
            }
            else
            {
                return false;
            }
        }

        // Any wildcards left over match the empty remainder.
        while (patternIndex < pattern.Length && pattern[patternIndex] == '*')
        {
            patternIndex++;
        }

        return patternIndex == pattern.Length;
    }

    private static bool CharsEqual(char left, char right) =>
        left == right || char.ToUpperInvariant(left) == char.ToUpperInvariant(right);
}

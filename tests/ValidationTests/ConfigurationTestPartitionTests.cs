using System.Reflection;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Validation;

namespace OoplesFinance.StockIndicators.Tests.Unit.ValidationTests;

public sealed class ConfigurationTestPartitionTests
{
    [Theory]
    [InlineData(typeof(BuiltInSharedValidationTests), "EveryConfigurationSatisfiesTheSharedInvariants", "Cases")]
    [InlineData(typeof(FormulaContractCoverageTests), "EveryReferencedConfigurationPassesItsFormula", "ReferencedCases")]
    public void DiscoveredMethodsCoverEveryConfigurationExactlyOnce(Type suite, string prefix, string source)
    {
        var expected = IndicatorValidationDiscovery.Discover(new[] { typeof(IIndicator).Assembly })
            .Select(c => c.ToString()).OrderBy(n => n, StringComparer.Ordinal).ToArray();
        var methods = suite.GetMethods().Where(m => m.Name.StartsWith(prefix, StringComparison.Ordinal))
            .OrderBy(m => m.Name, StringComparer.Ordinal).ToArray();
        Assert.Equal(ConfigurationTestPartitions.Count, methods.Length);
        var actual = new List<string>();
        var sizes = new List<int>();
        for (var index = 0; index < methods.Length; index++)
        {
            Assert.NotNull(methods[index].GetCustomAttribute<TheoryAttribute>());
            var data = methods[index].GetCustomAttribute<MemberDataAttribute>();
            Assert.NotNull(data);
            Assert.Equal(source, data!.MemberName);
            Assert.Equal(index, Assert.IsType<int>(Assert.Single(data.Parameters)));
            var rows = Assert.IsAssignableFrom<IEnumerable<object[]>>(
                suite.GetMethod(source)!.Invoke(null, data.Parameters)).ToArray();
            sizes.Add(rows.Length);
            actual.AddRange(rows.Select(row => Assert.IsType<IndicatorValidationCase>(Assert.Single(row)).ToString()));
        }
        Assert.Equal(expected, actual.OrderBy(n => n, StringComparer.Ordinal));
        Assert.InRange(sizes.Max() - sizes.Min(), 0, 1);
    }
}

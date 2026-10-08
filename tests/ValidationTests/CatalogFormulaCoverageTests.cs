using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Validation;

namespace OoplesFinance.StockIndicators.Tests.Unit.ValidationTests;

public sealed class CatalogFormulaCoverageTests
{
    [Fact]
    public void EveryCatalogIndicatorAndPublishedOutputMustHaveAnExplicitContractStatus()
    {
        using var stream = typeof(CatalogFormulaCoverageTests).Assembly.GetManifestResourceStream("CatalogFormulaBacklog.txt");
        Assert.NotNull(stream);
        using var reader = new StreamReader(stream!);
        var expected = reader.ReadToEnd().Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
        Assert.Empty(expected);
        Assert.Empty(MissingContracts());
    }

    // Enum/catalog coverage complements configuration coverage: a missing generated type must not
    // disappear from the denominator, nor may a partial typed alias hide legacy secondary outputs.
    internal static string[] MissingContracts()
    {
        var names = new HashSet<IndicatorName>();
        var exposed = new HashSet<string>(StringComparer.Ordinal);
        var referenced = new HashSet<string>(StringComparer.Ordinal);
        foreach (var testCase in IndicatorValidationDiscovery.Discover(new[] { typeof(IIndicator).Assembly }))
        {
            var indicator = testCase.Factory();
            if (indicator is not IBuiltInIndicator builtIn) continue;
            names.Add(builtIn.BatchName);
            var published = GeneratedIndicatorOutputs.KeysFor(builtIn.BatchName);
            var keys = builtIn.BatchOutputKey is { } key ? new[] { key } : published.ToArray();
            Assert.Equal(indicator.Outputs.Count, keys.Length);
            Assert.All(keys, output => Assert.Contains(output, published));
            foreach (var output in keys) exposed.Add(builtIn.BatchName + "/" + output);
            foreach (var slot in IndicatorFormulaCoverage.Inspect(testCase).ReferencedOutputSlots)
                referenced.Add(builtIn.BatchName + "/" + keys[slot]);
        }
        // Multi-market indicators already expose native state types requiring real series keys.
        // Include those implementations without fabricating single-series typed indicators.
        foreach (var testCase in IndicatorValidationDiscovery.DiscoverMultiSeries(new[] { typeof(IIndicator).Assembly }))
        {
            var state = testCase.Factory(MultiSeriesIndicatorValidation.PrimaryKey, MultiSeriesIndicatorValidation.BenchmarkKey);
            try
            {
                names.Add(state.Name);
                var published = GeneratedIndicatorOutputs.KeysFor(state.Name);
                Assert.Equal(published.OrderBy(k => k), testCase.OutputKeys.OrderBy(k => k));
                foreach (var output in testCase.OutputKeys)
                {
                    exposed.Add(state.Name+"/"+output);
                    if (testCase.Reference is not null) referenced.Add(state.Name+"/"+output);
                }
            }
            finally { (state as IDisposable)?.Dispose(); }
        }
        var catalog = Enum.GetValues<IndicatorName>().Where(name => name != IndicatorName.None).ToArray();
        Assert.All(catalog, name => Assert.NotEmpty(GeneratedIndicatorOutputs.KeysFor(name)));
        var outputs = catalog.SelectMany(name => GeneratedIndicatorOutputs.KeysFor(name).Select(key => name + "/" + key)).ToArray();
        return catalog.Where(name => !names.Contains(name)).Select(name => "No type: " + name)
            .Concat(outputs.Except(exposed).Select(output => "Unexposed: " + output))
            .Concat(outputs.Except(referenced).Select(output => "Unreferenced: " + output))
            .OrderBy(entry => entry, StringComparer.Ordinal).ToArray();
    }
}

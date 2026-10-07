using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Builder.Specs;
using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Streaming;
using OoplesFinance.StockIndicators.Validation;
namespace OoplesFinance.StockIndicators.Tests.Unit.ValidationTests;
public sealed class PivotPointAliasNumericalTests
{
    public static IEnumerable<object[]> Cases=>IndicatorValidationDiscovery.Discover(new[]{typeof(IIndicator).Assembly}).Where(c=>c.IndicatorType==typeof(PivotPoint)).Select(c=>new object[]{c});
    public static IEnumerable<object[]> Routes=>Cases.SelectMany(c=>new[]{"batch","fast","arm","native","streaming"}.Select(route=>new object[]{c[0],route}));
    [Theory,MemberData(nameof(Routes))]
    public void EveryRouteKeepsTheFloorFormula(IndicatorValidationCase testCase,string route)=>new OrdinalFamilyNumericalTests().CheckRoutes(testCase,route,BuiltInFormulaReferences.FloorPivotOutputs,IndicatorErrorBudget.Exact);
    [Theory,MemberData(nameof(Cases))]
    public Task SelectedSourcePreservesTheFormula(IndicatorValidationCase testCase)=>new OrdinalFamilyNumericalTests().SelectedSourcePreservesTheFormulaAndOriginalCandleFields(testCase);
    [Theory,MemberData(nameof(Cases))]
    public void EveryOutputRejectsInjectedFaults(IndicatorValidationCase testCase)=>new OrdinalFamilyNumericalTests().EveryPublishedOutputRejectsAnInjectedValueFault(testCase);
    [Theory,MemberData(nameof(Cases))]
    public Task EveryConfigurationReceivesNumericalFixtures(IndicatorValidationCase testCase)=>new OrdinalFamilyNumericalTests().PublicConfigurationsPassEveryNumericalClass(testCase);
    [Fact]
    public void CompatibilityLengthDoesNotChangeCompletedSessionLevels()
    {
        var bars=new[]{new Bar(DateTime.UnixEpoch,1,6,0,3,1),new Bar(DateTime.UnixEpoch.AddHours(1),2,9,1,6,1),new Bar(DateTime.UnixEpoch.AddDays(1),0,0,0,0,1)};
        var expected=BuiltInFormulaReferences.FloorPivotOutputs(bars);
        foreach(var length in new[]{0,1,14,int.MaxValue})
        {
            IBuiltInIndicator indicator=new PivotPoint(length);var spec=new IndicatorSpec(indicator.BatchName,indicator.CreateOptions());
            foreach(var state in new[]{StatefulIndicatorFactory.Create(spec),StreamingIndicatorFactory.CreateState(spec)})
            {
                Assert.NotNull(state);
                for(var replay=0;replay<2;replay++)
                {
                    state.Reset();
                    for(var i=0;i<bars.Length;i++)
                    {
                        var b=bars[i];var bar=new OhlcvBar("ALIAS",BarTimeframe.Minutes(1),b.Time,b.Time,b.Open,b.High,b.Low,b.Close,b.Volume,true);
                        foreach(var final in new[]{false,false,true})
                        {
                            var result=state.Update(bar,final,true);
                            foreach(var key in expected.Keys)Assert.Equal(expected[key][i],result.Outputs![key]);
                        }
                    }
                }
            }
        }
    }
}

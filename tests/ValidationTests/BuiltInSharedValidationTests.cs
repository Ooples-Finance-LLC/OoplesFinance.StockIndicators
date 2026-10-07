using OoplesFinance.StockIndicators.Indicators;
using OoplesFinance.StockIndicators.Validation;

namespace OoplesFinance.StockIndicators.Tests.Unit.ValidationTests;

public sealed partial class BuiltInSharedValidationTests
{
    // Separate methods let the CI method inventory distribute this sweep across 20 workers.
    public static IEnumerable<object[]> Cases(int partition) => ConfigurationTestPartitions.Select(
        IndicatorValidationDiscovery.Discover(new[] { typeof(IIndicator).Assembly }), partition);

    [Theory]
    [MemberData(nameof(Cases), 0)]
    public Task EveryConfigurationSatisfiesTheSharedInvariants00(IndicatorValidationCase testCase)
        => IndicatorValidation.ValidateAndThrowAsync(testCase, new() { RequireFormulaReference = true });

    [Theory]
    [MemberData(nameof(Cases), 1)]
    public Task EveryConfigurationSatisfiesTheSharedInvariants01(IndicatorValidationCase testCase)
        => IndicatorValidation.ValidateAndThrowAsync(testCase, new() { RequireFormulaReference = true });

    [Theory]
    [MemberData(nameof(Cases), 2)]
    public Task EveryConfigurationSatisfiesTheSharedInvariants02(IndicatorValidationCase testCase)
        => IndicatorValidation.ValidateAndThrowAsync(testCase, new() { RequireFormulaReference = true });

    [Theory]
    [MemberData(nameof(Cases), 3)]
    public Task EveryConfigurationSatisfiesTheSharedInvariants03(IndicatorValidationCase testCase)
        => IndicatorValidation.ValidateAndThrowAsync(testCase, new() { RequireFormulaReference = true });

    [Theory]
    [MemberData(nameof(Cases), 4)]
    public Task EveryConfigurationSatisfiesTheSharedInvariants04(IndicatorValidationCase testCase)
        => IndicatorValidation.ValidateAndThrowAsync(testCase, new() { RequireFormulaReference = true });

    [Theory]
    [MemberData(nameof(Cases), 5)]
    public Task EveryConfigurationSatisfiesTheSharedInvariants05(IndicatorValidationCase testCase)
        => IndicatorValidation.ValidateAndThrowAsync(testCase, new() { RequireFormulaReference = true });

    [Theory]
    [MemberData(nameof(Cases), 6)]
    public Task EveryConfigurationSatisfiesTheSharedInvariants06(IndicatorValidationCase testCase)
        => IndicatorValidation.ValidateAndThrowAsync(testCase, new() { RequireFormulaReference = true });

    [Theory]
    [MemberData(nameof(Cases), 7)]
    public Task EveryConfigurationSatisfiesTheSharedInvariants07(IndicatorValidationCase testCase)
        => IndicatorValidation.ValidateAndThrowAsync(testCase, new() { RequireFormulaReference = true });

    [Theory]
    [MemberData(nameof(Cases), 8)]
    public Task EveryConfigurationSatisfiesTheSharedInvariants08(IndicatorValidationCase testCase)
        => IndicatorValidation.ValidateAndThrowAsync(testCase, new() { RequireFormulaReference = true });

    [Theory]
    [MemberData(nameof(Cases), 9)]
    public Task EveryConfigurationSatisfiesTheSharedInvariants09(IndicatorValidationCase testCase)
        => IndicatorValidation.ValidateAndThrowAsync(testCase, new() { RequireFormulaReference = true });

    [Theory]
    [MemberData(nameof(Cases), 10)]
    public Task EveryConfigurationSatisfiesTheSharedInvariants10(IndicatorValidationCase testCase)
        => IndicatorValidation.ValidateAndThrowAsync(testCase, new() { RequireFormulaReference = true });

    [Theory]
    [MemberData(nameof(Cases), 11)]
    public Task EveryConfigurationSatisfiesTheSharedInvariants11(IndicatorValidationCase testCase)
        => IndicatorValidation.ValidateAndThrowAsync(testCase, new() { RequireFormulaReference = true });

    [Theory]
    [MemberData(nameof(Cases), 12)]
    public Task EveryConfigurationSatisfiesTheSharedInvariants12(IndicatorValidationCase testCase)
        => IndicatorValidation.ValidateAndThrowAsync(testCase, new() { RequireFormulaReference = true });

    [Theory]
    [MemberData(nameof(Cases), 13)]
    public Task EveryConfigurationSatisfiesTheSharedInvariants13(IndicatorValidationCase testCase)
        => IndicatorValidation.ValidateAndThrowAsync(testCase, new() { RequireFormulaReference = true });

    [Theory]
    [MemberData(nameof(Cases), 14)]
    public Task EveryConfigurationSatisfiesTheSharedInvariants14(IndicatorValidationCase testCase)
        => IndicatorValidation.ValidateAndThrowAsync(testCase, new() { RequireFormulaReference = true });

    [Theory]
    [MemberData(nameof(Cases), 15)]
    public Task EveryConfigurationSatisfiesTheSharedInvariants15(IndicatorValidationCase testCase)
        => IndicatorValidation.ValidateAndThrowAsync(testCase, new() { RequireFormulaReference = true });

    [Theory]
    [MemberData(nameof(Cases), 16)]
    public Task EveryConfigurationSatisfiesTheSharedInvariants16(IndicatorValidationCase testCase)
        => IndicatorValidation.ValidateAndThrowAsync(testCase, new() { RequireFormulaReference = true });

    [Theory]
    [MemberData(nameof(Cases), 17)]
    public Task EveryConfigurationSatisfiesTheSharedInvariants17(IndicatorValidationCase testCase)
        => IndicatorValidation.ValidateAndThrowAsync(testCase, new() { RequireFormulaReference = true });

    [Theory]
    [MemberData(nameof(Cases), 18)]
    public Task EveryConfigurationSatisfiesTheSharedInvariants18(IndicatorValidationCase testCase)
        => IndicatorValidation.ValidateAndThrowAsync(testCase, new() { RequireFormulaReference = true });

    [Theory]
    [MemberData(nameof(Cases), 19)]
    public Task EveryConfigurationSatisfiesTheSharedInvariants19(IndicatorValidationCase testCase)
        => IndicatorValidation.ValidateAndThrowAsync(testCase, new() { RequireFormulaReference = true });

}

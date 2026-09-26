using OoplesFinance.StockIndicators.Builder;

namespace OoplesFinance.StockIndicators.Tests.Unit.ValidationTests;

public sealed class CatalogInputContractTests
{
    public static IEnumerable<object[]> InvalidFields()
    {
        foreach (var value in new[] { double.NaN, double.PositiveInfinity, double.NegativeInfinity })
            for (var field = 0; field < 7; field++)
                yield return new object[] { field, value };
    }

    [Theory]
    [MemberData(nameof(InvalidFields))]
    public void CatalogRejectsNonfiniteRawAndEffectiveInputs(int field, double invalid)
    {
        var data = Data();
        var builder = Builder(data);
        // Mutation after wrapping the source must still be checked at the execution boundary.
        Column(data, field)[1] = invalid;
        using var runtime = builder.Build();
        var error = Assert.Throws<ArgumentOutOfRangeException>(runtime.Start);
        Assert.Contains("bar 1", error.Message);
    }

    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(2)] [InlineData(3)]
    [InlineData(4)] [InlineData(5)] [InlineData(6)] [InlineData(7)]
    public void CatalogRejectsUnequalColumnLengths(int field)
    {
        var data = Data();
        if (field == 7) data.Dates.RemoveAt(0);
        else Column(data, field).RemoveAt(0);
        using var runtime = Builder(data).Build();
        Assert.Throws<ArgumentException>(runtime.Start);
    }

    [Fact]
    public void NamedBenchmarkCannotBypassValidation()
    {
        var builder = Builder(Data());
        var benchmark = Data();
        benchmark.Volumes[1] = double.NaN;
        builder.AddDataSource("market", IndicatorDataSource.FromBatch(benchmark));
        builder.ConfigureIndicators(catalog => catalog.RSMKIndicator(catalog.Price("market"), 1, 1));
        using var runtime = builder.Build();
        Assert.Throws<ArgumentOutOfRangeException>(runtime.Start);
    }

    [Fact]
    public void SignedFiniteInputsRetainTheirMeaning()
    {
        var data = Data();
        data.InputValues = new() { -2, 0, 4 };
        var builder = new StockIndicatorBuilder(IndicatorDataSource.FromBatch(data));
        SeriesHandle handle = default;
        builder.ConfigureIndicators(catalog => handle = catalog.Sma(2));
        using var runtime = builder.Build();
        runtime.Subscribe(handle);
        runtime.Start();
        Assert.Equal(new[] { 0d, -1d, 2d }, runtime.GetSeries(handle));
    }

    private static StockIndicatorBuilder Builder(StockData data)
    {
        var builder = new StockIndicatorBuilder(IndicatorDataSource.FromBatch(data));
        builder.ConfigureIndicators(catalog => catalog.Sma(2));
        return builder;
    }

    private static StockData Data() => new(Enumerable.Range(0, 3).Select(i => new TickerData
    { Date = new DateTime(2024, 1, 1).AddDays(i), Open = 10 + i, High = 10 + i, Low = 10 + i, Close = 10 + i, Volume = 1 }));

    private static List<double> Column(StockData data, int field)
    {
        if (field == 6 && data.CustomValuesList.Count == 0) data.CustomValuesList = new(data.ClosePrices);
        return field switch
        {
            0 => data.OpenPrices, 1 => data.HighPrices, 2 => data.LowPrices, 3 => data.ClosePrices,
            4 => data.Volumes, 5 => data.InputValues, _ => data.CustomValuesList
        };
    }
}

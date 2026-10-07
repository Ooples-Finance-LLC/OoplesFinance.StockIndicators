using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Builder.Compute;

namespace OoplesFinance.StockIndicators.Tests.Unit.ModelsTests;

public sealed class AdoptedInputTests
{
    private static StockData Data() => StockData.FromColumnViews(
        new[] { 2d, 4, 8 }, new[] { 20d, 20, 20 }, new[] { 0d, 0, 0 },
        new[] { 2d, 4, 8 }, new[] { 1d, 1, 1 },
        Enumerable.Range(0, 3).Select(i => new DateTime(2024, 1, 1).AddDays(i)).ToArray());

    private static double[] Read(StockData data, string accessor) => accessor switch
    {
        "list" => data.InputValues.ToArray(),
        "span" => data.InputSpan.ToArray(),
        "memory" => data.InputMemory.ToArray(),
        _ => throw new ArgumentException(nameof(accessor))
    };

    [Theory]
    [InlineData("list")] [InlineData("span")] [InlineData("memory")]
    public void FirstDefaultInputReadUsesEditedCloseList(string accessor)
    {
        var data = Data();
        data.ClosePrices[1] = 12;
        Assert.Equal(new[] { 2d, 12, 8 }, Read(data, accessor));
    }

    [Theory]
    [InlineData("span")] [InlineData("memory")]
    public void ReadingAViewDoesNotFreezeDefaultInput(string accessor)
    {
        var data = Data();
        Assert.Equal(new[] { 2d, 4, 8 }, Read(data, accessor));
        data.ClosePrices[1] = 12;
        Assert.Equal(new[] { 2d, 12, 8 }, Read(data, accessor));
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void ExistingInputCopyOrExplicitSelectionKeepsPrecedence(bool explicitSelection)
    {
        var data = Data();
        if (explicitSelection) data.InputValues = new() { -2, 0, 4 };
        var selected = data.InputValues;
        var expected = selected.ToArray();
        data.ClosePrices[1] = 12;
        Assert.Same(selected, data.InputValues);
        foreach (var accessor in new[] { "list", "span", "memory" })
            Assert.Equal(expected, Read(data, accessor));
    }

    [Theory]
    [InlineData("batch")] [InlineData("fast")] [InlineData("builder")]
    public void MovingAverageUsesEditedClosesAcrossRoutes(string route)
    {
        var data = Data();
        data.ClosePrices[1] = 12;
        double[] actual;
        if (route == "batch")
            actual = data.CalculateSimpleMovingAverage(2).CustomValuesList.ToArray();
        else if (route == "fast")
        {
            using var context = new ComputeContext();
            using var buffer = IndicatorCompute.ComputeSmaFast(data, context, 2);
            actual = buffer.ToArray();
        }
        else
        {
            SeriesHandle handle = default;
            using var runtime = new StockIndicatorBuilder(IndicatorDataSource.FromBatch(data))
                .ConfigureIndicators(catalog => handle = catalog.Sma(2)).Build();
            runtime.Start();
            actual = runtime.GetSeries(handle).AsSpan().ToArray();
        }
        // Two-bar means of [2, 12, 8], with the public zero warmup.
        Assert.Equal(new[] { 0d, 7, 10 }, actual);
    }
}

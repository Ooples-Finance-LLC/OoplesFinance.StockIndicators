namespace OoplesFinance.StockIndicators.Tests.Unit.IntegrationTests;

using FluentAssertions;
using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Models;

/// <summary>
/// Integration tests for multi-stock comparison indicators.
/// These indicators compare a stock against a market benchmark (e.g., S&P 500).
/// </summary>
public sealed class MultiStockIndicatorTests : GlobalTestData
{
    private StockData CreateMarketData(int count)
    {
        // Create market data (simulating S&P 500)
        var tickerList = new List<TickerData>();
        var basePrice = 4500.0; // Typical S&P 500 level

        for (var i = 0; i < count; i++)
        {
            var date = DateTime.Today.AddDays(-count + i);
            var change = (i % 2 == 0 ? 1 : -1) * (i % 10 + 1);
            var price = basePrice + change;
            tickerList.Add(new TickerData
            {
                Date = date,
                Open = price - 5,
                High = price + 10,
                Low = price - 10,
                Close = price,
                Volume = 1_000_000_000 + i * 10_000_000
            });
        }

        return new StockData(tickerList);
    }

    [Fact]
    public void RSMKIndicator_ShouldComputeValidValues()
    {
        // Arrange
        var stockData = new StockData(StockTestData);
        var marketData = CreateMarketData(stockData.Count);

        var stockSource = IndicatorDataSource.FromBatch(stockData);
        var marketSource = IndicatorDataSource.FromBatch(marketData);

        var builder = new StockIndicatorBuilder(stockSource);
        builder.AddDataSource("market", marketSource);

        SeriesHandle? rsmkHandle = null;
        builder.ConfigureIndicators(catalog =>
        {
            var marketPrice = catalog.Price("market");
            rsmkHandle = catalog.RSMKIndicator(marketPrice, 90, 3);
        });

        // Act
        var runtime = builder.Build();
        runtime.Start();

        // Assert
        rsmkHandle.Should().NotBeNull();
        runtime.Subscribe(rsmkHandle!.Value);
        var series = runtime.GetSeries(rsmkHandle!.Value);

        series.Count.Should().BeGreaterThan(0, "RSMK should produce output values");

        // After warmup, values should be valid (not NaN)
        var nonNanCount = series.Count(v => !double.IsNaN(v));
        nonNanCount.Should().BeGreaterThan(0, "RSMK should have valid non-NaN values");
    }

    [Fact]
    public void ComparePriceMomentumOscillator_ShouldComputeValidValues()
    {
        // Arrange
        var stockData = new StockData(StockTestData);
        var marketData = CreateMarketData(stockData.Count);

        var stockSource = IndicatorDataSource.FromBatch(stockData);
        var marketSource = IndicatorDataSource.FromBatch(marketData);

        var builder = new StockIndicatorBuilder(stockSource);
        builder.AddDataSource("spy", marketSource);

        SeriesHandle? cpmoHandle = null;
        builder.ConfigureIndicators(catalog =>
        {
            var marketPrice = catalog.Price("spy");
            cpmoHandle = catalog.ComparePriceMomentumOscillator(marketPrice, 20, 35, 10);
        });

        // Act
        var runtime = builder.Build();
        runtime.Start();

        // Assert
        cpmoHandle.Should().NotBeNull();
        runtime.Subscribe(cpmoHandle!.Value);
        var series = runtime.GetSeries(cpmoHandle!.Value);

        series.Count.Should().BeGreaterThan(0);
        var nonNanCount = series.Count(v => !double.IsNaN(v));
        nonNanCount.Should().BeGreaterThan(0, "CPMO should have valid non-NaN values");
    }

    [Fact]
    public void KaufmanStressIndicator_ShouldComputeValidValues()
    {
        // Arrange
        var stockData = new StockData(StockTestData);
        var marketData = CreateMarketData(stockData.Count);

        var stockSource = IndicatorDataSource.FromBatch(stockData);
        var marketSource = IndicatorDataSource.FromBatch(marketData);

        var builder = new StockIndicatorBuilder(stockSource);
        builder.AddDataSource("market", marketSource);

        SeriesHandle? stressHandle = null;
        builder.ConfigureIndicators(catalog =>
        {
            var marketPrice = catalog.Price("market");
            stressHandle = catalog.KaufmanStressIndicator(marketPrice, 60);
        });

        // Act
        var runtime = builder.Build();
        runtime.Start();

        // Assert
        stressHandle.Should().NotBeNull();
        runtime.Subscribe(stressHandle!.Value);
        var series = runtime.GetSeries(stressHandle!.Value);

        series.Count.Should().BeGreaterThan(0);
    }

    [Fact]
    public void RelativeNormalizedVolatility_ShouldComputeValidValues()
    {
        // Arrange
        var stockData = new StockData(StockTestData);
        var marketData = CreateMarketData(stockData.Count);

        var stockSource = IndicatorDataSource.FromBatch(stockData);
        var marketSource = IndicatorDataSource.FromBatch(marketData);

        var builder = new StockIndicatorBuilder(stockSource);
        builder.AddDataSource("benchmark", marketSource);

        SeriesHandle? rnvHandle = null;
        builder.ConfigureIndicators(catalog =>
        {
            var marketPrice = catalog.Price("benchmark");
            rnvHandle = catalog.RelativeNormalizedVolatility(marketPrice, 14);
        });

        // Act
        var runtime = builder.Build();
        runtime.Start();

        // Assert
        rnvHandle.Should().NotBeNull();
        runtime.Subscribe(rnvHandle!.Value);
        var series = runtime.GetSeries(rnvHandle!.Value);

        series.Count.Should().BeGreaterThan(0);
    }

    [Fact]
    public void RelativeStrength3DIndicator_ShouldComputeValidValues()
    {
        // Arrange
        var stockData = new StockData(StockTestData);
        var marketData = CreateMarketData(stockData.Count);

        var stockSource = IndicatorDataSource.FromBatch(stockData);
        var marketSource = IndicatorDataSource.FromBatch(marketData);

        var builder = new StockIndicatorBuilder(stockSource);
        builder.AddDataSource("market", marketSource);

        SeriesHandle? rs3dHandle = null;
        builder.ConfigureIndicators(catalog =>
        {
            var marketPrice = catalog.Price("market");
            rs3dHandle = catalog.RelativeStrength3DIndicator(marketPrice, 4, 7, 10, 15, 30);
        });

        // Act
        var runtime = builder.Build();
        runtime.Start();

        // Assert
        rs3dHandle.Should().NotBeNull();
        runtime.Subscribe(rs3dHandle!.Value);
        var series = runtime.GetSeries(rs3dHandle!.Value);

        series.Count.Should().BeGreaterThan(0);
    }

    [Fact]
    public void SectorRotationModel_ShouldComputeValidValues()
    {
        // Arrange
        var stockData = new StockData(StockTestData);
        var marketData = CreateMarketData(stockData.Count);

        var stockSource = IndicatorDataSource.FromBatch(stockData);
        var marketSource = IndicatorDataSource.FromBatch(marketData);

        var builder = new StockIndicatorBuilder(stockSource);
        builder.AddDataSource("market", marketSource);

        SeriesHandle? srmHandle = null;
        builder.ConfigureIndicators(catalog =>
        {
            var marketPrice = catalog.Price("market");
            srmHandle = catalog.SectorRotationModel(marketPrice, 25, 75);
        });

        // Act
        var runtime = builder.Build();
        runtime.Start();

        // Assert
        srmHandle.Should().NotBeNull();
        runtime.Subscribe(srmHandle!.Value);
        var series = runtime.GetSeries(srmHandle!.Value);

        series.Count.Should().BeGreaterThan(0);
    }

    [Fact]
    public void MultipleMultiStockIndicators_ShouldWorkTogether()
    {
        // Arrange
        var stockData = new StockData(StockTestData);
        var marketData = CreateMarketData(stockData.Count);

        var stockSource = IndicatorDataSource.FromBatch(stockData);
        var marketSource = IndicatorDataSource.FromBatch(marketData);

        var builder = new StockIndicatorBuilder(stockSource);
        builder.AddDataSource("market", marketSource);

        SeriesHandle? rsmkHandle = null;
        SeriesHandle? cpmoHandle = null;
        SeriesHandle? srmHandle = null;

        builder.ConfigureIndicators(catalog =>
        {
            var marketPrice = catalog.Price("market");
            rsmkHandle = catalog.RSMKIndicator(marketPrice);
            cpmoHandle = catalog.ComparePriceMomentumOscillator(marketPrice);
            srmHandle = catalog.SectorRotationModel(marketPrice);
        });

        // Act
        var runtime = builder.Build();
        runtime.Start();

        // Assert - all three indicators should produce output
        runtime.Subscribe(rsmkHandle!.Value);
        runtime.Subscribe(cpmoHandle!.Value);
        runtime.Subscribe(srmHandle!.Value);

        var rsmkSeries = runtime.GetSeries(rsmkHandle!.Value);
        var cpmoSeries = runtime.GetSeries(cpmoHandle!.Value);
        var srmSeries = runtime.GetSeries(srmHandle!.Value);

        rsmkSeries.Count.Should().BeGreaterThan(0);
        cpmoSeries.Count.Should().BeGreaterThan(0);
        srmSeries.Count.Should().BeGreaterThan(0);
    }

    [Fact]
    public void MissingDataSource_ShouldThrowClearError()
    {
        // Arrange
        var stockData = new StockData(StockTestData);
        var stockSource = IndicatorDataSource.FromBatch(stockData);

        var builder = new StockIndicatorBuilder(stockSource);
        // Note: NOT adding the market data source

        // Act & Assert
        var action = () => builder.ConfigureIndicators(catalog =>
        {
            var marketPrice = catalog.Price("nonexistent"); // This should throw
        });

        action.Should().Throw<InvalidOperationException>()
            .WithMessage("*not found*");
    }

    [Fact]
    public void DataSourceNames_ShouldBeCaseInsensitive()
    {
        // Arrange
        var stockData = new StockData(StockTestData);
        var marketData = CreateMarketData(stockData.Count);

        var stockSource = IndicatorDataSource.FromBatch(stockData);
        var marketSource = IndicatorDataSource.FromBatch(marketData);

        var builder = new StockIndicatorBuilder(stockSource);
        builder.AddDataSource("MARKET", marketSource); // Uppercase

        SeriesHandle? handle = null;
        builder.ConfigureIndicators(catalog =>
        {
            var marketPrice = catalog.Price("market"); // Lowercase - should work
            handle = catalog.RSMKIndicator(marketPrice);
        });

        // Act
        var runtime = builder.Build();
        runtime.Start();

        // Assert
        runtime.Subscribe(handle!.Value);
        var series = runtime.GetSeries(handle!.Value);
        series.Count.Should().BeGreaterThan(0);
    }
}

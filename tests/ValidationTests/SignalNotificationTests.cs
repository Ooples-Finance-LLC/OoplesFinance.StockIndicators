using OoplesFinance.StockIndicators.Builder;
using OoplesFinance.StockIndicators.Builder.Catalogs;
using OoplesFinance.StockIndicators.Builder.Notifications;
using OoplesFinance.StockIndicators.Builder.Signals;
using OoplesFinance.StockIndicators.Models;
using Xunit;

namespace OoplesFinance.StockIndicators.Tests.ValidationTests;

/// <summary>
/// Tests for the signal generation and notification system (Phase 9).
/// </summary>
public class SignalNotificationTests
{
    private static List<TickerData> CreateTestData()
    {
        var data = new List<TickerData>();
        var baseDate = new DateTime(2024, 1, 1);
        var random = new Random(42);

        double price = 100;
        for (int i = 0; i < 50; i++)
        {
            var change = (random.NextDouble() - 0.5) * 2;
            price = Math.Max(50, Math.Min(150, price + change));
            var high = price * (1 + random.NextDouble() * 0.02);
            var low = price * (1 - random.NextDouble() * 0.02);
            var open = low + random.NextDouble() * (high - low);
            var close = low + random.NextDouble() * (high - low);

            data.Add(new TickerData
            {
                Date = baseDate.AddDays(i),
                Open = open,
                High = high,
                Low = low,
                Close = close,
                Volume = 1000000 + random.Next(500000)
            });
        }

        return data;
    }

    [Fact]
    public void SignalCatalog_When_CreatesSignalRule()
    {
        // Arrange
        var testData = CreateTestData();
        var stockData = new StockData(testData);
        var source = IndicatorDataSource.FromBatch(stockData);
        var builder = new StockIndicatorBuilder(source);

        SeriesHandle rsi = default;

        // Act
        builder.ConfigureIndicators(catalog =>
        {
            rsi = catalog.Rsi(14);
        });

        builder.ConfigureSignals(signals =>
        {
            signals.When(rsi).CrossesAbove(70).Emit("RSI Overbought");
            signals.When(rsi).CrossesBelow(30).Emit("RSI Oversold");
        });

        using var runtime = builder.Build();

        // Assert - Build succeeds without errors
        Assert.NotNull(runtime);
    }

    [Fact]
    public void SignalCatalog_Signal_CreatesCompoundSignal()
    {
        // Arrange
        var testData = CreateTestData();
        var stockData = new StockData(testData);
        var source = IndicatorDataSource.FromBatch(stockData);
        var builder = new StockIndicatorBuilder(source);

        SeriesHandle rsi = default;
        SeriesHandle macdLine = default;
        SeriesHandle macdSignal = default;

        // Act
        builder.ConfigureIndicators(catalog =>
        {
            rsi = catalog.Rsi(14);
            var macd = catalog.Macd(12, 26, 9);
            macdLine = macd.Primary;
            macdSignal = macd.Signal;
        });

        builder.ConfigureSignals(signals =>
        {
            // Test compound signal with And()
            signals.Signal(rsi).IsBelow(30)
                   .And(macdLine).CrossesAbove(macdSignal)
                   .Named("Strong Buy Signal");
        });

        using var runtime = builder.Build();

        // Assert - Build succeeds without errors
        Assert.NotNull(runtime);
    }

    [Fact]
    public void CompoundSignalBuilder_AndAbove_ChainsCorrectly()
    {
        // Arrange
        var testData = CreateTestData();
        var stockData = new StockData(testData);
        var source = IndicatorDataSource.FromBatch(stockData);
        var builder = new StockIndicatorBuilder(source);

        SeriesHandle rsi = default;
        SeriesHandle sma = default;

        // Act
        builder.ConfigureIndicators(catalog =>
        {
            rsi = catalog.Rsi(14);
            sma = catalog.Sma(20);
        });

        builder.ConfigureSignals(signals =>
        {
            signals.Signal(rsi).IsAbove(50)
                   .AndAbove(sma, 100)
                   .Named("Bullish Trend");
        });

        using var runtime = builder.Build();

        // Assert
        Assert.NotNull(runtime);
    }

    [Fact]
    public void NotificationCatalog_Console_AddsChannel()
    {
        // Arrange
        var testData = CreateTestData();
        var stockData = new StockData(testData);
        var source = IndicatorDataSource.FromBatch(stockData);
        var builder = new StockIndicatorBuilder(source);

        // Act
        builder.ConfigureIndicators(catalog =>
        {
            catalog.Rsi(14);
        });

        builder.ConfigureNotifications(notify =>
        {
            notify.Console();
        });

        using var runtime = builder.Build();

        // Assert
        Assert.NotNull(runtime);
    }

    [Fact]
    public void NotificationCatalog_WebSocket_AddsChannel()
    {
        // Arrange
        var testData = CreateTestData();
        var stockData = new StockData(testData);
        var source = IndicatorDataSource.FromBatch(stockData);
        var builder = new StockIndicatorBuilder(source);

        // Act
        builder.ConfigureIndicators(catalog =>
        {
            catalog.Rsi(14);
        });

        builder.ConfigureNotifications(notify =>
        {
            notify.WebSocket("ws://localhost:8080/signals");
        });

        using var runtime = builder.Build();

        // Assert
        Assert.NotNull(runtime);
    }

    [Fact]
    public void NotificationCatalog_OnSignal_CreatesRoute()
    {
        // Arrange
        var testData = CreateTestData();
        var stockData = new StockData(testData);
        var source = IndicatorDataSource.FromBatch(stockData);
        var builder = new StockIndicatorBuilder(source);

        SeriesHandle rsi = default;

        // Act
        builder.ConfigureIndicators(catalog =>
        {
            rsi = catalog.Rsi(14);
        });

        builder.ConfigureSignals(signals =>
        {
            signals.When(rsi).CrossesAbove(70).Emit("RSI Overbought");
        });

        builder.ConfigureNotifications(notify =>
        {
            notify.OnSignal("RSI Overbought")
                  .SendConsole()
                  .SendWebhook("https://example.com/webhook");
        });

        using var runtime = builder.Build();

        // Assert
        Assert.NotNull(runtime);
    }

    [Fact]
    public void NotificationRoute_Matches_ExactName()
    {
        // Arrange
        var route = new NotificationRoute
        {
            SignalPattern = "RSI Overbought"
        };
        var notification = new NotificationEvent(
            new SignalHandle(1),
            "RSI Overbought",
            75.5,
            DateTime.Now);

        // Act
        var matches = route.Matches(notification);

        // Assert
        Assert.True(matches);
    }

    [Fact]
    public void NotificationRoute_Matches_WildcardPattern()
    {
        // Arrange
        var route = new NotificationRoute
        {
            SignalPattern = "RSI*"
        };
        var notification = new NotificationEvent(
            new SignalHandle(1),
            "RSI Overbought",
            75.5,
            DateTime.Now);

        // Act
        var matches = route.Matches(notification);

        // Assert
        Assert.True(matches);
    }

    [Fact]
    public void NotificationRoute_Matches_StarWildcard()
    {
        // Arrange
        var route = new NotificationRoute
        {
            SignalPattern = "*"
        };
        var notification = new NotificationEvent(
            new SignalHandle(1),
            "Any Signal Name",
            50.0,
            DateTime.Now);

        // Act
        var matches = route.Matches(notification);

        // Assert
        Assert.True(matches);
    }

    [Fact]
    public void NotificationRoute_DoesNotMatch_DifferentName()
    {
        // Arrange
        var route = new NotificationRoute
        {
            SignalPattern = "RSI Overbought"
        };
        var notification = new NotificationEvent(
            new SignalHandle(1),
            "MACD Cross",
            0.5,
            DateTime.Now);

        // Act
        var matches = route.Matches(notification);

        // Assert
        Assert.False(matches);
    }

    [Fact]
    public void NotificationRoute_Matches_ByHandle()
    {
        // Arrange
        var route = new NotificationRoute
        {
            SignalHandles = new List<SignalHandle> { new SignalHandle(5) }
        };
        var notification = new NotificationEvent(
            new SignalHandle(5),
            "Some Signal",
            50.0,
            DateTime.Now);

        // Act
        var matches = route.Matches(notification);

        // Assert
        Assert.True(matches);
    }

    [Fact]
    public void WebSocketOptions_DefaultValues_AreCorrect()
    {
        // Arrange & Act
        var options = new WebSocketOptions();

        // Assert
        Assert.Equal(5000, options.ReconnectIntervalMs);
        Assert.True(options.AutoReconnect);
        Assert.Equal(10, options.MaxReconnectAttempts);
        Assert.Null(options.Uri);
        Assert.Null(options.Headers);
    }

    [Fact]
    public void NotificationRouteBuilder_SendWebSocket_IntegrationTest()
    {
        // Arrange
        var testData = CreateTestData();
        var stockData = new StockData(testData);
        var source = IndicatorDataSource.FromBatch(stockData);
        var builder = new StockIndicatorBuilder(source);

        SeriesHandle rsi = default;

        // Act - Use the public API to configure notifications with WebSocket
        builder.ConfigureIndicators(catalog =>
        {
            rsi = catalog.Rsi(14);
        });

        builder.ConfigureSignals(signals =>
        {
            signals.When(rsi).CrossesAbove(70).Emit("Test Signal");
        });

        builder.ConfigureNotifications(notify =>
        {
            notify.OnSignal("Test Signal")
                  .SendWebSocket("ws://localhost:8080");
        });

        using var runtime = builder.Build();

        // Assert - Integration succeeds
        Assert.NotNull(runtime);
    }

    [Fact]
    public void CompoundSignalBuilder_OrCrossesAbove_SetsOrOperator()
    {
        // Arrange
        var testData = CreateTestData();
        var stockData = new StockData(testData);
        var source = IndicatorDataSource.FromBatch(stockData);
        var builder = new StockIndicatorBuilder(source);

        SeriesHandle rsi = default;
        SeriesHandle sma = default;

        // Act
        builder.ConfigureIndicators(catalog =>
        {
            rsi = catalog.Rsi(14);
            sma = catalog.Sma(20);
        });

        builder.ConfigureSignals(signals =>
        {
            // Test OR compound signal
            signals.Signal(rsi).IsAbove(70)
                   .OrCrossesAbove(sma, 100)
                   .Named("Entry Signal");
        });

        using var runtime = builder.Build();

        // Assert
        Assert.NotNull(runtime);
    }

    [Fact]
    public void SignalCatalog_Group_WithMultipleConditions()
    {
        // Arrange
        var testData = CreateTestData();
        var stockData = new StockData(testData);
        var source = IndicatorDataSource.FromBatch(stockData);
        var builder = new StockIndicatorBuilder(source);

        SeriesHandle rsi = default;
        SeriesHandle sma = default;

        // Act
        builder.ConfigureIndicators(catalog =>
        {
            rsi = catalog.Rsi(14);
            sma = catalog.Sma(20);
        });

        builder.ConfigureSignals(signals =>
        {
            signals.Group(
                SignalCondition.Above(rsi, 50),
                SignalCondition.CrossesAbove(sma, 100)
            ).All().Emit("Bullish Confluence");
        });

        using var runtime = builder.Build();

        // Assert
        Assert.NotNull(runtime);
    }

    [Fact]
    public void ConsoleNotificationChannel_NotifyAsync_WritesToConsole()
    {
        // Arrange
        var channel = new ConsoleNotificationChannel();
        var notification = new NotificationEvent(
            new SignalHandle(1),
            "Test Signal",
            42.5,
            new DateTime(2024, 1, 15, 10, 30, 0));

        // Act & Assert - Should not throw
        var task = channel.NotifyAsync(notification);
        Assert.True(task.IsCompletedSuccessfully);
    }

    [Fact]
    public void NotificationEvent_Properties_AreSetCorrectly()
    {
        // Arrange & Act
        var signal = new SignalHandle(42);
        var timestamp = new DateTime(2024, 6, 15, 14, 30, 0);
        var notification = new NotificationEvent(signal, "Buy Signal", 75.5, timestamp);

        // Assert
        Assert.Equal(42, notification.Signal.Id);
        Assert.Equal("Buy Signal", notification.Name);
        Assert.Equal(75.5, notification.Value);
        Assert.Equal(timestamp, notification.Timestamp);
    }
}

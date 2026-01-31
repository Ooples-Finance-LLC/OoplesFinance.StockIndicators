using FluentAssertions;
using Moq;
using OoplesFinance.TradingApp.Maui.Models;
using OoplesFinance.TradingApp.Maui.Services;
using OoplesFinance.TradingApp.Maui.ViewModels;
using Xunit;

namespace TradingApp.Tests.ViewModels;

/// <summary>
/// Unit tests for ViewModels using mocked services.
/// </summary>
public class PositionsViewModelTests
{
    private readonly Mock<IPortfolioService> _mockPortfolioService;
    private readonly Mock<IOrderService> _mockOrderService;
    private readonly PositionsViewModel _viewModel;

    public PositionsViewModelTests()
    {
        _mockPortfolioService = new Mock<IPortfolioService>();
        _mockOrderService = new Mock<IOrderService>();
        _viewModel = new PositionsViewModel(_mockPortfolioService.Object, _mockOrderService.Object);
    }

    [Fact]
    public async Task LoadPositionsAsync_WithPositions_PopulatesCollection()
    {
        // Arrange
        var positions = new List<Position>
        {
            new() { Symbol = "AAPL", CompanyName = "Apple Inc.", Quantity = 100, AveragePrice = 150m, CurrentPrice = 175m, MarketValue = 17500m, UnrealizedPnL = 2500m, DayPnL = 150m },
            new() { Symbol = "MSFT", CompanyName = "Microsoft", Quantity = 50, AveragePrice = 300m, CurrentPrice = 380m, MarketValue = 19000m, UnrealizedPnL = 4000m, DayPnL = 200m }
        };

        _mockPortfolioService.Setup(s => s.GetPositionsAsync()).ReturnsAsync(positions);

        // Act
        await _viewModel.LoadPositionsAsync();

        // Assert
        _viewModel.Positions.Should().HaveCount(2);
        _viewModel.TotalMarketValue.Should().Be(36500m);
        _viewModel.TotalUnrealizedPnL.Should().Be(6500m);
        _viewModel.TotalDayPnL.Should().Be(350m);
    }

    [Fact]
    public async Task LoadPositionsAsync_WithNoPositions_HasEmptyCollection()
    {
        // Arrange
        _mockPortfolioService.Setup(s => s.GetPositionsAsync()).ReturnsAsync(new List<Position>());

        // Act
        await _viewModel.LoadPositionsAsync();

        // Assert
        _viewModel.Positions.Should().BeEmpty();
        _viewModel.TotalMarketValue.Should().Be(0);
    }

    [Fact]
    public async Task LoadPositionsAsync_SetsCorrectColors()
    {
        // Arrange
        var positions = new List<Position>
        {
            new() { Symbol = "AAPL", Quantity = 100, AveragePrice = 150m, CurrentPrice = 175m, UnrealizedPnL = 2500m, DayPnL = 150m },
            new() { Symbol = "TSLA", Quantity = 50, AveragePrice = 300m, CurrentPrice = 250m, UnrealizedPnL = -2500m, DayPnL = -100m }
        };

        _mockPortfolioService.Setup(s => s.GetPositionsAsync()).ReturnsAsync(positions);

        // Act
        await _viewModel.LoadPositionsAsync();

        // Assert - AAPL has positive PnL
        var aaplPosition = _viewModel.Positions.First(p => p.Symbol == "AAPL");
        aaplPosition.PnLColor.Should().Be(Microsoft.Maui.Graphics.Color.FromArgb("#4EC9B0"));

        // Assert - TSLA has negative PnL
        var tslaPosition = _viewModel.Positions.First(p => p.Symbol == "TSLA");
        tslaPosition.PnLColor.Should().Be(Microsoft.Maui.Graphics.Color.FromArgb("#F14C4C"));
    }

    [Fact]
    public void IsRefreshing_PropertyChangedNotification_Works()
    {
        // Arrange
        var propertyChanged = false;
        _viewModel.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName == nameof(_viewModel.IsRefreshing))
                propertyChanged = true;
        };

        // Act
        _viewModel.IsRefreshing = true;

        // Assert
        propertyChanged.Should().BeTrue();
    }
}

/// <summary>
/// Tests for OrderEntryViewModel.
/// </summary>
public class OrderEntryViewModelTests
{
    private readonly Mock<IMarketDataService> _mockMarketDataService;
    private readonly Mock<IOrderService> _mockOrderService;
    private readonly Mock<IPortfolioService> _mockPortfolioService;
    private readonly OrderEntryViewModel _viewModel;

    public OrderEntryViewModelTests()
    {
        _mockMarketDataService = new Mock<IMarketDataService>();
        _mockOrderService = new Mock<IOrderService>();
        _mockPortfolioService = new Mock<IPortfolioService>();

        _viewModel = new OrderEntryViewModel(
            _mockMarketDataService.Object,
            _mockOrderService.Object,
            _mockPortfolioService.Object);
    }

    [Fact]
    public async Task SearchSymbol_WithValidSymbol_PopulatesQuote()
    {
        // Arrange
        var quote = new Quote
        {
            Symbol = "AAPL",
            CompanyName = "Apple Inc.",
            LastPrice = 175m,
            Change = 2.50m,
            ChangePercent = 0.0145m
        };

        _mockMarketDataService.Setup(s => s.GetQuoteAsync("AAPL")).ReturnsAsync(quote);
        _viewModel.Symbol = "AAPL";

        // Act
        await _viewModel.SearchSymbolCommand.ExecuteAsync(null);

        // Assert
        _viewModel.CurrentPrice.Should().Be(175m);
        _viewModel.CompanyName.Should().Be("Apple Inc.");
    }

    [Fact]
    public void EstimatedTotal_CalculatesCorrectly()
    {
        // Arrange
        _viewModel.Quantity = 100;
        _viewModel.CurrentPrice = 175m;

        // Assert
        _viewModel.EstimatedTotal.Should().Be(17500m);
    }

    [Fact]
    public void CanSubmit_WhenValid_ReturnsTrue()
    {
        // Arrange
        _viewModel.Symbol = "AAPL";
        _viewModel.Quantity = 10;
        _viewModel.CurrentPrice = 175m;
        _viewModel.IsLoading = false;

        // Assert
        _viewModel.CanSubmit.Should().BeTrue();
    }

    [Fact]
    public void CanSubmit_WhenNoSymbol_ReturnsFalse()
    {
        // Arrange
        _viewModel.Symbol = string.Empty;
        _viewModel.Quantity = 10;

        // Assert
        _viewModel.CanSubmit.Should().BeFalse();
    }

    [Fact]
    public void CanSubmit_WhenZeroQuantity_ReturnsFalse()
    {
        // Arrange
        _viewModel.Symbol = "AAPL";
        _viewModel.Quantity = 0;

        // Assert
        _viewModel.CanSubmit.Should().BeFalse();
    }
}

/// <summary>
/// Tests for OrderConfirmationViewModel.
/// </summary>
public class OrderConfirmationViewModelTests
{
    private readonly Mock<IOrderService> _mockOrderService;
    private readonly Mock<IPortfolioService> _mockPortfolioService;
    private readonly OrderConfirmationViewModel _viewModel;

    public OrderConfirmationViewModelTests()
    {
        _mockOrderService = new Mock<IOrderService>();
        _mockPortfolioService = new Mock<IPortfolioService>();
        _viewModel = new OrderConfirmationViewModel(_mockOrderService.Object, _mockPortfolioService.Object);
    }

    [Theory]
    [InlineData(100, 150, 15000)]
    [InlineData(50, 300, 15000)]
    [InlineData(1, 1000, 1000)]
    public void EstimatedTotal_CalculatesCorrectly(decimal quantity, decimal price, decimal expected)
    {
        // Arrange
        _viewModel.Quantity = quantity;
        _viewModel.EstimatedPrice = price;

        // Assert
        _viewModel.EstimatedTotal.Should().Be(expected);
    }

    [Fact]
    public void IsLargeOrder_WhenOver10K_ReturnsTrue()
    {
        // Arrange
        _viewModel.Quantity = 100;
        _viewModel.EstimatedPrice = 150m; // 15000 > 10000

        // Assert
        _viewModel.IsLargeOrder.Should().BeTrue();
    }

    [Fact]
    public void IsLargeOrder_WhenUnder10K_ReturnsFalse()
    {
        // Arrange
        _viewModel.Quantity = 10;
        _viewModel.EstimatedPrice = 100m; // 1000 < 10000

        // Assert
        _viewModel.IsLargeOrder.Should().BeFalse();
    }

    [Theory]
    [InlineData("buy", "#10B981")]
    [InlineData("sell", "#EF4444")]
    public void SideColor_ReturnsCorrectColor(string side, string expectedColor)
    {
        // Arrange
        _viewModel.Side = side;

        // Assert
        _viewModel.SideColor.Should().Be(Microsoft.Maui.Graphics.Color.FromArgb(expectedColor));
    }

    [Fact]
    public void CanConfirm_WhenNotLargeOrder_ReturnsTrue()
    {
        // Arrange
        _viewModel.Quantity = 10;
        _viewModel.EstimatedPrice = 100m; // Small order

        // Assert
        _viewModel.CanConfirm.Should().BeTrue();
    }

    [Fact]
    public void CanConfirm_WhenLargeOrderWithoutExplicitConfirm_ReturnsFalse()
    {
        // Arrange
        _viewModel.Quantity = 100;
        _viewModel.EstimatedPrice = 200m; // Large order
        _viewModel.ExplicitlyConfirmed = false;

        // Assert
        _viewModel.CanConfirm.Should().BeFalse();
    }

    [Fact]
    public void CanConfirm_WhenLargeOrderWithExplicitConfirm_ReturnsTrue()
    {
        // Arrange
        _viewModel.Quantity = 100;
        _viewModel.EstimatedPrice = 200m; // Large order
        _viewModel.ExplicitlyConfirmed = true;

        // Assert
        _viewModel.CanConfirm.Should().BeTrue();
    }

    [Fact]
    public void NewPosition_CalculatesCorrectly_ForBuy()
    {
        // Arrange
        _viewModel.CurrentPosition = 50;
        _viewModel.Quantity = 25;
        _viewModel.Side = "buy";

        // Assert
        _viewModel.NewPosition.Should().Be(75);
    }

    [Fact]
    public void NewPosition_CalculatesCorrectly_ForSell()
    {
        // Arrange
        _viewModel.CurrentPosition = 50;
        _viewModel.Quantity = 25;
        _viewModel.Side = "sell";

        // Assert
        _viewModel.NewPosition.Should().Be(25);
    }
}

/// <summary>
/// Tests for AlertsViewModel.
/// </summary>
public class AlertsViewModelTests
{
    private readonly Mock<IAlertService> _mockAlertService;
    private readonly AlertsViewModel _viewModel;

    public AlertsViewModelTests()
    {
        _mockAlertService = new Mock<IAlertService>();
        _viewModel = new AlertsViewModel(_mockAlertService.Object);
    }

    [Fact]
    public async Task LoadAlertsAsync_PopulatesCollection()
    {
        // Arrange
        var alerts = new List<Alert>
        {
            new() { Id = "1", Symbol = "AAPL", Condition = "Price above", TargetPrice = 180m, IsActive = true },
            new() { Id = "2", Symbol = "MSFT", Condition = "Price below", TargetPrice = 350m, IsActive = true }
        };

        _mockAlertService.Setup(s => s.GetAllAlertsAsync()).ReturnsAsync(alerts);

        // Act
        await _viewModel.LoadAlertsAsync();

        // Assert
        _viewModel.Alerts.Should().HaveCount(2);
    }

    [Fact]
    public async Task CreateAlertAsync_AddsNewAlert()
    {
        // Arrange
        _mockAlertService
            .Setup(s => s.CreateAlertAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<decimal>()))
            .ReturnsAsync("new-alert-id");

        _mockAlertService
            .Setup(s => s.GetAllAlertsAsync())
            .ReturnsAsync(new List<Alert>());

        _viewModel.NewAlertSymbol = "AAPL";
        _viewModel.NewAlertCondition = "Price above";
        _viewModel.NewAlertPrice = 200m;

        // Act
        await _viewModel.CreateAlertCommand.ExecuteAsync(null);

        // Assert
        _mockAlertService.Verify(s => s.CreateAlertAsync("AAPL", "Price above", 200m), Times.Once);
    }

    [Fact]
    public async Task DeleteAlertAsync_RemovesAlert()
    {
        // Arrange
        var alertId = "test-alert-id";
        _mockAlertService.Setup(s => s.DeleteAlertAsync(alertId)).Returns(Task.CompletedTask);
        _mockAlertService.Setup(s => s.GetAllAlertsAsync()).ReturnsAsync(new List<Alert>());

        var alertVm = new AlertItemViewModel { Id = alertId, Symbol = "AAPL" };

        // Act
        await _viewModel.DeleteAlertCommand.ExecuteAsync(alertVm);

        // Assert
        _mockAlertService.Verify(s => s.DeleteAlertAsync(alertId), Times.Once);
    }
}

/// <summary>
/// Tests for DashboardViewModel.
/// </summary>
public class DashboardViewModelTests
{
    private readonly Mock<IPortfolioService> _mockPortfolioService;
    private readonly Mock<IMarketDataService> _mockMarketDataService;
    private readonly Mock<IOrderService> _mockOrderService;
    private readonly Mock<ISettingsService> _mockSettingsService;
    private readonly DashboardViewModel _viewModel;

    public DashboardViewModelTests()
    {
        _mockPortfolioService = new Mock<IPortfolioService>();
        _mockMarketDataService = new Mock<IMarketDataService>();
        _mockOrderService = new Mock<IOrderService>();
        _mockSettingsService = new Mock<ISettingsService>();

        _viewModel = new DashboardViewModel(
            _mockPortfolioService.Object,
            _mockMarketDataService.Object,
            _mockOrderService.Object,
            _mockSettingsService.Object);
    }

    [Fact]
    public async Task LoadDashboardAsync_PopulatesAccountInfo()
    {
        // Arrange
        var accountInfo = new AccountInfo
        {
            PortfolioValue = 125000m,
            Cash = 25000m,
            BuyingPower = 50000m,
            TodayPnL = 1250m,
            TotalPnL = 25000m
        };

        _mockPortfolioService.Setup(s => s.GetAccountAsync()).ReturnsAsync(accountInfo);
        _mockPortfolioService.Setup(s => s.GetPositionsAsync()).ReturnsAsync(new List<Position>());
        _mockOrderService.Setup(s => s.GetOpenOrdersAsync()).ReturnsAsync(new List<Order>());
        _mockMarketDataService.Setup(s => s.GetMarketStatusAsync()).ReturnsAsync(new MarketStatus { IsOpen = true });
        _mockSettingsService.Setup(s => s.GetWatchlistAsync()).ReturnsAsync(new List<string>());

        // Act
        await _viewModel.LoadDashboardAsync();

        // Assert
        _viewModel.PortfolioValue.Should().Be(125000m);
        _viewModel.Cash.Should().Be(25000m);
        _viewModel.BuyingPower.Should().Be(50000m);
        _viewModel.TodayPnL.Should().Be(1250m);
    }

    [Fact]
    public async Task LoadDashboardAsync_SetsCorrectPnLColors()
    {
        // Arrange
        var accountInfo = new AccountInfo
        {
            PortfolioValue = 100000m,
            TodayPnL = -500m, // Negative
            TotalPnL = 5000m  // Positive
        };

        _mockPortfolioService.Setup(s => s.GetAccountAsync()).ReturnsAsync(accountInfo);
        _mockPortfolioService.Setup(s => s.GetPositionsAsync()).ReturnsAsync(new List<Position>());
        _mockOrderService.Setup(s => s.GetOpenOrdersAsync()).ReturnsAsync(new List<Order>());
        _mockMarketDataService.Setup(s => s.GetMarketStatusAsync()).ReturnsAsync(new MarketStatus { IsOpen = true });
        _mockSettingsService.Setup(s => s.GetWatchlistAsync()).ReturnsAsync(new List<string>());

        // Act
        await _viewModel.LoadDashboardAsync();

        // Assert
        _viewModel.TodayPnLColor.Should().Be(Microsoft.Maui.Graphics.Color.FromArgb("#EF4444")); // Red for negative
        _viewModel.TotalPnLColor.Should().Be(Microsoft.Maui.Graphics.Color.FromArgb("#10B981")); // Green for positive
    }
}

using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using OoplesFinance.TradingApp.Maui.Services;
using OoplesFinance.TradingApp.Maui.Models;
using Cloud = OoplesFinance.StockIndicators.Builder.Cloud;

namespace OoplesFinance.TradingApp.Maui.ViewModels;

#region Positions ViewModel

public partial class PositionsViewModel : ObservableObject
{
    private readonly IPortfolioService _portfolioService;
    private readonly IOrderService _orderService;

    [ObservableProperty] private bool _isRefreshing;
    [ObservableProperty] private decimal _totalMarketValue;
    [ObservableProperty] private decimal _totalDayPnL;
    [ObservableProperty] private decimal _totalUnrealizedPnL;
    [ObservableProperty] private Color _dayPnLColor = Colors.White;
    [ObservableProperty] private Color _totalPnLColor = Colors.White;
    [ObservableProperty] private ObservableCollection<PositionViewModel> _positions = new();
    [ObservableProperty] private PositionViewModel? _selectedPosition;

    public PositionsViewModel(IPortfolioService portfolioService, IOrderService orderService)
    {
        _portfolioService = portfolioService;
        _orderService = orderService;
    }

    public async Task LoadPositionsAsync() => await RefreshAsync();

    [RelayCommand]
    private async Task RefreshAsync()
    {
        try
        {
            IsRefreshing = true;
            var positions = await _portfolioService.GetPositionsAsync();

            Positions.Clear();
            TotalMarketValue = 0;
            TotalDayPnL = 0;
            TotalUnrealizedPnL = 0;

            foreach (var pos in positions)
            {
                var vm = new PositionViewModel
                {
                    Symbol = pos.Symbol,
                    CompanyName = pos.CompanyName,
                    Quantity = pos.Quantity,
                    AveragePrice = pos.AveragePrice,
                    CurrentPrice = pos.CurrentPrice,
                    MarketValue = pos.MarketValue,
                    UnrealizedPnL = pos.UnrealizedPnL,
                    UnrealizedPnLPercent = pos.AveragePrice > 0 ? (pos.CurrentPrice - pos.AveragePrice) / pos.AveragePrice : 0,
                    DayPnL = pos.DayPnL,
                    Side = pos.Quantity >= 0 ? "LONG" : "SHORT",
                    SideColor = pos.Quantity >= 0 ? Color.FromArgb("#4EC9B0") : Color.FromArgb("#F14C4C"),
                    PnLColor = pos.UnrealizedPnL >= 0 ? Color.FromArgb("#4EC9B0") : Color.FromArgb("#F14C4C")
                };
                Positions.Add(vm);

                TotalMarketValue += pos.MarketValue;
                TotalDayPnL += pos.DayPnL;
                TotalUnrealizedPnL += pos.UnrealizedPnL;
            }

            DayPnLColor = TotalDayPnL >= 0 ? Color.FromArgb("#4EC9B0") : Color.FromArgb("#F14C4C");
            TotalPnLColor = TotalUnrealizedPnL >= 0 ? Color.FromArgb("#4EC9B0") : Color.FromArgb("#F14C4C");
        }
        finally
        {
            IsRefreshing = false;
        }
    }

    [RelayCommand]
    private async Task PositionTappedAsync()
    {
        if (SelectedPosition is null) return;
        await Shell.Current.GoToAsync($"positionDetail?symbol={SelectedPosition.Symbol}");
        SelectedPosition = null;
    }

    [RelayCommand]
    private async Task ClosePositionAsync(PositionViewModel position)
    {
        var confirm = await Application.Current!.MainPage!.DisplayAlert(
            "Close Position",
            $"Are you sure you want to close your {position.Symbol} position?",
            "Yes", "No");

        if (!confirm) return;

        await _orderService.ClosePositionAsync(position.Symbol);
        await RefreshAsync();
    }

    [RelayCommand]
    private async Task GoToTradeAsync()
    {
        await Shell.Current.GoToAsync("//trade");
    }
}

public partial class PositionViewModel : ObservableObject
{
    [ObservableProperty] private string _symbol = string.Empty;
    [ObservableProperty] private string _companyName = string.Empty;
    [ObservableProperty] private decimal _quantity;
    [ObservableProperty] private decimal _averagePrice;
    [ObservableProperty] private decimal _currentPrice;
    [ObservableProperty] private decimal _marketValue;
    [ObservableProperty] private decimal _unrealizedPnL;
    [ObservableProperty] private decimal _unrealizedPnLPercent;
    [ObservableProperty] private decimal _dayPnL;
    [ObservableProperty] private string _side = "LONG";
    [ObservableProperty] private Color _sideColor = Colors.Green;
    [ObservableProperty] private Color _pnLColor = Colors.White;
}

#endregion

#region Order Entry ViewModel

public partial class OrderEntryViewModel : ObservableObject
{
    private readonly IOrderService _orderService;
    private readonly IMarketDataService _marketDataService;
    private readonly IPortfolioService _portfolioService;

    [ObservableProperty] private string _searchText = string.Empty;
    [ObservableProperty] private string _selectedSymbol = string.Empty;
    [ObservableProperty] private string _companyName = string.Empty;
    [ObservableProperty] private decimal _currentPrice;
    [ObservableProperty] private decimal _priceChange;
    [ObservableProperty] private decimal _priceChangePercent;
    [ObservableProperty] private Color _priceChangeColor = Colors.White;
    [ObservableProperty] private bool _hasSelectedSymbol;

    [ObservableProperty] private bool _isBuy = true;
    [ObservableProperty] private Color _buyButtonColor = Color.FromArgb("#4EC9B0");
    [ObservableProperty] private Color _sellButtonColor = Color.FromArgb("#3E3E42");

    [ObservableProperty] private string _selectedOrderType = "Market";
    [ObservableProperty] private ObservableCollection<string> _orderTypes = new() { "Market", "Limit", "Stop", "Stop Limit" };
    [ObservableProperty] private bool _showLimitPrice;
    [ObservableProperty] private bool _showStopPrice;
    [ObservableProperty] private string _limitPrice = string.Empty;
    [ObservableProperty] private string _stopPrice = string.Empty;

    [ObservableProperty] private string _quantity = string.Empty;
    [ObservableProperty] private decimal _availableShares;

    [ObservableProperty] private string _selectedTimeInForce = "Day";
    [ObservableProperty] private ObservableCollection<string> _timeInForceOptions = new() { "Day", "GTC", "IOC", "FOK" };

    [ObservableProperty] private decimal _estimatedCost;
    [ObservableProperty] private decimal _commission;
    [ObservableProperty] private decimal _totalCost;
    [ObservableProperty] private decimal _buyingPowerAfter;
    [ObservableProperty] private decimal _buyingPower;

    [ObservableProperty] private string _submitButtonText = "Review Buy Order";
    [ObservableProperty] private Color _submitButtonColor = Color.FromArgb("#4EC9B0");
    [ObservableProperty] private bool _canSubmit;

    public OrderEntryViewModel(IOrderService orderService, IMarketDataService marketDataService, IPortfolioService portfolioService)
    {
        _orderService = orderService;
        _marketDataService = marketDataService;
        _portfolioService = portfolioService;
    }

    public async Task InitializeAsync()
    {
        var account = await _portfolioService.GetAccountAsync();
        BuyingPower = account.BuyingPower;
        UpdateCalculations();
    }

    partial void OnSelectedOrderTypeChanged(string value)
    {
        ShowLimitPrice = value is "Limit" or "Stop Limit";
        ShowStopPrice = value is "Stop" or "Stop Limit";
    }

    partial void OnIsBuyChanged(bool value)
    {
        if (value)
        {
            BuyButtonColor = Color.FromArgb("#4EC9B0");
            SellButtonColor = Color.FromArgb("#3E3E42");
            SubmitButtonText = "Review Buy Order";
            SubmitButtonColor = Color.FromArgb("#4EC9B0");
        }
        else
        {
            BuyButtonColor = Color.FromArgb("#3E3E42");
            SellButtonColor = Color.FromArgb("#F14C4C");
            SubmitButtonText = "Review Sell Order";
            SubmitButtonColor = Color.FromArgb("#F14C4C");
        }
        UpdateCalculations();
    }

    partial void OnQuantityChanged(string value) => UpdateCalculations();
    partial void OnLimitPriceChanged(string value) => UpdateCalculations();

    private void UpdateCalculations()
    {
        if (!decimal.TryParse(Quantity, out var qty) || qty <= 0)
        {
            EstimatedCost = 0;
            Commission = 0;
            TotalCost = 0;
            BuyingPowerAfter = BuyingPower;
            CanSubmit = false;
            return;
        }

        var price = SelectedOrderType == "Limit" && decimal.TryParse(LimitPrice, out var limitPrice)
            ? limitPrice
            : CurrentPrice;

        EstimatedCost = qty * price;
        Commission = 0; // Commission-free trading
        TotalCost = EstimatedCost + Commission;
        BuyingPowerAfter = IsBuy ? BuyingPower - TotalCost : BuyingPower + TotalCost;
        CanSubmit = HasSelectedSymbol && qty > 0 && (IsBuy ? TotalCost <= BuyingPower : true);
    }

    [RelayCommand]
    private async Task SearchAsync()
    {
        if (string.IsNullOrWhiteSpace(SearchText)) return;

        var quote = await _marketDataService.GetQuoteAsync(SearchText.ToUpperInvariant());
        if (quote is not null)
        {
            SelectedSymbol = quote.Symbol;
            CompanyName = quote.CompanyName;
            CurrentPrice = quote.LastPrice;
            PriceChange = quote.Change;
            PriceChangePercent = quote.ChangePercent;
            PriceChangeColor = PriceChange >= 0 ? Color.FromArgb("#4EC9B0") : Color.FromArgb("#F14C4C");
            HasSelectedSymbol = true;

            // Get available shares for selling
            var positions = await _portfolioService.GetPositionsAsync();
            var position = positions.FirstOrDefault(p => p.Symbol == SelectedSymbol);
            AvailableShares = position?.Quantity ?? 0;

            UpdateCalculations();
        }
    }

    [RelayCommand]
    private void SetBuy() => IsBuy = true;

    [RelayCommand]
    private void SetSell() => IsBuy = false;

    [RelayCommand]
    private void SetQuantityPercent(string percentStr)
    {
        if (!decimal.TryParse(percentStr, out var percent)) return;

        if (IsBuy)
        {
            var maxShares = CurrentPrice > 0 ? Math.Floor(BuyingPower * percent / CurrentPrice) : 0;
            Quantity = maxShares.ToString("N0");
        }
        else
        {
            var shares = Math.Floor(AvailableShares * percent);
            Quantity = shares.ToString("N0");
        }
    }

    [RelayCommand]
    private async Task SubmitOrderAsync()
    {
        if (!CanSubmit) return;

        var order = new OrderRequest
        {
            Symbol = SelectedSymbol,
            Side = IsBuy ? "buy" : "sell",
            Quantity = decimal.Parse(Quantity),
            OrderType = SelectedOrderType.ToLowerInvariant().Replace(" ", "_"),
            TimeInForce = SelectedTimeInForce.ToLowerInvariant(),
            LimitPrice = ShowLimitPrice && decimal.TryParse(LimitPrice, out var lp) ? lp : null,
            StopPrice = ShowStopPrice && decimal.TryParse(StopPrice, out var sp) ? sp : null
        };

        var result = await _orderService.SubmitOrderAsync(order);
        if (result.Success)
        {
            await Application.Current!.MainPage!.DisplayAlert("Order Submitted", $"Order {result.OrderId} submitted successfully.", "OK");
            // Reset form
            Quantity = string.Empty;
            LimitPrice = string.Empty;
            StopPrice = string.Empty;
        }
        else
        {
            await Application.Current!.MainPage!.DisplayAlert("Order Failed", result.ErrorMessage, "OK");
        }
    }
}

#endregion

#region Alerts ViewModel

public partial class AlertsViewModel : ObservableObject
{
    private readonly IAlertService _alertService;

    [ObservableProperty] private bool _isRefreshing;
    [ObservableProperty] private ObservableCollection<AlertItemViewModel> _alerts = new();

    public AlertsViewModel(IAlertService alertService)
    {
        _alertService = alertService;
    }

    public async Task LoadAlertsAsync() => await RefreshAsync();

    [RelayCommand]
    private async Task RefreshAsync()
    {
        try
        {
            IsRefreshing = true;
            var alerts = await _alertService.GetAllAlertsAsync();
            Alerts.Clear();
            foreach (var alert in alerts)
            {
                Alerts.Add(new AlertItemViewModel
                {
                    Id = alert.Id,
                    Symbol = alert.Symbol,
                    Condition = alert.Condition,
                    TargetPrice = alert.TargetPrice,
                    IsActive = alert.IsActive,
                    CreatedAt = alert.CreatedAt,
                    StatusIcon = alert.IsActive ? "[ON]" : "[OFF]"
                });
            }
        }
        finally
        {
            IsRefreshing = false;
        }
    }

    [RelayCommand]
    private async Task DeleteAlertAsync(AlertItemViewModel alert)
    {
        await _alertService.DeleteAlertAsync(alert.Id);
        Alerts.Remove(alert);
    }

    [RelayCommand]
    private async Task AddAlertAsync()
    {
        // Navigate to add alert page or show dialog
        await Shell.Current.GoToAsync("addAlert");
    }
}

public partial class AlertItemViewModel : ObservableObject
{
    [ObservableProperty] private string _id = string.Empty;
    [ObservableProperty] private string _symbol = string.Empty;
    [ObservableProperty] private string _condition = string.Empty;
    [ObservableProperty] private decimal _targetPrice;
    [ObservableProperty] private bool _isActive;
    [ObservableProperty] private DateTime _createdAt;
    [ObservableProperty] private string _statusIcon = "[ON]";
}

#endregion

#region Watchlist ViewModel

public partial class WatchlistViewModel : ObservableObject
{
    private readonly IMarketDataService _marketDataService;
    private readonly ISettingsService _settingsService;

    [ObservableProperty] private bool _isRefreshing;
    [ObservableProperty] private ObservableCollection<WatchlistItemViewModel> _items = new();

    public WatchlistViewModel(IMarketDataService marketDataService, ISettingsService settingsService)
    {
        _marketDataService = marketDataService;
        _settingsService = settingsService;
    }

    public async Task LoadWatchlistAsync() => await RefreshAsync();

    public async Task LoadDataAsync() => await RefreshAsync();

    [RelayCommand]
    private async Task RefreshAsync()
    {
        try
        {
            IsRefreshing = true;
            var symbols = await _settingsService.GetWatchlistAsync();
            Items.Clear();

            foreach (var symbol in symbols)
            {
                var quote = await _marketDataService.GetQuoteAsync(symbol);
                if (quote is not null)
                {
                    Items.Add(new WatchlistItemViewModel
                    {
                        Symbol = quote.Symbol,
                        CompanyName = quote.CompanyName,
                        LastPrice = quote.LastPrice,
                        Change = quote.Change,
                        ChangePercent = quote.ChangePercent,
                        ChangeColor = quote.Change >= 0 ? Color.FromArgb("#4EC9B0") : Color.FromArgb("#F14C4C")
                    });
                }
            }
        }
        finally
        {
            IsRefreshing = false;
        }
    }

    [RelayCommand]
    private async Task RemoveFromWatchlistAsync(WatchlistItemViewModel item)
    {
        await _settingsService.RemoveFromWatchlistAsync(item.Symbol);
        Items.Remove(item);
    }

    [RelayCommand]
    private async Task TapItemAsync(WatchlistItemViewModel item)
    {
        await Shell.Current.GoToAsync($"//trade?symbol={item.Symbol}");
    }
}

public partial class WatchlistItemViewModel : ObservableObject
{
    [ObservableProperty] private string _symbol = string.Empty;
    [ObservableProperty] private string _companyName = string.Empty;
    [ObservableProperty] private decimal _lastPrice;
    [ObservableProperty] private decimal _change;
    [ObservableProperty] private decimal _changePercent;
    [ObservableProperty] private Color _changeColor = Colors.White;
}

#endregion

#region Order History ViewModel

public partial class OrderHistoryViewModel : ObservableObject
{
    private readonly IOrderService _orderService;

    [ObservableProperty] private bool _isRefreshing;
    [ObservableProperty] private string _selectedFilter = "all";
    [ObservableProperty] private ObservableCollection<OrderItemViewModel> _filteredOrders = new();

    private List<OrderItemViewModel> _allOrders = new();

    // Filter button styles
    public Style AllFilterStyle => SelectedFilter == "all" ? GetActiveStyle() : GetInactiveStyle();
    public Style OpenFilterStyle => SelectedFilter == "open" ? GetActiveStyle() : GetInactiveStyle();
    public Style FilledFilterStyle => SelectedFilter == "filled" ? GetActiveStyle() : GetInactiveStyle();
    public Style CancelledFilterStyle => SelectedFilter == "cancelled" ? GetActiveStyle() : GetInactiveStyle();

    private static Style GetActiveStyle() => Application.Current?.Resources["FilterButtonActive"] as Style ?? new Style(typeof(Button));
    private static Style GetInactiveStyle() => Application.Current?.Resources["FilterButton"] as Style ?? new Style(typeof(Button));

    public OrderHistoryViewModel(IOrderService orderService) => _orderService = orderService;

    public async Task LoadOrdersAsync() => await RefreshAsync();

    [RelayCommand]
    private async Task RefreshAsync()
    {
        try
        {
            IsRefreshing = true;
            var orders = await _orderService.GetRecentOrdersAsync(50);
            _allOrders.Clear();
            foreach (var order in orders)
            {
                _allOrders.Add(new OrderItemViewModel
                {
                    OrderId = order.OrderId,
                    Symbol = order.Symbol,
                    Side = order.Side.ToUpperInvariant(),
                    Quantity = order.Quantity,
                    Price = order.Price,
                    FilledPrice = order.FilledPrice,
                    Status = order.Status,
                    CreatedAt = order.CreatedAt,
                    SideColor = order.Side.Equals("buy", StringComparison.OrdinalIgnoreCase)
                        ? Color.FromArgb("#10B981") : Color.FromArgb("#EF4444"),
                    StatusBackgroundColor = GetStatusBackgroundColor(order.Status),
                    StatusTextColor = Colors.White,
                    CanCancel = order.Status is "pending" or "new" or "open",
                    IsFilled = order.Status == "filled"
                });
            }
            ApplyFilter();
        }
        finally { IsRefreshing = false; }
    }

    [RelayCommand]
    private void Filter(string filter)
    {
        SelectedFilter = filter;
        ApplyFilter();
        OnPropertyChanged(nameof(AllFilterStyle));
        OnPropertyChanged(nameof(OpenFilterStyle));
        OnPropertyChanged(nameof(FilledFilterStyle));
        OnPropertyChanged(nameof(CancelledFilterStyle));
    }

    private void ApplyFilter()
    {
        FilteredOrders.Clear();
        var filtered = SelectedFilter switch
        {
            "open" => _allOrders.Where(o => o.Status is "pending" or "new" or "open"),
            "filled" => _allOrders.Where(o => o.Status == "filled"),
            "cancelled" => _allOrders.Where(o => o.Status is "cancelled" or "canceled"),
            _ => _allOrders
        };
        foreach (var order in filtered)
            FilteredOrders.Add(order);
    }

    [RelayCommand]
    private async Task CancelOrderAsync(OrderItemViewModel order)
    {
        var confirm = await Application.Current!.MainPage!.DisplayAlert(
            "Cancel Order", $"Cancel order for {order.Symbol}?", "Yes", "No");
        if (!confirm) return;

        await _orderService.CancelOrderAsync(order.OrderId);
        await RefreshAsync();
    }

    [RelayCommand]
    private async Task OrderTappedAsync()
    {
        // Navigate to order detail if needed
        await Task.CompletedTask;
    }

    private static Color GetStatusBackgroundColor(string status) => status.ToLowerInvariant() switch
    {
        "filled" => Color.FromArgb("#059669"),
        "cancelled" or "canceled" => Color.FromArgb("#64748B"),
        "rejected" => Color.FromArgb("#DC2626"),
        "pending" or "new" or "open" => Color.FromArgb("#2563EB"),
        _ => Color.FromArgb("#64748B")
    };
}

public partial class OrderItemViewModel : ObservableObject
{
    [ObservableProperty] private string _orderId = string.Empty;
    [ObservableProperty] private string _symbol = string.Empty;
    [ObservableProperty] private string _side = string.Empty;
    [ObservableProperty] private decimal _quantity;
    [ObservableProperty] private decimal _price;
    [ObservableProperty] private decimal _filledPrice;
    [ObservableProperty] private string _status = string.Empty;
    [ObservableProperty] private DateTime _createdAt;
    [ObservableProperty] private Color _sideColor = Colors.Green;
    [ObservableProperty] private Color _statusBackgroundColor = Colors.Gray;
    [ObservableProperty] private Color _statusTextColor = Colors.White;
    [ObservableProperty] private bool _canCancel;
    [ObservableProperty] private bool _isFilled;
}

public partial class OrderSummaryViewModel : ObservableObject
{
    [ObservableProperty] private string _orderId = string.Empty;
    [ObservableProperty] private string _symbol = string.Empty;
    [ObservableProperty] private string _description = string.Empty;
    [ObservableProperty] private string _status = string.Empty;
    [ObservableProperty] private DateTime _time;
    [ObservableProperty] private Color _statusColor = Colors.Gray;
}

#endregion

#region Chart ViewModel

public partial class ChartViewModel : ObservableObject
{
    private readonly IMarketDataService _marketDataService;

    [ObservableProperty] private string _symbol = string.Empty;
    [ObservableProperty] private string _companyName = string.Empty;
    [ObservableProperty] private decimal _lastPrice;
    [ObservableProperty] private decimal _change;
    [ObservableProperty] private decimal _changePercent;
    [ObservableProperty] private Color _changeColor = Colors.White;

    [ObservableProperty] private string _selectedTimeframe = "1D";
    [ObservableProperty] private bool _isLoading;
    public bool IsNotLoading => !IsLoading;

    [ObservableProperty] private decimal _open;
    [ObservableProperty] private decimal _high;
    [ObservableProperty] private decimal _low;
    [ObservableProperty] private long _volume;
    [ObservableProperty] private decimal _bid;
    [ObservableProperty] private decimal _ask;

    [ObservableProperty] private IDrawable? _chartDrawable;

    // Timeframe button styles
    public Style DayButtonStyle => SelectedTimeframe == "1D" ? GetActiveStyle() : GetInactiveStyle();
    public Style WeekButtonStyle => SelectedTimeframe == "1W" ? GetActiveStyle() : GetInactiveStyle();
    public Style MonthButtonStyle => SelectedTimeframe == "1M" ? GetActiveStyle() : GetInactiveStyle();
    public Style ThreeMonthButtonStyle => SelectedTimeframe == "3M" ? GetActiveStyle() : GetInactiveStyle();
    public Style YearButtonStyle => SelectedTimeframe == "1Y" ? GetActiveStyle() : GetInactiveStyle();
    public Style AllButtonStyle => SelectedTimeframe == "ALL" ? GetActiveStyle() : GetInactiveStyle();

    private static Style GetActiveStyle() => Application.Current?.Resources["TimeframeButtonActive"] as Style ?? new Style(typeof(Button));
    private static Style GetInactiveStyle() => Application.Current?.Resources["TimeframeButton"] as Style ?? new Style(typeof(Button));

    public ChartViewModel(IMarketDataService marketDataService)
    {
        _marketDataService = marketDataService;
    }

    public async Task LoadDataAsync()
    {
        if (string.IsNullOrEmpty(Symbol)) return;

        IsLoading = true;
        try
        {
            var quote = await _marketDataService.GetQuoteAsync(Symbol);
            if (quote is not null)
            {
                CompanyName = quote.CompanyName;
                LastPrice = quote.LastPrice;
                Change = quote.Change;
                ChangePercent = quote.ChangePercent;
                ChangeColor = Change >= 0 ? Color.FromArgb("#10B981") : Color.FromArgb("#EF4444");
                Bid = quote.BidPrice;
                Ask = quote.AskPrice;
                Volume = quote.Volume;
            }

            var bars = await _marketDataService.GetHistoricalBarsAsync(Symbol, SelectedTimeframe, 30);
            if (bars.Count > 0)
            {
                Open = bars.First().Open;
                High = bars.Max(b => b.High);
                Low = bars.Min(b => b.Low);
                Volume = bars.Sum(b => b.Volume);
            }
        }
        finally
        {
            IsLoading = false;
            OnPropertyChanged(nameof(IsNotLoading));
        }
    }

    [RelayCommand]
    private async Task SetTimeframeAsync(string timeframe)
    {
        SelectedTimeframe = timeframe;
        OnPropertyChanged(nameof(DayButtonStyle));
        OnPropertyChanged(nameof(WeekButtonStyle));
        OnPropertyChanged(nameof(MonthButtonStyle));
        OnPropertyChanged(nameof(ThreeMonthButtonStyle));
        OnPropertyChanged(nameof(YearButtonStyle));
        OnPropertyChanged(nameof(AllButtonStyle));
        await LoadDataAsync();
    }

    [RelayCommand]
    private async Task BuyAsync()
    {
        await Shell.Current.GoToAsync($"//trade?symbol={Symbol}&side=buy");
    }

    [RelayCommand]
    private async Task SellAsync()
    {
        await Shell.Current.GoToAsync($"//trade?symbol={Symbol}&side=sell");
    }
}

#endregion

#region Strategy Monitor ViewModel

public partial class StrategyMonitorViewModel : ObservableObject
{
    private readonly IStrategyMonitorService _strategyService;

    [ObservableProperty] private bool _isRefreshing;
    [ObservableProperty] private ObservableCollection<StrategyItemViewModel> _strategies = new();

    public StrategyMonitorViewModel(IStrategyMonitorService strategyService) => _strategyService = strategyService;

    public async Task LoadStrategiesAsync() => await RefreshAsync();

    [RelayCommand]
    private async Task RefreshAsync()
    {
        try
        {
            IsRefreshing = true;
            var strategies = await _strategyService.GetActiveStrategiesAsync();
            Strategies.Clear();
            foreach (var strategy in strategies)
            {
                Strategies.Add(new StrategyItemViewModel
                {
                    Id = strategy.Id,
                    Name = strategy.Name,
                    Status = strategy.Status,
                    PnL = strategy.PnL,
                    TradesCount = strategy.TradesCount,
                    StartedAt = strategy.StartedAt,
                    WinRate = 0.65m, // Placeholder
                    RunningTime = FormatRunningTime(strategy.StartedAt),
                    IsRunning = strategy.Status == "Running",
                    StatusBackgroundColor = GetStatusBackgroundColor(strategy.Status),
                    StatusTextColor = Colors.White,
                    PnLColor = strategy.PnL >= 0 ? Color.FromArgb("#10B981") : Color.FromArgb("#EF4444"),
                    ToggleButtonText = strategy.Status == "Running" ? "Pause" : "Resume",
                    ToggleButtonStyle = strategy.Status == "Running"
                        ? Application.Current?.Resources["SecondaryButton"] as Style ?? new Style(typeof(Button))
                        : Application.Current?.Resources["PrimaryButton"] as Style ?? new Style(typeof(Button))
                });
            }
        }
        finally { IsRefreshing = false; }
    }

    private static string FormatRunningTime(DateTime startedAt)
    {
        var elapsed = DateTime.Now - startedAt;
        if (elapsed.TotalHours < 1)
            return $"{elapsed.Minutes}m";
        if (elapsed.TotalDays < 1)
            return $"{(int)elapsed.TotalHours}h {elapsed.Minutes}m";
        return $"{(int)elapsed.TotalDays}d {elapsed.Hours}h";
    }

    private static Color GetStatusBackgroundColor(string status) => status.ToLowerInvariant() switch
    {
        "running" => Color.FromArgb("#059669"),
        "paused" => Color.FromArgb("#D97706"),
        "stopped" or "error" => Color.FromArgb("#DC2626"),
        _ => Color.FromArgb("#64748B")
    };

    [RelayCommand]
    private async Task ToggleStrategyAsync(StrategyItemViewModel strategy)
    {
        if (strategy.IsRunning)
            await _strategyService.StopStrategyAsync(strategy.Id);
        else
            await _strategyService.StartStrategyAsync(strategy.Id);
        await RefreshAsync();
    }

    [RelayCommand]
    private async Task StopStrategyAsync(StrategyItemViewModel strategy)
    {
        var confirm = await Application.Current!.MainPage!.DisplayAlert(
            "Stop Strategy", $"Stop {strategy.Name}?", "Yes", "No");
        if (!confirm) return;

        await _strategyService.StopStrategyAsync(strategy.Id);
        await RefreshAsync();
    }

    [RelayCommand]
    private async Task ViewDetailsAsync(StrategyItemViewModel strategy)
    {
        await Shell.Current.GoToAsync($"strategyDetail?id={strategy.Id}");
    }

    [RelayCommand]
    private async Task OpenStrategyBuilderAsync()
    {
        // Navigate to strategy builder
        await Shell.Current.DisplayAlert("Strategy Builder", "Opening Strategy Builder app...", "OK");
    }
}

public partial class StrategyItemViewModel : ObservableObject
{
    [ObservableProperty] private string _id = string.Empty;
    [ObservableProperty] private string _name = string.Empty;
    [ObservableProperty] private string _status = string.Empty;
    [ObservableProperty] private decimal _pnL;
    [ObservableProperty] private int _tradesCount;
    [ObservableProperty] private decimal _winRate;
    [ObservableProperty] private DateTime _startedAt;
    [ObservableProperty] private string _runningTime = string.Empty;
    [ObservableProperty] private bool _isRunning;
    [ObservableProperty] private Color _statusBackgroundColor = Colors.Gray;
    [ObservableProperty] private Color _statusTextColor = Colors.White;
    [ObservableProperty] private Color _pnLColor = Colors.White;
    [ObservableProperty] private string _toggleButtonText = "Pause";
    [ObservableProperty] private Style? _toggleButtonStyle;
}

public partial class StrategyStatusViewModel : ObservableObject
{
    [ObservableProperty] private string _name = string.Empty;
    [ObservableProperty] private string _status = string.Empty;
    [ObservableProperty] private decimal _pnL;
    [ObservableProperty] private int _tradesCount;
}

#endregion

#region Settings ViewModel

public partial class SettingsViewModel : ObservableObject
{
    private readonly ISettingsService _settingsService;
    private readonly IBrokerConnectionService _brokerService;

    [ObservableProperty] private string _selectedTheme = "Dark";
    [ObservableProperty] private bool _pushNotificationsEnabled = true;
    [ObservableProperty] private bool _priceAlertsEnabled = true;
    [ObservableProperty] private bool _orderFillsEnabled = true;
    [ObservableProperty] private bool _biometricEnabled;
    [ObservableProperty] private string _connectedBroker = string.Empty;
    [ObservableProperty] private bool _isConnected;
    public bool IsNotConnected => !IsConnected;
    [ObservableProperty] private string _connectionButtonText = "Connect";
    [ObservableProperty] private string _appVersion = "1.0.0";

    [ObservableProperty] private ObservableCollection<string> _themeOptions = new() { "Dark", "Light", "System" };

    public SettingsViewModel(ISettingsService settingsService, IBrokerConnectionService brokerService)
    {
        _settingsService = settingsService;
        _brokerService = brokerService;
    }

    public async Task LoadSettingsAsync()
    {
        SelectedTheme = await _settingsService.GetThemeAsync();
        BiometricEnabled = await _settingsService.GetBiometricEnabledAsync();
        PushNotificationsEnabled = await _settingsService.GetPushNotificationsEnabledAsync();
        IsConnected = await _brokerService.IsConnectedAsync();
        ConnectedBroker = await _brokerService.GetConnectedBrokerAsync();
        ConnectionButtonText = IsConnected ? "Disconnect" : "Connect";
        OnPropertyChanged(nameof(IsNotConnected));
    }

    partial void OnSelectedThemeChanged(string value)
    {
        _ = _settingsService.SetThemeAsync(value);
    }

    partial void OnBiometricEnabledChanged(bool value)
    {
        _ = _settingsService.SetBiometricEnabledAsync(value);
    }

    partial void OnPushNotificationsEnabledChanged(bool value)
    {
        _ = _settingsService.SetPushNotificationsEnabledAsync(value);
    }

    [RelayCommand]
    private async Task ManageBrokerAsync()
    {
        if (IsConnected)
        {
            await _brokerService.DisconnectAsync();
            IsConnected = false;
            ConnectedBroker = string.Empty;
            ConnectionButtonText = "Connect";
        }
        else
        {
            await Shell.Current.GoToAsync("brokerConnection");
        }
        OnPropertyChanged(nameof(IsNotConnected));
    }

    [RelayCommand]
    private async Task LogoutAsync()
    {
        await _brokerService.DisconnectAsync();
        IsConnected = false;
        ConnectedBroker = string.Empty;
        ConnectionButtonText = "Connect";
        OnPropertyChanged(nameof(IsNotConnected));
    }

    [RelayCommand]
    private async Task OpenHelpAsync()
    {
        await Browser.OpenAsync("https://ooplesfinance.com/help");
    }

    [RelayCommand]
    private async Task ContactSupportAsync()
    {
        await Browser.OpenAsync("mailto:support@ooplesfinance.com");
    }

    [RelayCommand]
    private async Task OpenPrivacyPolicyAsync()
    {
        await Browser.OpenAsync("https://ooplesfinance.com/privacy");
    }

    [RelayCommand]
    private async Task OpenTermsAsync()
    {
        await Browser.OpenAsync("https://ooplesfinance.com/terms");
    }
}

#endregion

#region Broker Connection ViewModel

public partial class BrokerConnectionViewModel : ObservableObject
{
    private readonly IBrokerConnectionService _brokerService;

    [ObservableProperty] private ObservableCollection<BrokerOption> _brokers = new();
    [ObservableProperty] private BrokerOption? _selectedBroker;
    [ObservableProperty] private string _apiKey = string.Empty;
    [ObservableProperty] private string _apiSecret = string.Empty;
    [ObservableProperty] private bool _isPaperTrading = true;
    [ObservableProperty] private bool _isLoading;
    [ObservableProperty] private string _errorMessage = string.Empty;
    [ObservableProperty] private bool _hasError;

    public bool ShowCredentialsForm => SelectedBroker is not null;
    public string ConnectButtonText => IsLoading ? "Connecting..." : "Connect";
    public bool CanConnect => SelectedBroker is not null && !string.IsNullOrEmpty(ApiKey) && !string.IsNullOrEmpty(ApiSecret) && !IsLoading;

    public BrokerConnectionViewModel(IBrokerConnectionService brokerService)
    {
        _brokerService = brokerService;
    }

    public void Initialize()
    {
        Brokers = new ObservableCollection<BrokerOption>
        {
            new() { Name = "Alpaca", Description = "Commission-free stock & crypto trading", IconSource = "alpaca_icon.png" },
            new() { Name = "Interactive Brokers", Description = "Professional trading platform", IconSource = "ib_icon.png" },
            new() { Name = "TD Ameritrade", Description = "Full-featured retail broker", IconSource = "td_icon.png" },
            new() { Name = "Tradier", Description = "Low-cost brokerage for developers", IconSource = "tradier_icon.png" },
            new() { Name = "Coinbase", Description = "Cryptocurrency trading", IconSource = "coinbase_icon.png" }
        };
    }

    partial void OnSelectedBrokerChanged(BrokerOption? value)
    {
        OnPropertyChanged(nameof(ShowCredentialsForm));
        OnPropertyChanged(nameof(CanConnect));
    }

    partial void OnApiKeyChanged(string value) => OnPropertyChanged(nameof(CanConnect));
    partial void OnApiSecretChanged(string value) => OnPropertyChanged(nameof(CanConnect));

    [RelayCommand]
    private async Task ConnectAsync()
    {
        if (SelectedBroker is null) return;

        IsLoading = true;
        HasError = false;
        ErrorMessage = string.Empty;
        OnPropertyChanged(nameof(ConnectButtonText));
        OnPropertyChanged(nameof(CanConnect));

        try
        {
            var success = await _brokerService.ConnectAsync(SelectedBroker.Name, ApiKey, ApiSecret, IsPaperTrading);
            if (success)
            {
                await Shell.Current.GoToAsync("..");
            }
            else
            {
                HasError = true;
                ErrorMessage = "Failed to connect. Please check your API credentials.";
            }
        }
        catch (Exception ex)
        {
            HasError = true;
            ErrorMessage = $"Connection error: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
            OnPropertyChanged(nameof(ConnectButtonText));
            OnPropertyChanged(nameof(CanConnect));
        }
    }

    [RelayCommand]
    private async Task ShowApiKeyHelpAsync()
    {
        var url = SelectedBroker?.Name switch
        {
            "Alpaca" => "https://alpaca.markets/docs/api-references/trading-api/",
            "Interactive Brokers" => "https://interactivebrokers.github.io/tws-api/",
            "TD Ameritrade" => "https://developer.tdameritrade.com/",
            "Tradier" => "https://documentation.tradier.com/",
            "Coinbase" => "https://docs.cdp.coinbase.com/",
            _ => "https://ooplesfinance.com/help/api-keys"
        };
        await Browser.OpenAsync(url);
    }

    [RelayCommand]
    private async Task UseDemoModeAsync()
    {
        // Skip connection and use demo mode with sample data
        await Shell.Current.GoToAsync("..");
    }
}

public partial class BrokerOption : ObservableObject
{
    [ObservableProperty] private string _name = string.Empty;
    [ObservableProperty] private string _description = string.Empty;
    [ObservableProperty] private string _iconSource = string.Empty;
}

#endregion

#region Position Detail ViewModel

public partial class PositionDetailViewModel : ObservableObject
{
    private readonly IPortfolioService _portfolioService;
    private readonly IOrderService _orderService;

    [ObservableProperty] private string _symbol = string.Empty;
    [ObservableProperty] private string _companyName = string.Empty;
    [ObservableProperty] private decimal _quantity;
    [ObservableProperty] private decimal _averagePrice;
    [ObservableProperty] private decimal _currentPrice;
    [ObservableProperty] private decimal _marketValue;
    [ObservableProperty] private decimal _costBasis;
    [ObservableProperty] private decimal _unrealizedPnL;
    [ObservableProperty] private decimal _dayPnL;
    [ObservableProperty] private decimal _dayChange;
    [ObservableProperty] private decimal _dayChangePercent;
    [ObservableProperty] private decimal _totalReturnPercent;
    [ObservableProperty] private decimal _dayReturnPercent;

    [ObservableProperty] private Color _unrealizedPnLColor = Colors.White;
    [ObservableProperty] private Color _dayPnLColor = Colors.White;
    [ObservableProperty] private Color _dayChangeColor = Colors.White;
    [ObservableProperty] private Color _totalReturnColor = Colors.White;
    [ObservableProperty] private Color _dayReturnColor = Colors.White;

    public PositionDetailViewModel(IPortfolioService portfolioService, IOrderService orderService)
    {
        _portfolioService = portfolioService;
        _orderService = orderService;
    }

    public async Task LoadPositionAsync()
    {
        if (string.IsNullOrEmpty(Symbol)) return;

        var positions = await _portfolioService.GetPositionsAsync();
        var position = positions.FirstOrDefault(p => p.Symbol.Equals(Symbol, StringComparison.OrdinalIgnoreCase));

        if (position is null) return;

        CompanyName = position.CompanyName;
        Quantity = position.Quantity;
        AveragePrice = position.AveragePrice;
        CurrentPrice = position.CurrentPrice;
        MarketValue = position.MarketValue;
        CostBasis = position.AveragePrice * position.Quantity;
        UnrealizedPnL = position.UnrealizedPnL;
        DayPnL = position.DayPnL;

        // Calculate returns
        if (CostBasis > 0)
            TotalReturnPercent = UnrealizedPnL / CostBasis;

        if (MarketValue - DayPnL != 0)
            DayReturnPercent = DayPnL / (MarketValue - DayPnL);

        // Set colors
        UnrealizedPnLColor = UnrealizedPnL >= 0 ? Color.FromArgb("#10B981") : Color.FromArgb("#EF4444");
        DayPnLColor = DayPnL >= 0 ? Color.FromArgb("#10B981") : Color.FromArgb("#EF4444");
        TotalReturnColor = TotalReturnPercent >= 0 ? Color.FromArgb("#10B981") : Color.FromArgb("#EF4444");
        DayReturnColor = DayReturnPercent >= 0 ? Color.FromArgb("#10B981") : Color.FromArgb("#EF4444");
        DayChangeColor = DayPnL >= 0 ? Color.FromArgb("#10B981") : Color.FromArgb("#EF4444");
    }

    [RelayCommand]
    private async Task AddToPositionAsync()
    {
        await Shell.Current.GoToAsync($"//trade?symbol={Symbol}&side=buy");
    }

    [RelayCommand]
    private async Task ReducePositionAsync()
    {
        await Shell.Current.GoToAsync($"//trade?symbol={Symbol}&side=sell");
    }

    [RelayCommand]
    private async Task ClosePositionAsync()
    {
        var confirm = await Application.Current!.MainPage!.DisplayAlert(
            "Close Position",
            $"Are you sure you want to close your entire {Symbol} position ({Quantity} shares)?",
            "Yes", "No");

        if (!confirm) return;

        await _orderService.ClosePositionAsync(Symbol);
        await Shell.Current.GoToAsync("..");
    }

    [RelayCommand]
    private async Task SetStopLossAsync()
    {
        // Navigate to alert creation with stop loss preset
        await Shell.Current.GoToAsync($"addAlert?symbol={Symbol}&condition=below");
    }

    [RelayCommand]
    private async Task SetTakeProfitAsync()
    {
        // Navigate to alert creation with take profit preset
        await Shell.Current.GoToAsync($"addAlert?symbol={Symbol}&condition=above");
    }

    [RelayCommand]
    private async Task ViewChartAsync()
    {
        await Shell.Current.GoToAsync($"chart?symbol={Symbol}");
    }
}

#endregion

#region Login ViewModel

public partial class LoginViewModel : ObservableObject
{
    private readonly IBrokerConnectionService _brokerService;

    [ObservableProperty] private string _selectedBroker = "Alpaca";
    [ObservableProperty] private ObservableCollection<string> _brokers = new() { "Alpaca", "Interactive Brokers", "TD Ameritrade", "Tradier" };
    [ObservableProperty] private string _apiKey = string.Empty;
    [ObservableProperty] private string _apiSecret = string.Empty;
    [ObservableProperty] private bool _isPaperTrading = true;
    [ObservableProperty] private bool _isLoading;

    public LoginViewModel(IBrokerConnectionService brokerService) => _brokerService = brokerService;

    [RelayCommand]
    private async Task LoginAsync()
    {
        IsLoading = true;
        try
        {
            var success = await _brokerService.ConnectAsync(SelectedBroker, ApiKey, ApiSecret, IsPaperTrading);
            if (success)
            {
                await Shell.Current.GoToAsync("..");
            }
            else
            {
                await Application.Current!.MainPage!.DisplayAlert("Connection Failed", "Failed to connect to broker. Check credentials.", "OK");
            }
        }
        finally
        {
            IsLoading = false;
        }
    }
}

#endregion

#region Supabase Auth ViewModels

/// <summary>
/// ViewModel for Supabase-based login.
/// </summary>
public partial class AuthLoginViewModel : ObservableObject
{
    private readonly Cloud.SupabaseClient _supabase;

    [ObservableProperty] private string _email = string.Empty;
    [ObservableProperty] private string _password = string.Empty;
    [ObservableProperty] private bool _isLoading;
    [ObservableProperty] private string _errorMessage = string.Empty;
    [ObservableProperty] private bool _hasError;

    public bool IsNotLoading => !IsLoading;

    public AuthLoginViewModel(Cloud.SupabaseClient supabase)
    {
        _supabase = supabase ?? throw new ArgumentNullException(nameof(supabase));
    }

    [RelayCommand]
    private async Task SignInAsync()
    {
        if (string.IsNullOrWhiteSpace(Email) || string.IsNullOrWhiteSpace(Password))
        {
            HasError = true;
            ErrorMessage = "Please enter email and password.";
            return;
        }

        IsLoading = true;
        HasError = false;
        OnPropertyChanged(nameof(IsNotLoading));

        try
        {
            var result = await _supabase.SignInAsync(Email, Password);
            if (result.Success)
            {
                // Store session for persistence
                await SecureStorage.SetAsync("supabase_user_id", result.UserId ?? string.Empty);

                // Navigate to main app
                Application.Current!.MainPage = new AppShell();
            }
            else
            {
                HasError = true;
                ErrorMessage = result.ErrorMessage ?? "Invalid email or password.";
            }
        }
        catch (Exception ex)
        {
            HasError = true;
            ErrorMessage = $"Sign in failed: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
            OnPropertyChanged(nameof(IsNotLoading));
        }
    }

    [RelayCommand]
    private async Task SignInWithGoogleAsync()
    {
        var url = _supabase.GetOAuthSignInUrl("google", "ooplesfinance://auth/callback");
        await Browser.OpenAsync(url, BrowserLaunchMode.SystemPreferred);
    }

    [RelayCommand]
    private async Task SignInWithGitHubAsync()
    {
        var url = _supabase.GetOAuthSignInUrl("github", "ooplesfinance://auth/callback");
        await Browser.OpenAsync(url, BrowserLaunchMode.SystemPreferred);
    }

    [RelayCommand]
    private async Task ForgotPasswordAsync()
    {
        if (string.IsNullOrWhiteSpace(Email))
        {
            await Application.Current!.MainPage!.DisplayAlert("Reset Password", "Please enter your email address first.", "OK");
            return;
        }

        var success = await _supabase.ResetPasswordAsync(Email);
        if (success)
        {
            await Application.Current!.MainPage!.DisplayAlert("Reset Password", "Password reset email sent. Check your inbox.", "OK");
        }
        else
        {
            await Application.Current!.MainPage!.DisplayAlert("Error", "Failed to send reset email. Please try again.", "OK");
        }
    }

    [RelayCommand]
    private async Task GoToRegisterAsync()
    {
        await Shell.Current.GoToAsync("register");
    }
}

/// <summary>
/// ViewModel for Supabase-based registration.
/// </summary>
public partial class RegisterViewModel : ObservableObject
{
    private readonly Cloud.SupabaseClient _supabase;

    [ObservableProperty] private string _displayName = string.Empty;
    [ObservableProperty] private string _email = string.Empty;
    [ObservableProperty] private string _password = string.Empty;
    [ObservableProperty] private string _confirmPassword = string.Empty;
    [ObservableProperty] private bool _agreedToTerms;
    [ObservableProperty] private bool _isLoading;
    [ObservableProperty] private string _errorMessage = string.Empty;
    [ObservableProperty] private bool _hasError;

    public bool CanCreateAccount => !IsLoading && AgreedToTerms &&
        !string.IsNullOrWhiteSpace(Email) &&
        !string.IsNullOrWhiteSpace(Password) &&
        Password == ConfirmPassword;

    public RegisterViewModel(Cloud.SupabaseClient supabase)
    {
        _supabase = supabase ?? throw new ArgumentNullException(nameof(supabase));
    }

    partial void OnEmailChanged(string value) => OnPropertyChanged(nameof(CanCreateAccount));
    partial void OnPasswordChanged(string value) => OnPropertyChanged(nameof(CanCreateAccount));
    partial void OnConfirmPasswordChanged(string value) => OnPropertyChanged(nameof(CanCreateAccount));
    partial void OnAgreedToTermsChanged(bool value) => OnPropertyChanged(nameof(CanCreateAccount));

    [RelayCommand]
    private async Task CreateAccountAsync()
    {
        if (!ValidateInput())
            return;

        IsLoading = true;
        HasError = false;
        OnPropertyChanged(nameof(CanCreateAccount));

        try
        {
            var result = await _supabase.SignUpAsync(Email, Password);
            if (result.Success)
            {
                await Application.Current!.MainPage!.DisplayAlert(
                    "Account Created",
                    "Please check your email to verify your account, then sign in.",
                    "OK");

                await Shell.Current.GoToAsync("..");
            }
            else
            {
                HasError = true;
                ErrorMessage = result.ErrorMessage ?? "Failed to create account.";
            }
        }
        catch (Exception ex)
        {
            HasError = true;
            ErrorMessage = $"Registration failed: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
            OnPropertyChanged(nameof(CanCreateAccount));
        }
    }

    private bool ValidateInput()
    {
        if (string.IsNullOrWhiteSpace(Email))
        {
            HasError = true;
            ErrorMessage = "Please enter your email address.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(Password))
        {
            HasError = true;
            ErrorMessage = "Please enter a password.";
            return false;
        }

        if (Password.Length < 8)
        {
            HasError = true;
            ErrorMessage = "Password must be at least 8 characters.";
            return false;
        }

        if (Password != ConfirmPassword)
        {
            HasError = true;
            ErrorMessage = "Passwords do not match.";
            return false;
        }

        if (!AgreedToTerms)
        {
            HasError = true;
            ErrorMessage = "Please agree to the Terms of Service.";
            return false;
        }

        return true;
    }

    [RelayCommand]
    private async Task GoToLoginAsync()
    {
        await Shell.Current.GoToAsync("..");
    }
}

#endregion

#region Order Confirmation ViewModel

/// <summary>
/// ViewModel for order confirmation popup with safety checks.
/// </summary>
public partial class OrderConfirmationViewModel : ObservableObject
{
    private readonly IOrderService _orderService;
    private readonly IPortfolioService _portfolioService;

    [ObservableProperty] private string _symbol = string.Empty;
    [ObservableProperty] private string _side = string.Empty;
    [ObservableProperty] private string _orderType = "Market";
    [ObservableProperty] private decimal _quantity;
    [ObservableProperty] private decimal? _limitPrice;
    [ObservableProperty] private decimal? _stopPrice;
    [ObservableProperty] private string _timeInForce = "Day";
    [ObservableProperty] private decimal _estimatedPrice;
    [ObservableProperty] private decimal _currentPosition;
    [ObservableProperty] private bool _explicitlyConfirmed;
    [ObservableProperty] private bool _isSubmitting;

    // Computed properties
    public bool IsMarketOrder => OrderType.Equals("Market", StringComparison.OrdinalIgnoreCase);
    public bool IsLargeOrder => EstimatedTotal > 10000m; // $10K threshold
    public bool HasPrice => LimitPrice.HasValue || StopPrice.HasValue;
    public bool HasPositionImpact => CurrentPosition != 0;
    public bool RequiresExplicitConfirmation => IsLargeOrder || IsMarketOrder && EstimatedTotal > 5000m;

    public string PriceLabel => StopPrice.HasValue ? "Stop Price" : "Limit Price";
    public string PriceDisplay => (LimitPrice ?? StopPrice ?? 0).ToString("C2");
    public decimal EstimatedTotal => Quantity * (LimitPrice ?? EstimatedPrice);
    public string EstimatedLabel => Side.Equals("buy", StringComparison.OrdinalIgnoreCase) ? "Estimated Cost" : "Estimated Proceeds";

    public decimal NewPosition => Side.Equals("buy", StringComparison.OrdinalIgnoreCase)
        ? CurrentPosition + Quantity
        : CurrentPosition - Quantity;

    public Color SideColor => Side.Equals("buy", StringComparison.OrdinalIgnoreCase)
        ? Color.FromArgb("#10B981")
        : Color.FromArgb("#EF4444");

    public Color EstimatedColor => Side.Equals("buy", StringComparison.OrdinalIgnoreCase)
        ? Color.FromArgb("#EF4444")
        : Color.FromArgb("#10B981");

    public Color ConfirmButtonColor => Side.Equals("buy", StringComparison.OrdinalIgnoreCase)
        ? Color.FromArgb("#10B981")
        : Color.FromArgb("#EF4444");

    public string ConfirmButtonText => Side.Equals("buy", StringComparison.OrdinalIgnoreCase)
        ? "Confirm Buy"
        : "Confirm Sell";

    public bool CanConfirm => !IsSubmitting && (!RequiresExplicitConfirmation || ExplicitlyConfirmed);

    public OrderConfirmationViewModel(IOrderService orderService, IPortfolioService portfolioService)
    {
        _orderService = orderService ?? throw new ArgumentNullException(nameof(orderService));
        _portfolioService = portfolioService ?? throw new ArgumentNullException(nameof(portfolioService));
    }

    public async Task InitializeAsync()
    {
        // Load current position if any
        var positions = await _portfolioService.GetPositionsAsync();
        var position = positions.FirstOrDefault(p => p.Symbol.Equals(Symbol, StringComparison.OrdinalIgnoreCase));
        CurrentPosition = position?.Quantity ?? 0;

        OnPropertyChanged(nameof(HasPositionImpact));
        OnPropertyChanged(nameof(NewPosition));
    }

    partial void OnExplicitlyConfirmedChanged(bool value) => OnPropertyChanged(nameof(CanConfirm));

    [RelayCommand]
    private async Task ConfirmAsync()
    {
        if (!CanConfirm)
            return;

        IsSubmitting = true;
        OnPropertyChanged(nameof(CanConfirm));

        try
        {
            var request = new OrderRequest
            {
                Symbol = Symbol,
                Side = Side.ToLowerInvariant(),
                Quantity = (int)Quantity,
                OrderType = OrderType.ToLowerInvariant(),
                LimitPrice = LimitPrice,
                StopPrice = StopPrice,
                TimeInForce = TimeInForce.ToLowerInvariant()
            };

            var result = await _orderService.SubmitOrderAsync(request);

            if (result.Success)
            {
                await Application.Current!.MainPage!.DisplayAlert(
                    "Order Submitted",
                    $"Order {result.OrderId} submitted successfully.",
                    "OK");

                await Shell.Current.GoToAsync("..");
            }
            else
            {
                await Application.Current!.MainPage!.DisplayAlert(
                    "Order Failed",
                    result.Message ?? "Failed to submit order.",
                    "OK");
            }
        }
        catch (Exception ex)
        {
            await Application.Current!.MainPage!.DisplayAlert(
                "Error",
                $"Order submission failed: {ex.Message}",
                "OK");
        }
        finally
        {
            IsSubmitting = false;
            OnPropertyChanged(nameof(CanConfirm));
        }
    }

    [RelayCommand]
    private async Task CancelAsync()
    {
        await Shell.Current.GoToAsync("..");
    }
}

#endregion

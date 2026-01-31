using System.Collections.ObjectModel;
using OoplesFinance.TradingApp.Maui.Models;
using OoplesFinance.TradingApp.Maui.Services;

namespace OoplesFinance.TradingApp.Maui.ViewModels;

/// <summary>
/// ViewModel for the main Dashboard page.
/// </summary>
public class DashboardViewModel : BaseViewModel
{
    private readonly IPortfolioService _portfolioService;
    private readonly IMarketDataService _marketDataService;
    private readonly IOrderService _orderService;
    private readonly ISettingsService _settingsService;

    private decimal _portfolioValue;
    private decimal _cash;
    private decimal _buyingPower;
    private decimal _todayPnL;
    private decimal _totalPnL;
    private decimal _todayPnLPercent;
    private decimal _totalPnLPercent;
    private bool _marketIsOpen;
    private string _marketStatusText = "Loading...";
    private int _openOrdersCount;
    private int _positionsCount;

    public decimal PortfolioValue
    {
        get => _portfolioValue;
        set => SetProperty(ref _portfolioValue, value);
    }

    public decimal Cash
    {
        get => _cash;
        set => SetProperty(ref _cash, value);
    }

    public decimal BuyingPower
    {
        get => _buyingPower;
        set => SetProperty(ref _buyingPower, value);
    }

    public decimal TodayPnL
    {
        get => _todayPnL;
        set
        {
            if (SetProperty(ref _todayPnL, value))
                OnPropertyChanged(nameof(TodayPnLColor));
        }
    }

    public decimal TotalPnL
    {
        get => _totalPnL;
        set
        {
            if (SetProperty(ref _totalPnL, value))
                OnPropertyChanged(nameof(TotalPnLColor));
        }
    }

    public decimal TodayPnLPercent
    {
        get => _todayPnLPercent;
        set => SetProperty(ref _todayPnLPercent, value);
    }

    public decimal TotalPnLPercent
    {
        get => _totalPnLPercent;
        set => SetProperty(ref _totalPnLPercent, value);
    }

    public bool MarketIsOpen
    {
        get => _marketIsOpen;
        set
        {
            if (SetProperty(ref _marketIsOpen, value))
                OnPropertyChanged(nameof(MarketStatusColor));
        }
    }

    public string MarketStatusText
    {
        get => _marketStatusText;
        set => SetProperty(ref _marketStatusText, value);
    }

    public int OpenOrdersCount
    {
        get => _openOrdersCount;
        set => SetProperty(ref _openOrdersCount, value);
    }

    public int PositionsCount
    {
        get => _positionsCount;
        set => SetProperty(ref _positionsCount, value);
    }

    public Color TodayPnLColor => TodayPnL >= 0
        ? Color.FromArgb("#10B981")  // Green
        : Color.FromArgb("#EF4444"); // Red

    public Color TotalPnLColor => TotalPnL >= 0
        ? Color.FromArgb("#10B981")
        : Color.FromArgb("#EF4444");

    public Color MarketStatusColor => MarketIsOpen
        ? Color.FromArgb("#10B981")
        : Color.FromArgb("#6B7280");

    public ObservableCollection<Position> TopPositions { get; } = new();
    public ObservableCollection<Order> RecentOrders { get; } = new();
    public ObservableCollection<WatchlistItem> Watchlist { get; } = new();

    public AsyncCommand RefreshCommand { get; }
    public AsyncCommand NavigateToPositionsCommand { get; }
    public AsyncCommand NavigateToOrdersCommand { get; }
    public AsyncCommand NavigateToTradeCommand { get; }

    public DashboardViewModel(
        IPortfolioService portfolioService,
        IMarketDataService marketDataService,
        IOrderService orderService,
        ISettingsService settingsService)
    {
        _portfolioService = portfolioService ?? throw new ArgumentNullException(nameof(portfolioService));
        _marketDataService = marketDataService ?? throw new ArgumentNullException(nameof(marketDataService));
        _orderService = orderService ?? throw new ArgumentNullException(nameof(orderService));
        _settingsService = settingsService ?? throw new ArgumentNullException(nameof(settingsService));

        RefreshCommand = new AsyncCommand(async () => await RefreshAsync(LoadDashboardAsync));
        NavigateToPositionsCommand = new AsyncCommand(async () => await Shell.Current.GoToAsync("positions"));
        NavigateToOrdersCommand = new AsyncCommand(async () => await Shell.Current.GoToAsync("orders"));
        NavigateToTradeCommand = new AsyncCommand(async () => await Shell.Current.GoToAsync("trade"));
    }

    /// <summary>
    /// Loads all dashboard data.
    /// </summary>
    public async Task LoadDashboardAsync()
    {
        // Load all data in parallel
        var accountTask = _portfolioService.GetAccountAsync();
        var positionsTask = _portfolioService.GetPositionsAsync();
        var ordersTask = _orderService.GetOpenOrdersAsync();
        var marketStatusTask = _marketDataService.GetMarketStatusAsync();
        var watchlistTask = _settingsService.GetWatchlistAsync();

        await Task.WhenAll(accountTask, positionsTask, ordersTask, marketStatusTask, watchlistTask)
            .ConfigureAwait(false);

        var account = accountTask.Result;
        var positions = positionsTask.Result;
        var orders = ordersTask.Result;
        var marketStatus = marketStatusTask.Result;
        var watchlistSymbols = watchlistTask.Result;

        // Load quotes for watchlist
        var watchlistQuotes = watchlistSymbols.Count > 0
            ? await _marketDataService.GetQuotesAsync(watchlistSymbols).ConfigureAwait(false)
            : Array.Empty<Quote>();

        await MainThread.InvokeOnMainThreadAsync(() =>
        {
            // Account info
            PortfolioValue = account.PortfolioValue;
            Cash = account.Cash;
            BuyingPower = account.BuyingPower;
            TodayPnL = account.TodayPnL;
            TotalPnL = account.TotalPnL;
            TodayPnLPercent = account.TodayPnLPercent;
            TotalPnLPercent = account.TotalPnLPercent;

            // Market status
            MarketIsOpen = marketStatus.IsOpen;
            MarketStatusText = marketStatus.StatusText;

            // Counts
            OpenOrdersCount = orders.Count;
            PositionsCount = positions.Count;

            // Top positions (by market value)
            TopPositions.Clear();
            foreach (var position in positions.OrderByDescending(p => p.MarketValue).Take(5))
            {
                TopPositions.Add(position);
            }

            // Recent orders
            RecentOrders.Clear();
            foreach (var order in orders.Take(5))
            {
                RecentOrders.Add(order);
            }

            // Watchlist
            Watchlist.Clear();
            foreach (var quote in watchlistQuotes)
            {
                Watchlist.Add(new WatchlistItem
                {
                    Symbol = quote.Symbol,
                    CompanyName = quote.CompanyName,
                    LastPrice = quote.LastPrice,
                    Change = quote.Change,
                    ChangePercent = quote.ChangePercent
                });
            }
        });
    }
}

/// <summary>
/// Item in the watchlist display.
/// </summary>
public class WatchlistItem
{
    public string Symbol { get; set; } = string.Empty;
    public string CompanyName { get; set; } = string.Empty;
    public decimal LastPrice { get; set; }
    public decimal Change { get; set; }
    public decimal ChangePercent { get; set; }

    public Color ChangeColor => Change >= 0
        ? Color.FromArgb("#10B981")
        : Color.FromArgb("#EF4444");

    public string ChangeDisplay => Change >= 0
        ? $"+{Change:F2} (+{ChangePercent:P2})"
        : $"{Change:F2} ({ChangePercent:P2})";
}

using System.Collections.ObjectModel;
using OoplesFinance.TradingApp.Maui.Models;
using OoplesFinance.TradingApp.Maui.Services;

namespace OoplesFinance.TradingApp.Maui.ViewModels;

/// <summary>
/// ViewModel for the Positions page displaying portfolio holdings.
/// </summary>
public class PositionsViewModel : BaseViewModel
{
    private readonly IPortfolioService _portfolioService;
    private readonly IOrderService _orderService;
    private decimal _totalMarketValue;
    private decimal _totalUnrealizedPnL;
    private decimal _totalDayPnL;
    private Position? _selectedPosition;

    public ObservableCollection<Position> Positions { get; } = new();

    public decimal TotalMarketValue
    {
        get => _totalMarketValue;
        set => SetProperty(ref _totalMarketValue, value);
    }

    public decimal TotalUnrealizedPnL
    {
        get => _totalUnrealizedPnL;
        set => SetProperty(ref _totalUnrealizedPnL, value);
    }

    public decimal TotalDayPnL
    {
        get => _totalDayPnL;
        set => SetProperty(ref _totalDayPnL, value);
    }

    public Position? SelectedPosition
    {
        get => _selectedPosition;
        set => SetProperty(ref _selectedPosition, value);
    }

    public AsyncCommand RefreshCommand { get; }
    public AsyncCommand<Position> ClosePositionCommand { get; }
    public AsyncCommand<Position> ViewChartCommand { get; }

    public PositionsViewModel(IPortfolioService portfolioService, IOrderService orderService)
    {
        _portfolioService = portfolioService ?? throw new ArgumentNullException(nameof(portfolioService));
        _orderService = orderService ?? throw new ArgumentNullException(nameof(orderService));

        RefreshCommand = new AsyncCommand(async () => await RefreshAsync(LoadPositionsAsync));
        ClosePositionCommand = new AsyncCommand<Position>(ClosePositionAsync);
        ViewChartCommand = new AsyncCommand<Position>(ViewChartAsync);
    }

    /// <summary>
    /// Loads all positions from the portfolio service.
    /// </summary>
    public async Task LoadPositionsAsync()
    {
        var positions = await _portfolioService.GetPositionsAsync().ConfigureAwait(false);

        await MainThread.InvokeOnMainThreadAsync(() =>
        {
            Positions.Clear();
            foreach (var position in positions)
            {
                Positions.Add(position);
            }

            TotalMarketValue = positions.Sum(p => p.MarketValue);
            TotalUnrealizedPnL = positions.Sum(p => p.UnrealizedPnL);
            TotalDayPnL = positions.Sum(p => p.DayPnL);
        });
    }

    private async Task ClosePositionAsync(Position? position)
    {
        if (position is null)
            return;

        var request = new OrderRequest
        {
            Symbol = position.Symbol,
            Side = position.Quantity > 0 ? "sell" : "buy",
            Quantity = Math.Abs(position.Quantity),
            OrderType = "market"
        };

        await _orderService.SubmitOrderAsync(request).ConfigureAwait(false);
        await LoadPositionsAsync().ConfigureAwait(false);
    }

    private async Task ViewChartAsync(Position? position)
    {
        if (position is null)
            return;

        await Shell.Current.GoToAsync($"chart?symbol={position.Symbol}").ConfigureAwait(false);
    }
}

/// <summary>
/// ViewModel for order entry.
/// </summary>
public class OrderEntryViewModel : BaseViewModel
{
    private readonly IMarketDataService _marketDataService;
    private readonly IOrderService _orderService;
    private readonly IPortfolioService _portfolioService;

    private string _symbol = string.Empty;
    private string _companyName = string.Empty;
    private decimal _currentPrice;
    private decimal _quantity;
    private string _orderType = "market";
    private decimal? _limitPrice;
    private string _side = "buy";

    public string Symbol
    {
        get => _symbol;
        set
        {
            if (SetProperty(ref _symbol, value?.ToUpperInvariant() ?? string.Empty))
                OnPropertyChanged(nameof(CanSubmit));
        }
    }

    public string CompanyName
    {
        get => _companyName;
        set => SetProperty(ref _companyName, value);
    }

    public decimal CurrentPrice
    {
        get => _currentPrice;
        set
        {
            if (SetProperty(ref _currentPrice, value))
                OnPropertyChanged(nameof(EstimatedTotal));
        }
    }

    public decimal Quantity
    {
        get => _quantity;
        set
        {
            if (SetProperty(ref _quantity, value))
            {
                OnPropertyChanged(nameof(EstimatedTotal));
                OnPropertyChanged(nameof(CanSubmit));
            }
        }
    }

    public string OrderType
    {
        get => _orderType;
        set => SetProperty(ref _orderType, value);
    }

    public decimal? LimitPrice
    {
        get => _limitPrice;
        set => SetProperty(ref _limitPrice, value);
    }

    public string Side
    {
        get => _side;
        set => SetProperty(ref _side, value);
    }

    public decimal EstimatedTotal => Quantity * CurrentPrice;

    public bool CanSubmit => !string.IsNullOrEmpty(Symbol) && Quantity > 0 && !IsLoading;

    public AsyncCommand SearchSymbolCommand { get; }
    public AsyncCommand SubmitOrderCommand { get; }

    public OrderEntryViewModel(
        IMarketDataService marketDataService,
        IOrderService orderService,
        IPortfolioService portfolioService)
    {
        _marketDataService = marketDataService ?? throw new ArgumentNullException(nameof(marketDataService));
        _orderService = orderService ?? throw new ArgumentNullException(nameof(orderService));
        _portfolioService = portfolioService ?? throw new ArgumentNullException(nameof(portfolioService));

        SearchSymbolCommand = new AsyncCommand(SearchSymbolAsync, () => !string.IsNullOrEmpty(Symbol));
        SubmitOrderCommand = new AsyncCommand(SubmitOrderAsync, () => CanSubmit);
    }

    private async Task SearchSymbolAsync()
    {
        if (string.IsNullOrEmpty(Symbol))
            return;

        await ExecuteAsync(async () =>
        {
            var quote = await _marketDataService.GetQuoteAsync(Symbol).ConfigureAwait(false);
            if (quote is not null)
            {
                await MainThread.InvokeOnMainThreadAsync(() =>
                {
                    CurrentPrice = quote.LastPrice;
                    CompanyName = quote.CompanyName;
                });
            }
        });
    }

    private async Task SubmitOrderAsync()
    {
        await ExecuteAsync(async () =>
        {
            var request = new OrderRequest
            {
                Symbol = Symbol,
                Side = Side,
                Quantity = Quantity,
                OrderType = OrderType,
                LimitPrice = OrderType == "limit" ? LimitPrice : null
            };

            await _orderService.SubmitOrderAsync(request).ConfigureAwait(false);

            // Navigate back or to confirmation
            await Shell.Current.GoToAsync("..").ConfigureAwait(false);
        });
    }
}

/// <summary>
/// ViewModel for order confirmation.
/// </summary>
public class OrderConfirmationViewModel : BaseViewModel
{
    private readonly IOrderService _orderService;
    private readonly IPortfolioService _portfolioService;

    private string _symbol = string.Empty;
    private string _side = "buy";
    private decimal _quantity;
    private decimal _estimatedPrice;
    private decimal _currentPosition;
    private bool _explicitlyConfirmed;

    public string Symbol
    {
        get => _symbol;
        set => SetProperty(ref _symbol, value);
    }

    public string Side
    {
        get => _side;
        set
        {
            if (SetProperty(ref _side, value))
            {
                OnPropertyChanged(nameof(SideColor));
                OnPropertyChanged(nameof(NewPosition));
            }
        }
    }

    public decimal Quantity
    {
        get => _quantity;
        set
        {
            if (SetProperty(ref _quantity, value))
            {
                OnPropertyChanged(nameof(EstimatedTotal));
                OnPropertyChanged(nameof(IsLargeOrder));
                OnPropertyChanged(nameof(CanConfirm));
                OnPropertyChanged(nameof(NewPosition));
            }
        }
    }

    public decimal EstimatedPrice
    {
        get => _estimatedPrice;
        set
        {
            if (SetProperty(ref _estimatedPrice, value))
            {
                OnPropertyChanged(nameof(EstimatedTotal));
                OnPropertyChanged(nameof(IsLargeOrder));
                OnPropertyChanged(nameof(CanConfirm));
            }
        }
    }

    public decimal CurrentPosition
    {
        get => _currentPosition;
        set
        {
            if (SetProperty(ref _currentPosition, value))
                OnPropertyChanged(nameof(NewPosition));
        }
    }

    public bool ExplicitlyConfirmed
    {
        get => _explicitlyConfirmed;
        set
        {
            if (SetProperty(ref _explicitlyConfirmed, value))
                OnPropertyChanged(nameof(CanConfirm));
        }
    }

    public decimal EstimatedTotal => Quantity * EstimatedPrice;
    public bool IsLargeOrder => EstimatedTotal > 10000;
    public bool CanConfirm => !IsLargeOrder || ExplicitlyConfirmed;
    public decimal NewPosition => Side == "buy" ? CurrentPosition + Quantity : CurrentPosition - Quantity;

    public Color SideColor => Side.ToLowerInvariant() == "buy"
        ? Color.FromArgb("#10B981")
        : Color.FromArgb("#EF4444");

    public AsyncCommand ConfirmCommand { get; }
    public AsyncCommand CancelCommand { get; }

    public OrderConfirmationViewModel(IOrderService orderService, IPortfolioService portfolioService)
    {
        _orderService = orderService ?? throw new ArgumentNullException(nameof(orderService));
        _portfolioService = portfolioService ?? throw new ArgumentNullException(nameof(portfolioService));

        ConfirmCommand = new AsyncCommand(ConfirmOrderAsync, () => CanConfirm);
        CancelCommand = new AsyncCommand(CancelAsync);
    }

    private async Task ConfirmOrderAsync()
    {
        await ExecuteAsync(async () =>
        {
            var request = new OrderRequest
            {
                Symbol = Symbol,
                Side = Side,
                Quantity = Quantity,
                OrderType = "market"
            };

            await _orderService.SubmitOrderAsync(request).ConfigureAwait(false);
            await Shell.Current.GoToAsync("../..").ConfigureAwait(false);
        });
    }

    private async Task CancelAsync()
    {
        await Shell.Current.GoToAsync("..").ConfigureAwait(false);
    }
}

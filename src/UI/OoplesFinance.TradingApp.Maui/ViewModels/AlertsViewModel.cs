using System.Collections.ObjectModel;
using OoplesFinance.TradingApp.Maui.Models;
using OoplesFinance.TradingApp.Maui.Services;

namespace OoplesFinance.TradingApp.Maui.ViewModels;

/// <summary>
/// ViewModel for displaying an alert item.
/// </summary>
public class AlertItemViewModel : BaseViewModel
{
    public string Id { get; set; } = string.Empty;
    public string Symbol { get; set; } = string.Empty;
    public string Condition { get; set; } = string.Empty;
    public decimal TargetPrice { get; set; }
    public bool IsActive { get; set; }
    public bool IsTriggered { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? TriggeredAt { get; set; }

    public string ConditionDisplay => $"{Condition} ${TargetPrice:F2}";

    public Color StatusColor => IsTriggered
        ? Color.FromArgb("#10B981")  // Green - triggered
        : IsActive
            ? Color.FromArgb("#3B82F6")  // Blue - active
            : Color.FromArgb("#6B7280"); // Gray - inactive

    public static AlertItemViewModel FromAlert(Alert alert)
    {
        return new AlertItemViewModel
        {
            Id = alert.Id,
            Symbol = alert.Symbol,
            Condition = alert.Condition,
            TargetPrice = alert.TargetPrice,
            IsActive = alert.IsActive,
            IsTriggered = alert.IsTriggered,
            CreatedAt = alert.CreatedAt,
            TriggeredAt = alert.TriggeredAt
        };
    }
}

/// <summary>
/// ViewModel for the Alerts page.
/// </summary>
public class AlertsViewModel : BaseViewModel
{
    private readonly IAlertService _alertService;

    private string _newAlertSymbol = string.Empty;
    private string _newAlertCondition = "Price above";
    private decimal _newAlertPrice;

    public ObservableCollection<AlertItemViewModel> Alerts { get; } = new();

    public string[] AvailableConditions { get; } =
    {
        "Price above",
        "Price below",
        "Crosses above",
        "Crosses below",
        "% change"
    };

    public string NewAlertSymbol
    {
        get => _newAlertSymbol;
        set
        {
            if (SetProperty(ref _newAlertSymbol, value?.ToUpperInvariant() ?? string.Empty))
                OnPropertyChanged(nameof(CanCreateAlert));
        }
    }

    public string NewAlertCondition
    {
        get => _newAlertCondition;
        set => SetProperty(ref _newAlertCondition, value);
    }

    public decimal NewAlertPrice
    {
        get => _newAlertPrice;
        set
        {
            if (SetProperty(ref _newAlertPrice, value))
                OnPropertyChanged(nameof(CanCreateAlert));
        }
    }

    public bool CanCreateAlert => !string.IsNullOrEmpty(NewAlertSymbol) && NewAlertPrice > 0;

    public AsyncCommand RefreshCommand { get; }
    public AsyncCommand CreateAlertCommand { get; }
    public AsyncCommand<AlertItemViewModel> DeleteAlertCommand { get; }
    public AsyncCommand<AlertItemViewModel> ToggleAlertCommand { get; }

    public AlertsViewModel(IAlertService alertService)
    {
        _alertService = alertService ?? throw new ArgumentNullException(nameof(alertService));

        RefreshCommand = new AsyncCommand(async () => await RefreshAsync(LoadAlertsAsync));
        CreateAlertCommand = new AsyncCommand(CreateAlertAsync, () => CanCreateAlert);
        DeleteAlertCommand = new AsyncCommand<AlertItemViewModel>(DeleteAlertAsync);
        ToggleAlertCommand = new AsyncCommand<AlertItemViewModel>(ToggleAlertAsync);
    }

    /// <summary>
    /// Loads all alerts from the alert service.
    /// </summary>
    public async Task LoadAlertsAsync()
    {
        var alerts = await _alertService.GetAllAlertsAsync().ConfigureAwait(false);

        await MainThread.InvokeOnMainThreadAsync(() =>
        {
            Alerts.Clear();
            foreach (var alert in alerts.OrderByDescending(a => a.CreatedAt))
            {
                Alerts.Add(AlertItemViewModel.FromAlert(alert));
            }
        });
    }

    private async Task CreateAlertAsync()
    {
        await ExecuteAsync(async () =>
        {
            await _alertService.CreateAlertAsync(NewAlertSymbol, NewAlertCondition, NewAlertPrice)
                .ConfigureAwait(false);

            // Reset form
            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                NewAlertSymbol = string.Empty;
                NewAlertPrice = 0;
            });

            await LoadAlertsAsync().ConfigureAwait(false);
        });
    }

    private async Task DeleteAlertAsync(AlertItemViewModel? alertVm)
    {
        if (alertVm is null)
            return;

        await ExecuteAsync(async () =>
        {
            await _alertService.DeleteAlertAsync(alertVm.Id).ConfigureAwait(false);
            await LoadAlertsAsync().ConfigureAwait(false);
        });
    }

    private async Task ToggleAlertAsync(AlertItemViewModel? alertVm)
    {
        if (alertVm is null)
            return;

        await ExecuteAsync(async () =>
        {
            await _alertService.UpdateAlertAsync(alertVm.Id, !alertVm.IsActive).ConfigureAwait(false);
            await LoadAlertsAsync().ConfigureAwait(false);
        });
    }
}

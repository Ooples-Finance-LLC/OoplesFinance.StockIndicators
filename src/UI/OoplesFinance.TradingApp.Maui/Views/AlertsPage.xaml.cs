using OoplesFinance.TradingApp.Maui.ViewModels;

namespace OoplesFinance.TradingApp.Maui.Views;

public partial class AlertsPage : ContentPage
{
    private readonly AlertsViewModel _viewModel;

    // Parameterless constructor for Shell navigation
    public AlertsPage() : this(
        App.Current?.Handler?.MauiContext?.Services.GetService<AlertsViewModel>()
        ?? new AlertsViewModel(new Services.AlertService()))
    {
        App.LogError("AlertsPage", "Parameterless constructor called");
    }

    public AlertsPage(AlertsViewModel viewModel)
    {
        App.LogError("AlertsPage", "Constructor starting");
        try
        {
            InitializeComponent();
            _viewModel = viewModel;
            BindingContext = viewModel;
            App.LogError("AlertsPage", "Constructor completed");
        }
        catch (Exception ex)
        {
            App.LogException("AlertsPage.Constructor", ex);
            throw;
        }
    }

    protected override async void OnAppearing()
    {
        App.LogError("AlertsPage.OnAppearing", "Starting");
        try
        {
            base.OnAppearing();
            await _viewModel.LoadAlertsAsync();
            App.LogError("AlertsPage.OnAppearing", "Completed successfully");
        }
        catch (Exception ex)
        {
            App.LogException("AlertsPage.OnAppearing", ex);
        }
    }
}

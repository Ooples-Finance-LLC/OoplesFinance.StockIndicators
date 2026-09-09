using OoplesFinance.TradingApp.Maui.ViewModels;

namespace OoplesFinance.TradingApp.Maui.Views;

public partial class DashboardPage : ContentPage
{
    private DashboardViewModel? _viewModel;

    public DashboardPage()
    {
        App.LogError("DashboardPage", "Constructor starting");
        try
        {
            InitializeComponent();
            App.LogError("DashboardPage", "Constructor completed");
        }
        catch (Exception ex)
        {
            App.LogException("DashboardPage.Constructor", ex);
            throw;
        }
    }

    public DashboardPage(DashboardViewModel viewModel)
    {
        App.LogError("DashboardPage", "DI Constructor starting");
        try
        {
            InitializeComponent();
            _viewModel = viewModel;
            BindingContext = viewModel;
            App.LogError("DashboardPage", "DI Constructor completed");
        }
        catch (Exception ex)
        {
            App.LogException("DashboardPage.DIConstructor", ex);
            throw;
        }
    }

    protected override async void OnAppearing()
    {
        App.LogError("DashboardPage.OnAppearing", "Starting");
        try
        {
            base.OnAppearing();

            // If BindingContext is not set (page created by Shell DataTemplate), get it from DI
            if (BindingContext is null || _viewModel is null)
            {
                _viewModel = Application.Current?.Handler?.MauiContext?.Services.GetService<DashboardViewModel>();
                if (_viewModel is not null)
                {
                    BindingContext = _viewModel;
                    App.LogError("DashboardPage.OnAppearing", "Got ViewModel from DI");
                }
                else
                {
                    App.LogError("DashboardPage.OnAppearing", "ERROR: Could not resolve DashboardViewModel from DI");
                }
            }

            if (_viewModel is null)
            {
                App.LogError("DashboardPage.OnAppearing", "ERROR: ViewModel is null, cannot load data");
                return;
            }

            await _viewModel.LoadDashboardAsync();
            App.LogError("DashboardPage.OnAppearing", "Completed successfully");
        }
        catch (Exception ex)
        {
            App.LogException("DashboardPage.OnAppearing", ex);
        }
    }
}

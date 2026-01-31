using OoplesFinance.TradingApp.Maui.ViewModels;

namespace OoplesFinance.TradingApp.Maui.Views;

public partial class RegisterPage : ContentPage
{
    public RegisterPage(RegisterViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }
}

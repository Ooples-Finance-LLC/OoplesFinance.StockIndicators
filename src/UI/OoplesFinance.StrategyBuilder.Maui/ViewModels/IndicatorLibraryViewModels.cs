using System.Collections.ObjectModel;
using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace OoplesFinance.StrategyBuilder.Maui.ViewModels;

/// <summary>
/// ViewModel for a category of indicators in the library panel.
/// </summary>
public partial class IndicatorCategoryViewModel : ObservableObject
{
    [ObservableProperty]
    private string _categoryName = string.Empty;

    [ObservableProperty]
    private bool _isExpanded = true;

    [ObservableProperty]
    private ObservableCollection<IndicatorItemViewModel> _indicators = new();
}

/// <summary>
/// ViewModel for an individual indicator in the library panel.
/// </summary>
public partial class IndicatorItemViewModel : ObservableObject
{
    [ObservableProperty]
    private string _id = string.Empty;

    [ObservableProperty]
    private string _name = string.Empty;

    [ObservableProperty]
    private string _description = string.Empty;

    [ObservableProperty]
    private string _category = string.Empty;

    [ObservableProperty]
    private string _nodeType = "indicator";

    [ObservableProperty]
    private bool _isVisible = true;

    [ObservableProperty]
    private ICommand? _dragStartCommand;

    public IndicatorItemViewModel()
    {
        DragStartCommand = new RelayCommand<DragStartingEventArgs>(OnDragStart);
    }

    private void OnDragStart(DragStartingEventArgs? args)
    {
        if (args is null) return;

        args.Data.Properties["IndicatorId"] = Id;
        args.Data.Properties["IndicatorName"] = Name;
        args.Data.Properties["NodeType"] = NodeType;
    }
}

/// <summary>
/// ViewModel for the indicator library panel.
/// </summary>
public partial class IndicatorLibraryViewModel : ObservableObject
{
    private readonly IIndicatorLibraryService _libraryService;

    [ObservableProperty]
    private string _searchText = string.Empty;

    [ObservableProperty]
    private ObservableCollection<IndicatorCategoryViewModel> _categories = new();

    public IndicatorLibraryViewModel(IIndicatorLibraryService libraryService)
    {
        _libraryService = libraryService;
    }

    public async Task LoadAsync()
    {
        var categories = await _libraryService.GetCategoriesAsync();
        Categories = new ObservableCollection<IndicatorCategoryViewModel>(categories);
    }

    partial void OnSearchTextChanged(string value)
    {
        FilterIndicators(value);
    }

    private void FilterIndicators(string searchText)
    {
        foreach (var category in Categories)
        {
            var hasVisibleIndicators = false;
            foreach (var indicator in category.Indicators)
            {
                indicator.IsVisible = string.IsNullOrEmpty(searchText) ||
                    indicator.Name.Contains(searchText, StringComparison.OrdinalIgnoreCase) ||
                    indicator.Description.Contains(searchText, StringComparison.OrdinalIgnoreCase);

                if (indicator.IsVisible)
                    hasVisibleIndicators = true;
            }
            category.IsExpanded = hasVisibleIndicators;
        }
    }
}

/// <summary>
/// ViewModel for the node properties panel.
/// </summary>
public partial class NodePropertiesViewModel : ObservableObject
{
    [ObservableProperty]
    private NodeViewModel? _selectedNode;

    public bool HasNode => SelectedNode is not null;

    partial void OnSelectedNodeChanged(NodeViewModel? value)
    {
        OnPropertyChanged(nameof(HasNode));
    }
}

/// <summary>
/// ViewModel for the toolbar.
/// </summary>
public partial class ToolbarViewModel : ObservableObject
{
    [ObservableProperty]
    private string _strategyName = "New Strategy";

    [ObservableProperty]
    private bool _canUndo;

    [ObservableProperty]
    private bool _canRedo;

    [ObservableProperty]
    private bool _isValid;

    public ICommand? NewCommand { get; set; }
    public ICommand? OpenCommand { get; set; }
    public ICommand? SaveCommand { get; set; }
    public ICommand? UndoCommand { get; set; }
    public ICommand? RedoCommand { get; set; }
    public ICommand? ValidateCommand { get; set; }
    public ICommand? GenerateCodeCommand { get; set; }
    public ICommand? BacktestCommand { get; set; }
}

/// <summary>
/// ViewModel for backtest results.
/// </summary>
public partial class BacktestResultsViewModel : ObservableObject, IQueryAttributable
{
    [ObservableProperty]
    private BacktestResult? _result;

    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        if (query.TryGetValue("Result", out var result) && result is BacktestResult backtestResult)
        {
            Result = backtestResult;
        }
    }
}

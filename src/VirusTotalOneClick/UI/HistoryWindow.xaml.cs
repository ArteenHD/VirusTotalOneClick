using System.Windows;
using VirusTotalOneClick.Core;

namespace VirusTotalOneClick.UI;

public partial class HistoryWindow : AppWindow
{
    private readonly HistoryViewModel _viewModel;

    public HistoryWindow(
        IReadOnlyList<UploadHistoryEntry> entries,
        Action<string>? openResultUrl = null,
        Func<string?, bool>? canOpenResultUrl = null,
        Action? clearHistory = null,
        bool keepHistoryEnabled = true)
    {
        _viewModel = new HistoryViewModel(entries, openResultUrl, canOpenResultUrl, clearHistory, keepHistoryEnabled);
        DataContext = _viewModel;
        InitializeComponent();
    }

    public HistoryViewModel ViewModel => _viewModel;

    public int EntryCount => _viewModel.EntryCount;

    public int VisibleEntryCount => _viewModel.VisibleEntryCount;

    public bool HasClearHistoryAction => _viewModel.HasClearHistoryAction;

    public string FirstResultUrlText => _viewModel.VisibleRows.FirstOrDefault()?.ResultUrl ?? string.Empty;

    public string FirstOriginalPathText => _viewModel.VisibleRows.FirstOrDefault()?.OriginalPath ?? string.Empty;

    public void Search(string query) => _viewModel.Search(query);

    public void ClearHistory() => _viewModel.ClearHistory();

    public void OpenFirstResult() => _viewModel.OpenFirstResult();

    private void OnCloseClick(object sender, RoutedEventArgs e) => Close();
}

using System.Collections.ObjectModel;
using VirusTotalOneClick.Core;

namespace VirusTotalOneClick.UI;

public sealed class HistoryViewModel : ObservableObject
{
    private readonly Action<string>? _openResultUrl;
    private readonly Func<string?, bool> _canOpenResultUrl;
    private readonly Action? _clearHistory;
    private readonly List<HistoryRowViewModel> _allRows;
    private string _searchText = string.Empty;
    private HistoryRowViewModel? _selectedRow;

    public HistoryViewModel(
        IReadOnlyList<UploadHistoryEntry> entries,
        Action<string>? openResultUrl = null,
        Func<string?, bool>? canOpenResultUrl = null,
        Action? clearHistory = null,
        bool keepHistoryEnabled = true)
    {
        ArgumentNullException.ThrowIfNull(entries);
        _openResultUrl = openResultUrl;
        _canOpenResultUrl = canOpenResultUrl ?? (url => !string.IsNullOrWhiteSpace(url));
        _clearHistory = clearHistory;
        _allRows = entries
            .OrderByDescending(entry => entry.UploadedAtUtc)
            .Select(entry => new HistoryRowViewModel(entry))
            .ToList();
        VisibleRows = new ObservableCollection<HistoryRowViewModel>(_allRows);
        CaptionText = keepHistoryEnabled ? "Local history is enabled." : "Local history is disabled.";

        ClearHistoryCommand = new RelayCommand(ClearHistory);
        OpenResultCommand = new RelayCommand(OpenSelectedOrFirstResult, CanOpenSelectedOrFirstResult);
    }

    public ObservableCollection<HistoryRowViewModel> VisibleRows { get; }

    public RelayCommand ClearHistoryCommand { get; }

    public RelayCommand OpenResultCommand { get; }

    public bool HasClearHistoryAction => _clearHistory is not null;

    public string CaptionText { get; }

    public string SearchText
    {
        get => _searchText;
        set
        {
            if (SetProperty(ref _searchText, value ?? string.Empty))
            {
                ApplySearch();
            }
        }
    }

    public int EntryCount => _allRows.Count;

    public int VisibleEntryCount => VisibleRows.Count;

    public bool IsEmpty => VisibleRows.Count == 0;

    public HistoryRowViewModel? SelectedRow
    {
        get => _selectedRow;
        set
        {
            if (!ReferenceEquals(_selectedRow, value))
            {
                _selectedRow = value;
                OnPropertyChanged();
                OpenResultCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public void Search(string? query) => SearchText = query ?? string.Empty;

    public void ClearHistory()
    {
        _clearHistory?.Invoke();
        _allRows.Clear();
        VisibleRows.Clear();
        SelectedRow = null;
        OnPropertyChanged(nameof(EntryCount));
        OnPropertyChanged(nameof(VisibleEntryCount));
        OnPropertyChanged(nameof(IsEmpty));
        OpenResultCommand.RaiseCanExecuteChanged();
    }

    public void OpenSelectedOrFirstResult() => OpenResult(GetSelectedOrFirstResultUrl());

    public void OpenFirstResult() => OpenResult(VisibleRows.FirstOrDefault()?.ResultUrl);

    private void OpenResult(string? resultUrl)
    {
        if (_openResultUrl is null || resultUrl is null || !_canOpenResultUrl(resultUrl))
        {
            return;
        }

        try
        {
            _openResultUrl(resultUrl);
        }
        catch (InvalidOperationException)
        {
        }
        catch (System.ComponentModel.Win32Exception)
        {
        }
    }

    private void ApplySearch()
    {
        VisibleRows.Clear();
        foreach (var row in _allRows.Where(row => row.MatchesSearch(_searchText)))
        {
            VisibleRows.Add(row);
        }

        if (SelectedRow is not null && !VisibleRows.Contains(SelectedRow))
        {
            SelectedRow = null;
        }

        OnPropertyChanged(nameof(VisibleEntryCount));
        OnPropertyChanged(nameof(IsEmpty));
        OpenResultCommand.RaiseCanExecuteChanged();
    }

    private bool CanOpenSelectedOrFirstResult() =>
        _openResultUrl is not null && _canOpenResultUrl(GetSelectedOrFirstResultUrl());

    private string? GetSelectedOrFirstResultUrl() =>
        (SelectedRow ?? VisibleRows.FirstOrDefault())?.ResultUrl;
}

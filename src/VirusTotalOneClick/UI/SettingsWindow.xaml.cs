using System.Windows;
using VirusTotalOneClick.Core;

namespace VirusTotalOneClick.UI;

public partial class SettingsWindow : AppWindow
{
    private readonly SettingsViewModel _viewModel;
    private readonly double _openingHeight;
    private bool _suppressPasswordSync;

    public SettingsWindow(
        AppSettings settings,
        Action<AppSettings>? saveSettings = null,
        ISecretStore? secretStore = null,
        Action? clearHistory = null,
        Action<AppSettings>? applyIntegration = null,
        Action? openHistory = null,
        Func<string, CancellationToken, Task<ApiKeyValidationStatus>>? validateApiKey = null,
        ApiKeyValidator? apiKeyValidator = null,
        bool isFirstRun = false)
    {
        _viewModel = new SettingsViewModel(
            settings, saveSettings, secretStore, clearHistory, applyIntegration, openHistory, validateApiKey, apiKeyValidator, isFirstRun);
        DataContext = _viewModel;
        InitializeComponent();
        // The viewmodel loads the saved key before the password box exists, so
        // push it in once here; it shows masked until the reveal button is used.
        SyncPasswordBoxFromViewModel();
        Height = ClampHeightToWorkArea(Height, SystemParameters.WorkArea.Height, MinHeight);
        _openingHeight = Height;
        _viewModel.PropertyChanged += OnViewModelPropertyChanged;
        Loaded += OnLoaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        // When the work-area clamp leaves the page taller than the viewport,
        // startup layout of the prefilled key box can drift the scroll
        // position, so the page is pinned back to the top after layout settles.
        // Text metrics also vary by machine; if the rendered page runs a little
        // taller than the design height, the window grows to fit (still capped
        // by the work area) rather than opening with a needless scrollbar.
        Dispatcher.BeginInvoke(
            System.Windows.Threading.DispatcherPriority.Background,
            new Action(() =>
            {
                GrowToFitPage();
                PageScroller.ScrollToTop();
            }));

        if (_viewModel.IsFirstRun)
        {
            ApiKeyPasswordBox.Focus();
        }
    }

    private void GrowToFitPage()
    {
        // Only at the untouched opening size: a height the caller or user has
        // already changed is theirs, and a shortfall the work-area clamp
        // created cannot be grown away.
        if (Height != _openingHeight || PageScroller.ScrollableHeight <= 0)
        {
            return;
        }

        Height = ClampHeightToWorkArea(
            Height + PageScroller.ScrollableHeight,
            SystemParameters.WorkArea.Height,
            MinHeight);
    }

    public IReadOnlyList<string> SectionNames { get; } = SettingsViewModel.Sections;

    public SettingsViewModel ViewModel => _viewModel;

    public string SettingsStatusText => _viewModel.SettingsStatusText;

    public string ApiKeyStatusText => _viewModel.ApiKeyStatusText;

    public AppSettings GetSettings() => _viewModel.BuildSettings();

    public void SetApiKeyText(string text)
    {
        _viewModel.ApiKeyText = text ?? string.Empty;
        SyncPasswordBoxFromViewModel();
    }

    public Task SaveApiKey() => _viewModel.SaveApiKeyAsync(CancellationToken.None);

    public Task SaveApiKey(CancellationToken cancellationToken) => _viewModel.SaveApiKeyAsync(cancellationToken);

    public Task SaveSettings() => _viewModel.SaveSettingsAsync(CancellationToken.None);

    public Task SaveSettings(CancellationToken cancellationToken) => _viewModel.SaveSettingsAsync(cancellationToken);

    public void ClearHistory() => _viewModel.ClearHistory();

    public void OpenHistory() => _viewModel.OpenHistory();

    private void OnViewModelPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(SettingsViewModel.ApiKeyText))
        {
            SyncPasswordBoxFromViewModel();
        }
    }

    private void SyncPasswordBoxFromViewModel()
    {
        if (ApiKeyPasswordBox is null || ApiKeyPasswordBox.Password == _viewModel.ApiKeyText)
        {
            return;
        }

        // The password box raises PasswordChanged when set from code; the flag
        // stops that echo from looping back into the viewmodel.
        _suppressPasswordSync = true;
        try
        {
            ApiKeyPasswordBox.Password = _viewModel.ApiKeyText;
        }
        finally
        {
            _suppressPasswordSync = false;
        }
    }

    private void ApiKeyPasswordBox_PasswordChanged(object sender, RoutedEventArgs e)
    {
        if (_suppressPasswordSync || sender is not Wpf.Ui.Controls.PasswordBox passwordBox)
        {
            return;
        }

        _viewModel.ApiKeyText = passwordBox.Password;
    }
}

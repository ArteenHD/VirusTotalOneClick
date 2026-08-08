using System.Windows;
using VirusTotalOneClick.Core;

namespace VirusTotalOneClick.UI;

public partial class SettingsWindow : AppWindow
{
    private readonly SettingsViewModel _viewModel;
    private bool _suppressPasswordSync;

    public SettingsWindow(
        AppSettings settings,
        Action<AppSettings>? saveSettings = null,
        ISecretStore? secretStore = null,
        Action? clearHistory = null,
        Action<AppSettings>? applyIntegration = null,
        Action? openHistory = null,
        Func<string, CancellationToken, Task<ApiKeyValidationStatus>>? validateApiKey = null,
        ApiKeyValidator? apiKeyValidator = null)
    {
        _viewModel = new SettingsViewModel(
            settings, saveSettings, secretStore, clearHistory, applyIntegration, openHistory, validateApiKey, apiKeyValidator);
        DataContext = _viewModel;
        InitializeComponent();
        // The viewmodel loads the saved key before the password box exists, so
        // push it in once here; it shows masked until the reveal button is used.
        SyncPasswordBoxFromViewModel();
        Height = ClampHeightToWorkArea(Height, SystemParameters.WorkArea.Height, MinHeight);
        _viewModel.PropertyChanged += OnViewModelPropertyChanged;
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

    public void SaveSettings() => _viewModel.SaveSettings();

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

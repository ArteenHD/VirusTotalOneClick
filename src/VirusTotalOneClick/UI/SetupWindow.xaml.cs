using System.Windows;
using VirusTotalOneClick.Core;

namespace VirusTotalOneClick.UI;

public partial class SetupWindow : AppWindow
{
    private readonly SetupViewModel _viewModel;
    private bool _suppressPasswordSync;

    public SetupWindow(
        AppSettings settings,
        Action<AppSettings>? saveSettings = null,
        ISecretStore? secretStore = null,
        Action<AppSettings>? applyIntegration = null,
        Func<string, CancellationToken, Task<ApiKeyValidationStatus>>? validateApiKey = null,
        ApiKeyValidator? apiKeyValidator = null)
    {
        _viewModel = new SetupViewModel(
            settings, saveSettings, secretStore, applyIntegration, validateApiKey, apiKeyValidator);
        DataContext = _viewModel;
        InitializeComponent();
        _viewModel.PropertyChanged += OnViewModelPropertyChanged;
    }

    public IReadOnlyList<string> SectionNames { get; } = SetupViewModel.Sections;

    public SetupViewModel ViewModel => _viewModel;

    public string SetupStatusText => _viewModel.SetupStatusText;

    public string ApiKeyStatusText => _viewModel.ApiKeyStatusText;

    public void SetApiKeyText(string text)
    {
        _viewModel.ApiKeyText = text ?? string.Empty;
        SyncPasswordBoxFromViewModel();
    }

    public Task SaveSetup() => _viewModel.SaveSetupAsync(CancellationToken.None);

    public Task SaveSetup(CancellationToken cancellationToken) => _viewModel.SaveSetupAsync(cancellationToken);

    private void OnViewModelPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(SetupViewModel.ApiKeyText))
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

    private void SaveOrCloseButton_Click(object sender, RoutedEventArgs e)
    {
        if (_viewModel.IsSetupSaved)
        {
            Close();
            return;
        }

        _viewModel.SaveSetupCommand.Execute(null);
    }
}

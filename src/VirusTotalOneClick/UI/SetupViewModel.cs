using System.Collections.ObjectModel;
using VirusTotalOneClick.Core;

namespace VirusTotalOneClick.UI;

public sealed class SetupViewModel : ObservableObject
{
    public static IReadOnlyList<string> Sections { get; } = new ReadOnlyCollection<string>(
    [
        "API Key",
        "Upload Behavior",
        "Folders",
        "History",
        "Anonymizer",
        "Explorer Integration",
    ]);

    private readonly Action<AppSettings>? _saveSettings;
    private readonly ISecretStore? _secretStore;
    private readonly Action<AppSettings>? _applyIntegration;
    private readonly Func<string, CancellationToken, Task<ApiKeyValidationStatus>>? _validateApiKey;
    private readonly ApiKeyValidator _apiKeyValidator;

    private string _apiKeyText = string.Empty;
    private bool _isSetupSaved;
    private string _setupStatusText = "Setup not saved.";
    private string _apiKeyStatusText = "Stored with Windows current-user protection.";

    private bool _confirmBeforeFileUpload;
    private FolderUploadMode _defaultFolderMode;
    private bool _keepLocalHistory;
    private bool _scrubSubmittedFilename;
    private bool _scrubZipInternalPaths;
    private bool _enableClassicContextMenu;
    private bool _enableSendToShortcut;
    private bool _enableStartMenuShortcuts;
    private bool _launchOnStartup;

    public SetupViewModel(
        AppSettings settings,
        Action<AppSettings>? saveSettings = null,
        ISecretStore? secretStore = null,
        Action<AppSettings>? applyIntegration = null,
        Func<string, CancellationToken, Task<ApiKeyValidationStatus>>? validateApiKey = null,
        ApiKeyValidator? apiKeyValidator = null)
    {
        ArgumentNullException.ThrowIfNull(settings);
        _saveSettings = saveSettings;
        _secretStore = secretStore;
        _applyIntegration = applyIntegration;
        _validateApiKey = validateApiKey;
        _apiKeyValidator = apiKeyValidator ?? new ApiKeyValidator(allowTestKey: false);

        _confirmBeforeFileUpload = settings.ConfirmBeforeFileUpload;
        _defaultFolderMode = settings.DefaultFolderMode;
        _keepLocalHistory = settings.KeepLocalHistory;
        _scrubSubmittedFilename = settings.ScrubSubmittedFilename;
        _scrubZipInternalPaths = settings.ScrubZipInternalPaths;
        _enableClassicContextMenu = settings.EnableClassicContextMenu;
        _enableSendToShortcut = settings.EnableSendToShortcut;
        _enableStartMenuShortcuts = settings.EnableStartMenuShortcuts;
        _launchOnStartup = settings.LaunchOnStartup;

        SaveSetupCommand = new AsyncRelayCommand(
            () => SaveSetupAsync(CancellationToken.None),
            onError: ex => SetupStatusText = $"Setup failed: {ex.GetBaseException().Message}");
    }

    public AsyncRelayCommand SaveSetupCommand { get; }

    public string ApiKeyText
    {
        get => _apiKeyText;
        set => SetProperty(ref _apiKeyText, value ?? string.Empty);
    }

    public bool IsSetupSaved
    {
        get => _isSetupSaved;
        private set => SetProperty(ref _isSetupSaved, value);
    }

    public string SetupStatusText
    {
        get => _setupStatusText;
        private set => SetProperty(ref _setupStatusText, value);
    }

    public string ApiKeyStatusText
    {
        get => _apiKeyStatusText;
        private set => SetProperty(ref _apiKeyStatusText, value);
    }

    public bool ConfirmBeforeFileUpload
    {
        get => _confirmBeforeFileUpload;
        set
        {
            if (SetProperty(ref _confirmBeforeFileUpload, value))
            {
                OnPropertyChanged(nameof(UploadImmediately));
            }
        }
    }

    public bool UploadImmediately
    {
        get => !_confirmBeforeFileUpload;
        set => ConfirmBeforeFileUpload = !value;
    }

    public FolderUploadMode DefaultFolderMode
    {
        get => _defaultFolderMode;
        set
        {
            if (SetProperty(ref _defaultFolderMode, value))
            {
                OnPropertyChanged(nameof(ReviewSelectedFiles));
                OnPropertyChanged(nameof(ReviewThenZip));
            }
        }
    }

    public bool ReviewSelectedFiles
    {
        get => _defaultFolderMode == FolderUploadMode.ReviewSelectedFiles;
        set
        {
            if (value)
            {
                DefaultFolderMode = FolderUploadMode.ReviewSelectedFiles;
            }
        }
    }

    public bool ReviewThenZip
    {
        get => _defaultFolderMode == FolderUploadMode.ReviewThenZip;
        set
        {
            if (value)
            {
                DefaultFolderMode = FolderUploadMode.ReviewThenZip;
            }
        }
    }

    public bool KeepLocalHistory
    {
        get => _keepLocalHistory;
        set => SetProperty(ref _keepLocalHistory, value);
    }

    public bool ScrubSubmittedFilename
    {
        get => _scrubSubmittedFilename;
        set => SetProperty(ref _scrubSubmittedFilename, value);
    }

    public bool ScrubZipInternalPaths
    {
        get => _scrubZipInternalPaths;
        set => SetProperty(ref _scrubZipInternalPaths, value);
    }

    public bool EnableClassicContextMenu
    {
        get => _enableClassicContextMenu;
        set => SetProperty(ref _enableClassicContextMenu, value);
    }

    public bool EnableSendToShortcut
    {
        get => _enableSendToShortcut;
        set => SetProperty(ref _enableSendToShortcut, value);
    }

    public bool EnableStartMenuShortcuts
    {
        get => _enableStartMenuShortcuts;
        set => SetProperty(ref _enableStartMenuShortcuts, value);
    }

    public bool LaunchOnStartup
    {
        get => _launchOnStartup;
        set => SetProperty(ref _launchOnStartup, value);
    }

    public AppSettings BuildSettings() => new()
    {
        ConfirmBeforeFileUpload = ConfirmBeforeFileUpload,
        DefaultFolderMode = DefaultFolderMode,
        KeepLocalHistory = KeepLocalHistory,
        ScrubSubmittedFilename = ScrubSubmittedFilename,
        ScrubZipInternalPaths = ScrubZipInternalPaths,
        EnableClassicContextMenu = EnableClassicContextMenu,
        EnableSendToShortcut = EnableSendToShortcut,
        EnableStartMenuShortcuts = EnableStartMenuShortcuts,
        LaunchOnStartup = LaunchOnStartup,
    };

    public async Task SaveSetupAsync(CancellationToken cancellationToken)
    {
        var key = ApiKeyText.Trim();

        if (key.Length > 0)
        {
            var outcome = await ApiKeySaveFlow.SaveAsync(
                key,
                _secretStore,
                _apiKeyValidator,
                _validateApiKey,
                onChecking: status =>
                {
                    ApiKeyStatusText = status;
                    return Task.CompletedTask;
                },
                cancellationToken).ConfigureAwait(true);

            ApiKeyStatusText = outcome.StatusText;
            if (!outcome.Saved)
            {
                SetupStatusText = outcome.StatusText;
                return;
            }
        }
        else if (_secretStore is not null)
        {
            ApiKeyStatusText = "No API key saved.";
        }

        var settings = BuildSettings();
        try
        {
            _saveSettings?.Invoke(settings);
        }
        catch (Exception ex)
        {
            SetupStatusText = $"Could not save settings: {ex.GetBaseException().Message}";
            return;
        }

        try
        {
            _applyIntegration?.Invoke(settings);
            SetupStatusText = "Setup saved.";
            IsSetupSaved = true;
        }
        catch (Exception ex)
        {
            SetupStatusText = $"Setup saved, but Windows integration failed: {ex.GetBaseException().Message}";
            IsSetupSaved = true;
        }
    }
}

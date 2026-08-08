using System.Collections.ObjectModel;
using System.IO;
using VirusTotalOneClick.Core;

namespace VirusTotalOneClick.UI;

public sealed class SettingsViewModel : ObservableObject
{
    public static IReadOnlyList<string> Sections { get; } = new ReadOnlyCollection<string>(
    [
        "Account",
        "Uploads",
        "Folders",
        "Anonymizer",
        "History",
        "Integration",
    ]);

    private readonly Action<AppSettings>? _saveSettings;
    private readonly ISecretStore? _secretStore;
    private readonly Action? _clearHistory;
    private readonly Action<AppSettings>? _applyIntegration;
    private readonly Action? _openHistory;
    private readonly Func<string, CancellationToken, Task<ApiKeyValidationStatus>>? _validateApiKey;
    private readonly ApiKeyValidator _apiKeyValidator;

    private string _apiKeyText = string.Empty;
    private string _committedApiKey = string.Empty;
    private string _settingsStatusText = "Settings not saved in this session.";
    private string _apiKeyStatusText;
    private string _historyStatusText;

    private bool _confirmBeforeFileUpload;
    private FolderUploadMode _defaultFolderMode;
    private bool _keepLocalHistory;
    private bool _scrubSubmittedFilename;
    private bool _scrubZipInternalPaths;
    private bool _enableClassicContextMenu;
    private bool _enableSendToShortcut;
    private bool _enableStartMenuShortcuts;
    private bool _launchOnStartup;

    private bool _isRaisingSaveGuards;

    public SettingsViewModel(
        AppSettings settings,
        Action<AppSettings>? saveSettings = null,
        ISecretStore? secretStore = null,
        Action? clearHistory = null,
        Action<AppSettings>? applyIntegration = null,
        Action? openHistory = null,
        Func<string, CancellationToken, Task<ApiKeyValidationStatus>>? validateApiKey = null,
        ApiKeyValidator? apiKeyValidator = null)
    {
        ArgumentNullException.ThrowIfNull(settings);
        _saveSettings = saveSettings;
        _secretStore = secretStore;
        _clearHistory = clearHistory;
        _applyIntegration = applyIntegration;
        _openHistory = openHistory;
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

        _apiKeyText = _secretStore?.ReadApiKey() ?? string.Empty;
        _committedApiKey = _apiKeyText;
        _apiKeyStatusText = _apiKeyText.Length > 0 ? "API key saved." : "No API key saved.";
        _historyStatusText = settings.KeepLocalHistory
            ? "Local history is enabled."
            : "Local history is disabled.";

        SaveSettingsCommand = new AsyncRelayCommand(
            () => SaveSettingsAsync(CancellationToken.None),
            canExecute: () => !AnySaveIsRunning,
            onError: ex => SettingsStatusText = $"Save failed: {ex.GetBaseException().Message}");
        SaveApiKeyCommand = new AsyncRelayCommand(
            () => SaveApiKeyAsync(CancellationToken.None),
            canExecute: () => !AnySaveIsRunning,
            onError: ex => ApiKeyStatusText = $"Could not save API key: {ex.GetBaseException().Message}");
        ClearApiKeyCommand = new RelayCommand(ClearApiKey, () => !AnySaveIsRunning);
        ClearHistoryCommand = new RelayCommand(ClearHistory);
        OpenHistoryCommand = new RelayCommand(OpenHistory, () => _openHistory is not null);

        // A key save runs a 30-second live probe; clearing the key mid-probe
        // must not let that probe's own save land afterward and silently
        // restore what was just cleared, so Clear is disabled for the probe's
        // duration. "Save settings" can run the same probe for a key pending
        // in the box, so the two saving commands also exclude each other —
        // two concurrent probes of one key could disagree and split the
        // stored key from the reported outcome.
        SaveApiKeyCommand.CanExecuteChanged += (_, _) => RaiseSaveGuards();
        SaveSettingsCommand.CanExecuteChanged += (_, _) => RaiseSaveGuards();
    }

    public AsyncRelayCommand SaveSettingsCommand { get; }

    public AsyncRelayCommand SaveApiKeyCommand { get; }

    public RelayCommand ClearApiKeyCommand { get; }

    public RelayCommand ClearHistoryCommand { get; }

    public RelayCommand OpenHistoryCommand { get; }

    public string VersionCaption { get; } = $"Version {AppVersionInfo.GetInformationalVersion()}";

    public string ApiKeyText
    {
        get => _apiKeyText;
        set => SetProperty(ref _apiKeyText, value ?? string.Empty);
    }

    public string SettingsStatusText
    {
        get => _settingsStatusText;
        private set => SetProperty(ref _settingsStatusText, value);
    }

    public string ApiKeyStatusText
    {
        get => _apiKeyStatusText;
        private set => SetProperty(ref _apiKeyStatusText, value);
    }

    public string HistoryStatusText
    {
        get => _historyStatusText;
        private set => SetProperty(ref _historyStatusText, value);
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
        set
        {
            if (SetProperty(ref _keepLocalHistory, value))
            {
                HistoryStatusText = value ? "Local history is enabled." : "Local history is disabled.";
            }
        }
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

    public async Task SaveSettingsAsync(CancellationToken cancellationToken)
    {
        var key = ApiKeyText.Trim();
        if (key.Length > 0 && key != _committedApiKey)
        {
            // A key sitting unsaved in the box is committed as part of this
            // save, so pasting a key and clicking the one big button never
            // silently discards it. A failed key aborts everything below.
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
                SettingsStatusText = outcome.StatusText;
                return;
            }

            _committedApiKey = key;
        }
        else if (key.Length == 0 && _committedApiKey.Length == 0 && _secretStore is not null)
        {
            ApiKeyStatusText = "No API key saved.";
        }

        // An emptied box over a stored key changes nothing here; the Clear
        // button is the only path that deletes a stored key.

        var settings = BuildSettings();
        try
        {
            _saveSettings?.Invoke(settings);
        }
        catch (Exception ex)
        {
            SettingsStatusText = $"Could not save settings: {ex.GetBaseException().Message}";
            return;
        }

        try
        {
            _applyIntegration?.Invoke(settings);
            SettingsStatusText = "Settings saved.";
        }
        catch (Exception ex)
        {
            SettingsStatusText = $"Settings saved, but Windows integration failed: {ex.GetBaseException().Message}";
        }
    }

    public async Task SaveApiKeyAsync(CancellationToken cancellationToken)
    {
        var outcome = await ApiKeySaveFlow.SaveAsync(
            ApiKeyText,
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
        if (outcome.Saved)
        {
            _committedApiKey = ApiKeyText.Trim();
        }
    }

    public void ClearApiKey()
    {
        try
        {
            _secretStore?.DeleteApiKey();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            ApiKeyStatusText = $"Could not clear API key: {ex.GetBaseException().Message}";
            return;
        }

        _committedApiKey = string.Empty;
        ApiKeyText = string.Empty;
        ApiKeyStatusText = "API key cleared.";
    }

    public void ClearHistory()
    {
        try
        {
            _clearHistory?.Invoke();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            HistoryStatusText = $"Could not clear history: {ex.GetBaseException().Message}";
            return;
        }

        HistoryStatusText = "Local history cleared.";
    }

    public void OpenHistory() => _openHistory?.Invoke();

    private bool AnySaveIsRunning => SaveApiKeyCommand.IsRunning || SaveSettingsCommand.IsRunning;

    // Each save command's state change must re-query the other saving command
    // and Clear. Raising an event re-enters this handler through the other
    // command's subscription, so the flag bounds the cascade to one pass.
    private void RaiseSaveGuards()
    {
        if (_isRaisingSaveGuards)
        {
            return;
        }

        _isRaisingSaveGuards = true;
        try
        {
            ClearApiKeyCommand.RaiseCanExecuteChanged();
            SaveApiKeyCommand.RaiseCanExecuteChanged();
            SaveSettingsCommand.RaiseCanExecuteChanged();
        }
        finally
        {
            _isRaisingSaveGuards = false;
        }
    }
}

using System.Reflection;
using System.Windows;
using VirusTotalOneClick.Core;
using VirusTotalOneClick.UI;

namespace VirusTotalOneClick.AppTests;

public class SettingsWindowTests
{
    private static readonly string ValidKey = new('c', 64);

    [Fact]
    public void TheWindowCanBeResizedWithASensibleMinimumSize()
    {
        StaTestRunner.Run(() =>
        {
            var window = new SettingsWindow(AppSettings.Default);
            using var closer = new WindowCloser(window);

            Assert.Equal(ResizeMode.CanResize, window.ResizeMode);
            Assert.True(window.MinWidth > 0);
            Assert.True(window.MinHeight > 0);
        });
    }

    [Fact]
    public void AWorkAreaShorterThanTheDesignHeightClampsStartupHeightWithAMargin()
    {
        Assert.Equal(676d, AppWindow.ClampHeightToWorkArea(designHeight: 780, workAreaHeight: 700, minHeight: 480));
        Assert.Equal(780d, AppWindow.ClampHeightToWorkArea(designHeight: 780, workAreaHeight: 2000, minHeight: 480));
        Assert.Equal(780d, AppWindow.ClampHeightToWorkArea(designHeight: 780, workAreaHeight: 0, minHeight: 480));
    }

    [Fact]
    public void TheClampNeverUndercutsTheWindowsMinimumHeight()
    {
        Assert.Equal(480d, AppWindow.ClampHeightToWorkArea(designHeight: 780, workAreaHeight: 490, minHeight: 480));
    }

    [Fact]
    public void TheWindowHeightIsWiredToTheWorkAreaClamp()
    {
        StaTestRunner.Run(() =>
        {
            var window = new SettingsWindow(AppSettings.Default);
            using var closer = new WindowCloser(window);

            Assert.Equal(
                AppWindow.ClampHeightToWorkArea(830, SystemParameters.WorkArea.Height, window.MinHeight),
                window.Height);
        });
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void TheDefaultHeightShowsTheWholePageWithoutAScrollbar(bool isFirstRun)
    {
        StaTestRunner.Run(() =>
        {
            var store = new FakeSecretStore();
            store.SaveApiKey(ValidKey);
            var window = new SettingsWindow(
                AppSettings.Default,
                secretStore: store,
                isFirstRun: isFirstRun);
            using var closer = new WindowCloser(window);

            // The work-area clamp may shrink the window on a small display; this
            // pins the design itself: at full design height the page must fit
            // with nothing left to scroll.
            window.Height = 830;
            window.Show();
            window.Dispatcher.Invoke(() => { }, System.Windows.Threading.DispatcherPriority.ContextIdle);

            Assert.Equal(0d, window.PageScroller.ScrollableHeight);
        });
    }

    [Fact]
    public void TheSaveSettingsButtonIsTheDefaultButton()
    {
        StaTestRunner.Run(() =>
        {
            var window = new SettingsWindow(AppSettings.Default);
            using var closer = new WindowCloser(window);

            Assert.True(window.SaveSettingsButton.IsDefault);
        });
    }

    [Fact]
    public void ConstructsWithTheFixedSectionOrder()
    {
        StaTestRunner.Run(() =>
        {
            var window = new SettingsWindow(AppSettings.Default);
            using var closer = new WindowCloser(window);

            Assert.Equal("VirusTotal OneClick", window.Title);
            Assert.Equal(
                ["Account", "Uploads", "Folders", "Anonymizer", "History", "Integration"],
                window.SectionNames);
        });
    }

    [Fact]
    public void ControlStateRoundTripsThroughGetSettings()
    {
        StaTestRunner.Run(() =>
        {
            var window = new SettingsWindow(AppSettings.Default);
            using var closer = new WindowCloser(window);

            window.ViewModel.ConfirmBeforeFileUpload = true;
            window.ViewModel.ScrubSubmittedFilename = true;
            window.ViewModel.EnableSendToShortcut = false;
            window.ViewModel.EnableStartMenuShortcuts = false;
            window.ViewModel.LaunchOnStartup = true;

            var settings = window.GetSettings();

            Assert.True(settings.ConfirmBeforeFileUpload);
            Assert.True(settings.ScrubSubmittedFilename);
            Assert.False(settings.EnableSendToShortcut);
            Assert.False(settings.EnableStartMenuShortcuts);
            Assert.True(settings.LaunchOnStartup);
        });
    }

    [Fact]
    public void RadioTogglesRoundTripThroughBuildSettings()
    {
        StaTestRunner.Run(() =>
        {
            var window = new SettingsWindow(AppSettings.Default);
            using var closer = new WindowCloser(window);

            window.ViewModel.ConfirmBeforeFileUpload = true;
            window.ViewModel.ReviewThenZip = true;
            window.ViewModel.KeepLocalHistory = false;
            window.ViewModel.LaunchOnStartup = true;

            var settings = window.ViewModel.BuildSettings();

            Assert.True(settings.ConfirmBeforeFileUpload);
            Assert.Equal(FolderUploadMode.ReviewThenZip, settings.DefaultFolderMode);
            Assert.False(settings.KeepLocalHistory);
            Assert.True(settings.LaunchOnStartup);
        });
    }

    [Fact]
    public void SaveSettingsInvokesTheSaveActionAndShowsSavedStatus()
    {
        StaTestRunner.Run(() =>
        {
            AppSettings? saved = null;
            var window = new SettingsWindow(AppSettings.Default, settings => saved = settings);
            using var closer = new WindowCloser(window);

            window.ViewModel.ScrubSubmittedFilename = true;
            window.SaveSettings().GetAwaiter().GetResult();

            Assert.True(saved!.ScrubSubmittedFilename);
            Assert.Equal("Settings saved.", window.SettingsStatusText);
        });
    }

    [Fact]
    public void IntegrationFailureOnSaveIsAppendedAndRendered()
    {
        StaTestRunner.Run(() =>
        {
            var window = new SettingsWindow(
                AppSettings.Default,
                saveSettings: _ => { },
                applyIntegration: _ => throw new InvalidOperationException("registry denied"));
            using var closer = new WindowCloser(window);

            window.SaveSettings().GetAwaiter().GetResult();

            Assert.StartsWith("Settings saved", window.SettingsStatusText, StringComparison.Ordinal);
            Assert.Contains("Windows integration failed", window.SettingsStatusText, StringComparison.Ordinal);
            Assert.Contains("registry denied", window.SettingsStatusText, StringComparison.Ordinal);
            VisualTreeAssert.AssertWindowContainsText(window, "registry denied");
        });
    }

    [Fact]
    public void ASaveFailureShortCircuitsBeforeIntegration()
    {
        StaTestRunner.Run(() =>
        {
            var integrationRan = false;
            var window = new SettingsWindow(
                AppSettings.Default,
                saveSettings: _ => throw new IOException("disk full"),
                applyIntegration: _ => integrationRan = true);
            using var closer = new WindowCloser(window);

            window.SaveSettings().GetAwaiter().GetResult();

            Assert.False(integrationRan);
            Assert.Equal("Could not save settings: disk full", window.SettingsStatusText);
        });
    }

    [Fact]
    public void TheSavedApiKeyNeverAppearsInTheSavedSettings()
    {
        StaTestRunner.Run(() =>
        {
            var store = new FakeSecretStore();
            AppSettings? saved = null;
            var window = new SettingsWindow(AppSettings.Default, settings => saved = settings, store);
            using var closer = new WindowCloser(window);

            window.SetApiKeyText(ValidKey);
            window.SaveApiKey().GetAwaiter().GetResult();
            window.SaveSettings().GetAwaiter().GetResult();

            Assert.Equal(ValidKey, store.ReadApiKey());
            Assert.DoesNotContain(ValidKey, saved!.ToString(), StringComparison.Ordinal);
            Assert.Equal("API key saved.", window.ApiKeyStatusText);
        });
    }

    [Fact]
    public void SaveSettingsCommitsAnUnsavedKeyBeforeSettingsAndIntegration()
    {
        StaTestRunner.Run(() =>
        {
            var order = new List<string>();
            var store = new FakeSecretStore();
            var window = new SettingsWindow(
                AppSettings.Default,
                saveSettings: _ => order.Add("settings"),
                secretStore: store,
                applyIntegration: _ => order.Add("integration"),
                validateApiKey: (_, _) =>
                {
                    order.Add("probe");
                    return Task.FromResult(ApiKeyValidationStatus.Valid);
                });
            using var closer = new WindowCloser(window);

            window.SetApiKeyText(ValidKey);
            window.SaveSettings().GetAwaiter().GetResult();

            Assert.Equal(ValidKey, store.ReadApiKey());
            Assert.Equal(["probe", "settings", "integration"], order);
            Assert.Equal("API key saved.", window.ApiKeyStatusText);
            Assert.Equal("Settings saved.", window.SettingsStatusText);
        });
    }

    [Fact]
    public void ABadKeyInTheBoxAbortsTheWholeSettingsSave()
    {
        StaTestRunner.Run(() =>
        {
            var savedCount = 0;
            var integrationRan = false;
            var window = new SettingsWindow(
                AppSettings.Default,
                saveSettings: _ => savedCount++,
                secretStore: new FakeSecretStore(),
                applyIntegration: _ => integrationRan = true);
            using var closer = new WindowCloser(window);

            window.SetApiKeyText("garbage");
            window.SaveSettings().GetAwaiter().GetResult();

            Assert.Equal(0, savedCount);
            Assert.False(integrationRan);
            Assert.Equal("API key must be 64 hex characters.", window.ApiKeyStatusText);
            Assert.Equal("API key must be 64 hex characters.", window.SettingsStatusText);
        });
    }

    [Fact]
    public void ARejectedKeyInTheBoxAbortsTheWholeSettingsSave()
    {
        StaTestRunner.Run(() =>
        {
            var savedCount = 0;
            var store = new FakeSecretStore();
            var window = new SettingsWindow(
                AppSettings.Default,
                saveSettings: _ => savedCount++,
                secretStore: store,
                validateApiKey: (_, _) => Task.FromResult(ApiKeyValidationStatus.Rejected));
            using var closer = new WindowCloser(window);

            window.SetApiKeyText(ValidKey);
            window.SaveSettings().GetAwaiter().GetResult();

            Assert.Null(store.ReadApiKey());
            Assert.Equal(0, savedCount);
            Assert.Contains("rejected", window.ApiKeyStatusText, StringComparison.OrdinalIgnoreCase);
        });
    }

    [Fact]
    public void ANetworkErrorDuringTheSettingsSaveKeepsBothSaves()
    {
        StaTestRunner.Run(() =>
        {
            AppSettings? saved = null;
            var store = new FakeSecretStore();
            var window = new SettingsWindow(
                AppSettings.Default,
                saveSettings: s => saved = s,
                secretStore: store,
                validateApiKey: (_, _) => Task.FromResult(ApiKeyValidationStatus.NetworkError));
            using var closer = new WindowCloser(window);

            window.SetApiKeyText(ValidKey);
            window.SaveSettings().GetAwaiter().GetResult();

            Assert.Equal(ValidKey, store.ReadApiKey());
            Assert.NotNull(saved);
            Assert.Contains("will be verified on first upload", window.ApiKeyStatusText, StringComparison.Ordinal);
            Assert.Equal("Settings saved.", window.SettingsStatusText);
        });
    }

    [Fact]
    public void AnUnchangedPreloadedKeyIsNotReprobedBySaveSettings()
    {
        StaTestRunner.Run(() =>
        {
            var probes = 0;
            var store = new FakeSecretStore();
            store.SaveApiKey(ValidKey);
            AppSettings? saved = null;
            var window = new SettingsWindow(
                AppSettings.Default,
                saveSettings: s => saved = s,
                secretStore: store,
                validateApiKey: (_, _) => { probes++; return Task.FromResult(ApiKeyValidationStatus.Valid); });
            using var closer = new WindowCloser(window);

            window.SaveSettings().GetAwaiter().GetResult();

            Assert.Equal(0, probes);
            Assert.NotNull(saved);
            Assert.Equal(ValidKey, store.ReadApiKey());
            Assert.Equal("Settings saved.", window.SettingsStatusText);
        });
    }

    [Fact]
    public void AnEmptiedKeyBoxDoesNotDeleteTheStoredKeyOnSaveSettings()
    {
        StaTestRunner.Run(() =>
        {
            var store = new FakeSecretStore();
            store.SaveApiKey(ValidKey);
            AppSettings? saved = null;
            var window = new SettingsWindow(
                AppSettings.Default,
                saveSettings: s => saved = s,
                secretStore: store);
            using var closer = new WindowCloser(window);

            window.SetApiKeyText(string.Empty);
            window.SaveSettings().GetAwaiter().GetResult();

            Assert.Equal(ValidKey, store.ReadApiKey());
            Assert.NotNull(saved);
            Assert.Equal("Settings saved.", window.SettingsStatusText);
        });
    }

    [Fact]
    public void AKeyChangedAfterAStandaloneKeySaveIsCommittedAgainBySaveSettings()
    {
        StaTestRunner.Run(() =>
        {
            // Must differ from ValidKey's fill character or this test proves nothing.
            var otherKey = new string('d', 64);
            var store = new FakeSecretStore();
            var window = new SettingsWindow(
                AppSettings.Default,
                saveSettings: _ => { },
                secretStore: store);
            using var closer = new WindowCloser(window);

            window.SetApiKeyText(ValidKey);
            window.SaveApiKey().GetAwaiter().GetResult();
            Assert.Equal(ValidKey, store.ReadApiKey());

            window.SetApiKeyText(otherKey);
            window.SaveSettings().GetAwaiter().GetResult();

            Assert.Equal(otherKey, store.ReadApiKey());
        });
    }

    [Fact]
    public void AKeyEditedWhileItsCheckRunsIsStillCommittedBySaveSettings()
    {
        StaTestRunner.Run(() =>
        {
            // Plain TaskCompletionSource: resolving it below runs the rest of
            // the key save inline on this thread, so the edit lands strictly
            // between the probe starting and the store write.
            var editedKey = new string('e', 64);
            var probes = 0;
            var validationStarted = new TaskCompletionSource();
            var allowValidation = new TaskCompletionSource<ApiKeyValidationStatus>();
            var store = new FakeSecretStore();
            var window = new SettingsWindow(
                AppSettings.Default,
                saveSettings: _ => { },
                secretStore: store,
                validateApiKey: (_, _) =>
                {
                    if (++probes > 1)
                    {
                        return Task.FromResult(ApiKeyValidationStatus.Valid);
                    }

                    validationStarted.SetResult();
                    return allowValidation.Task;
                });
            using var closer = new WindowCloser(window);

            window.SetApiKeyText(ValidKey);
            window.ViewModel.SaveApiKeyCommand.Execute(null);
            validationStarted.Task.GetAwaiter().GetResult();

            window.SetApiKeyText(editedKey);
            allowValidation.SetResult(ApiKeyValidationStatus.Valid);

            // The finished save stored the key it actually checked.
            Assert.Equal(ValidKey, store.ReadApiKey());

            window.SaveSettings().GetAwaiter().GetResult();

            Assert.Equal(editedKey, store.ReadApiKey());
            Assert.Equal("Settings saved.", window.SettingsStatusText);
        });
    }

    [Fact]
    public void WhitespaceAroundAnUnsavedKeyIsTrimmedBySaveSettings()
    {
        StaTestRunner.Run(() =>
        {
            string? probed = null;
            var store = new FakeSecretStore();
            var window = new SettingsWindow(
                AppSettings.Default,
                saveSettings: _ => { },
                secretStore: store,
                validateApiKey: (key, _) => { probed = key; return Task.FromResult(ApiKeyValidationStatus.Valid); });
            using var closer = new WindowCloser(window);

            window.SetApiKeyText("  " + ValidKey + "\n");
            window.SaveSettings().GetAwaiter().GetResult();

            Assert.Equal(ValidKey, probed);
            Assert.Equal(ValidKey, store.ReadApiKey());
        });
    }

    [Fact]
    public void ABadKeyOverAStoredKeyBlocksTheSaveAndKeepsTheStoredKey()
    {
        StaTestRunner.Run(() =>
        {
            var savedCount = 0;
            var store = new FakeSecretStore();
            store.SaveApiKey(ValidKey);
            var window = new SettingsWindow(
                AppSettings.Default,
                saveSettings: _ => savedCount++,
                secretStore: store);
            using var closer = new WindowCloser(window);

            window.SetApiKeyText("nudged");
            window.SaveSettings().GetAwaiter().GetResult();

            Assert.Equal(0, savedCount);
            Assert.Equal(ValidKey, store.ReadApiKey());
            Assert.Equal("API key must be 64 hex characters.", window.ApiKeyStatusText);
            Assert.Equal("API key must be 64 hex characters.", window.SettingsStatusText);
        });
    }

    [Theory]
    [InlineData("short")]
    [InlineData("zzzz")]
    public void BadlyFormattedKeysAreRejectedWithoutProbeOrSave(string badKey)
    {
        StaTestRunner.Run(() =>
        {
            var probes = 0;
            var store = new FakeSecretStore();
            var window = new SettingsWindow(
                AppSettings.Default,
                secretStore: store,
                validateApiKey: (_, _) => { probes++; return Task.FromResult(ApiKeyValidationStatus.Valid); });
            using var closer = new WindowCloser(window);

            window.SetApiKeyText(badKey);
            window.SaveApiKey().GetAwaiter().GetResult();

            Assert.Equal(0, probes);
            Assert.Null(store.ReadApiKey());
            Assert.Equal("API key must be 64 hex characters.", window.ApiKeyStatusText);
        });
    }

    [Fact]
    public void AValidKeyProbesExactlyOnceBeforeSaving()
    {
        StaTestRunner.Run(() =>
        {
            var probes = 0;
            var store = new FakeSecretStore();
            var window = new SettingsWindow(
                AppSettings.Default,
                secretStore: store,
                validateApiKey: (_, _) => { probes++; return Task.FromResult(ApiKeyValidationStatus.Valid); });
            using var closer = new WindowCloser(window);

            window.SetApiKeyText(ValidKey);
            window.SaveApiKey().GetAwaiter().GetResult();

            Assert.Equal(1, probes);
            Assert.Equal(ValidKey, store.ReadApiKey());
        });
    }

    [Fact]
    public void WhitespaceAroundThePastedKeyIsTrimmedBeforeProbeAndSave()
    {
        StaTestRunner.Run(() =>
        {
            string? probed = null;
            var store = new FakeSecretStore();
            var window = new SettingsWindow(
                AppSettings.Default,
                secretStore: store,
                validateApiKey: (key, _) => { probed = key; return Task.FromResult(ApiKeyValidationStatus.Valid); });
            using var closer = new WindowCloser(window);

            window.SetApiKeyText("\t " + ValidKey + " \r\n");
            window.SaveApiKey().GetAwaiter().GetResult();

            Assert.Equal(ValidKey, probed);
            Assert.Equal(ValidKey, store.ReadApiKey());
        });
    }

    [Fact]
    public void TheCheckingStatusIsRenderedWhileTheProbeIsInFlight()
    {
        StaTestRunner.Run(() =>
        {
            var validationStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var allowValidation = new TaskCompletionSource<ApiKeyValidationStatus>(TaskCreationOptions.RunContinuationsAsynchronously);
            var window = new SettingsWindow(
                AppSettings.Default,
                secretStore: new FakeSecretStore(),
                validateApiKey: (_, _) =>
                {
                    validationStarted.SetResult();
                    return allowValidation.Task;
                });
            using var closer = new WindowCloser(window);

            window.SetApiKeyText(ValidKey);
            var saveTask = window.SaveApiKey();
            validationStarted.Task.GetAwaiter().GetResult();

            Assert.Contains("Checking", window.ApiKeyStatusText, StringComparison.Ordinal);
            VisualTreeAssert.AssertWindowContainsText(window, "Checking");

            allowValidation.SetResult(ApiKeyValidationStatus.Valid);
            saveTask.GetAwaiter().GetResult();
            Assert.Equal("API key saved.", window.ApiKeyStatusText);
        });
    }

    [Fact]
    public void TheTestKeySkipsTheProbeUnderAnAllowingPolicy()
    {
        StaTestRunner.Run(() =>
        {
            var probes = 0;
            var store = new FakeSecretStore();
            var window = new SettingsWindow(
                AppSettings.Default,
                secretStore: store,
                validateApiKey: (_, _) => { probes++; return Task.FromResult(ApiKeyValidationStatus.Valid); },
                apiKeyValidator: new ApiKeyValidator(allowTestKey: true));
            using var closer = new WindowCloser(window);

            window.SetApiKeyText("testdev");
            window.SaveApiKey().GetAwaiter().GetResult();

            Assert.Equal(0, probes);
            Assert.Equal("testdev", store.ReadApiKey());
        });
    }

    [Fact]
    public void TheTestKeyIsRejectedUnderTheDefaultPolicy()
    {
        StaTestRunner.Run(() =>
        {
            var store = new FakeSecretStore();
            var window = new SettingsWindow(AppSettings.Default, secretStore: store);
            using var closer = new WindowCloser(window);

            window.SetApiKeyText("testdev");
            window.SaveApiKey().GetAwaiter().GetResult();

            Assert.Null(store.ReadApiKey());
            Assert.Equal("API key must be 64 hex characters.", window.ApiKeyStatusText);
        });
    }

    [Fact]
    public void ARejectedKeyIsNotSavedAndSaysRejected()
    {
        StaTestRunner.Run(() =>
        {
            var store = new FakeSecretStore();
            var window = new SettingsWindow(
                AppSettings.Default,
                secretStore: store,
                validateApiKey: (_, _) => Task.FromResult(ApiKeyValidationStatus.Rejected));
            using var closer = new WindowCloser(window);

            window.SetApiKeyText(ValidKey);
            window.SaveApiKey().GetAwaiter().GetResult();

            Assert.Null(store.ReadApiKey());
            Assert.Contains("rejected", window.ApiKeyStatusText, StringComparison.OrdinalIgnoreCase);
        });
    }

    [Fact]
    public void ANetworkErrorSavesTheKeyWithACouldNotReachWarning()
    {
        StaTestRunner.Run(() =>
        {
            var store = new FakeSecretStore();
            var window = new SettingsWindow(
                AppSettings.Default,
                secretStore: store,
                validateApiKey: (_, _) => Task.FromResult(ApiKeyValidationStatus.NetworkError));
            using var closer = new WindowCloser(window);

            window.SetApiKeyText(ValidKey);
            window.SaveApiKey().GetAwaiter().GetResult();

            Assert.Equal(ValidKey, store.ReadApiKey());
            Assert.StartsWith("Could not reach VirusTotal", window.ApiKeyStatusText, StringComparison.Ordinal);
        });
    }

    [Fact]
    public void TheWindowOpensScrolledToTheTopEvenWhenContentOverflows()
    {
        StaTestRunner.Run(() =>
        {
            var store = new FakeSecretStore();
            store.SaveApiKey(ValidKey);
            var window = new SettingsWindow(AppSettings.Default, secretStore: store);
            using var closer = new WindowCloser(window);

            window.Height = 520;
            window.Show();
            window.Dispatcher.Invoke(() => { }, System.Windows.Threading.DispatcherPriority.ContextIdle);

            Assert.True(window.PageScroller.ScrollableHeight > 0);
            Assert.Equal(0d, window.PageScroller.VerticalOffset);
        });
    }

    [Fact]
    public void ASavedKeyPreloadsIntoTheKeyBox()
    {
        StaTestRunner.Run(() =>
        {
            var store = new FakeSecretStore();
            store.SaveApiKey(ValidKey);
            var window = new SettingsWindow(AppSettings.Default, secretStore: store);
            using var closer = new WindowCloser(window);

            Assert.Equal(ValidKey, window.ViewModel.ApiKeyText);
            Assert.Equal(ValidKey, window.ApiKeyPasswordBox.Password);
            Assert.False(window.ApiKeyPasswordBox.IsPasswordRevealed);
            Assert.Equal(
                new string(window.ApiKeyPasswordBox.PasswordChar, ValidKey.Length),
                window.ApiKeyPasswordBox.Text);
            Assert.Equal("API key saved.", window.ApiKeyStatusText);
        });
    }

    [Fact]
    public void WithoutASavedKeyTheKeyBoxStartsEmpty()
    {
        StaTestRunner.Run(() =>
        {
            var window = new SettingsWindow(AppSettings.Default, secretStore: new FakeSecretStore());
            using var closer = new WindowCloser(window);

            Assert.Equal(string.Empty, window.ApiKeyPasswordBox.Password);
            Assert.Equal("No API key saved.", window.ApiKeyStatusText);
        });
    }

    [Fact]
    public void ClearRemovesTheStoredKeyImmediately()
    {
        StaTestRunner.Run(() =>
        {
            var store = new FakeSecretStore();
            store.SaveApiKey(ValidKey);
            var window = new SettingsWindow(AppSettings.Default, secretStore: store);
            using var closer = new WindowCloser(window);

            Assert.Equal(ValidKey, window.ViewModel.ApiKeyText);

            window.ViewModel.ClearApiKey();

            Assert.Null(store.ReadApiKey());
            Assert.Equal(string.Empty, window.ViewModel.ApiKeyText);
            Assert.Equal(string.Empty, window.ApiKeyPasswordBox.Password);
            Assert.Equal("API key cleared.", window.ApiKeyStatusText);
        });
    }

    [Fact]
    public void ClearWithALockedKeyFileReportsFailureWithoutThrowing()
    {
        StaTestRunner.Run(() =>
        {
            var store = new FakeSecretStore { ThrowOnDelete = true };
            store.SaveApiKey(ValidKey);
            var window = new SettingsWindow(AppSettings.Default, secretStore: store);
            using var closer = new WindowCloser(window);

            var exception = Record.Exception(() => window.ViewModel.ClearApiKey());

            Assert.Null(exception);
            Assert.Equal(ValidKey, store.ReadApiKey());
            Assert.Equal(ValidKey, window.ViewModel.ApiKeyText);
            Assert.StartsWith("Could not clear API key", window.ApiKeyStatusText, StringComparison.Ordinal);
        });
    }

    [Fact]
    public void ClearHistoryWithALockedFileReportsFailureWithoutThrowing()
    {
        StaTestRunner.Run(() =>
        {
            var window = new SettingsWindow(
                AppSettings.Default,
                clearHistory: () => throw new IOException("history.jsonl is in use by another process"));
            using var closer = new WindowCloser(window);

            var exception = Record.Exception(() => window.ClearHistory());

            Assert.Null(exception);
            Assert.StartsWith("Could not clear history", window.ViewModel.HistoryStatusText, StringComparison.Ordinal);
        });
    }

    [Fact]
    public void ClearApiKeyIsUnavailableWhileASaveIsInFlight()
    {
        StaTestRunner.Run(() =>
        {
            var validationStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            // Left as ordinary (synchronous-continuation) task completion sources:
            // resolving them below must run the rest of the save inline on this
            // thread, the same way it would inline onto the UI thread in the
            // running app, so the running-state transition is observable here
            // without a live dispatcher message loop.
            var allowValidation = new TaskCompletionSource<ApiKeyValidationStatus>();
            var window = new SettingsWindow(
                AppSettings.Default,
                secretStore: new FakeSecretStore(),
                validateApiKey: (_, _) =>
                {
                    validationStarted.SetResult();
                    return allowValidation.Task;
                });
            using var closer = new WindowCloser(window);

            window.SetApiKeyText(ValidKey);
            Assert.True(window.ViewModel.ClearApiKeyCommand.CanExecute(null));

            window.ViewModel.SaveApiKeyCommand.Execute(null);
            validationStarted.Task.GetAwaiter().GetResult();

            Assert.False(window.ViewModel.ClearApiKeyCommand.CanExecute(null));

            allowValidation.SetResult(ApiKeyValidationStatus.Valid);

            Assert.True(window.ViewModel.ClearApiKeyCommand.CanExecute(null));
        });
    }

    [Fact]
    public void ClearAndSaveKeyAreUnavailableWhileASettingsSaveProbeIsInFlight()
    {
        StaTestRunner.Run(() =>
        {
            // Plain TaskCompletionSources on purpose: resolving them below runs
            // the rest of the save inline on this thread, which is the only way
            // to assert mid-flight state without a live dispatcher message loop.
            var validationStarted = new TaskCompletionSource();
            var allowValidation = new TaskCompletionSource<ApiKeyValidationStatus>();
            var window = new SettingsWindow(
                AppSettings.Default,
                saveSettings: _ => { },
                secretStore: new FakeSecretStore(),
                validateApiKey: (_, _) =>
                {
                    validationStarted.SetResult();
                    return allowValidation.Task;
                });
            using var closer = new WindowCloser(window);

            window.SetApiKeyText(ValidKey);
            window.ViewModel.SaveSettingsCommand.Execute(null);
            validationStarted.Task.GetAwaiter().GetResult();

            Assert.False(window.ViewModel.ClearApiKeyCommand.CanExecute(null));
            Assert.False(window.ViewModel.SaveApiKeyCommand.CanExecute(null));

            allowValidation.SetResult(ApiKeyValidationStatus.Valid);

            Assert.True(window.ViewModel.ClearApiKeyCommand.CanExecute(null));
            Assert.True(window.ViewModel.SaveApiKeyCommand.CanExecute(null));
        });
    }

    [Fact]
    public void TheKeyButtonsAreRequeriedWhenASettingsSaveStartsAndFinishes()
    {
        StaTestRunner.Run(() =>
        {
            // The guards above only prove CanExecute returns the right answer
            // when asked; this proves the buttons are actually told to ask, so
            // they visibly disable and re-enable around the save.
            var validationStarted = new TaskCompletionSource();
            var allowValidation = new TaskCompletionSource<ApiKeyValidationStatus>();
            var window = new SettingsWindow(
                AppSettings.Default,
                saveSettings: _ => { },
                secretStore: new FakeSecretStore(),
                validateApiKey: (_, _) =>
                {
                    validationStarted.SetResult();
                    return allowValidation.Task;
                });
            using var closer = new WindowCloser(window);

            var requeries = 0;
            window.ViewModel.ClearApiKeyCommand.CanExecuteChanged += (_, _) => requeries++;

            window.SetApiKeyText(ValidKey);
            window.ViewModel.SaveSettingsCommand.Execute(null);
            validationStarted.Task.GetAwaiter().GetResult();

            var requeriesWhileRunning = requeries;
            Assert.True(requeriesWhileRunning > 0);

            allowValidation.SetResult(ApiKeyValidationStatus.Valid);

            Assert.True(requeries > requeriesWhileRunning);
        });
    }

    [Fact]
    public void SaveSettingsIsUnavailableWhileAStandaloneKeySaveIsInFlight()
    {
        StaTestRunner.Run(() =>
        {
            // Same inline-resolution pattern as above.
            var validationStarted = new TaskCompletionSource();
            var allowValidation = new TaskCompletionSource<ApiKeyValidationStatus>();
            var window = new SettingsWindow(
                AppSettings.Default,
                saveSettings: _ => { },
                secretStore: new FakeSecretStore(),
                validateApiKey: (_, _) =>
                {
                    validationStarted.SetResult();
                    return allowValidation.Task;
                });
            using var closer = new WindowCloser(window);

            window.SetApiKeyText(ValidKey);
            window.ViewModel.SaveApiKeyCommand.Execute(null);
            validationStarted.Task.GetAwaiter().GetResult();

            Assert.False(window.ViewModel.SaveSettingsCommand.CanExecute(null));

            allowValidation.SetResult(ApiKeyValidationStatus.Valid);

            Assert.True(window.ViewModel.SaveSettingsCommand.CanExecute(null));
        });
    }

    [Fact]
    public void KeepLocalHistoryTogglesTheLiveStatusBeforeSaving()
    {
        StaTestRunner.Run(() =>
        {
            var window = new SettingsWindow(AppSettings.Default);
            using var closer = new WindowCloser(window);

            Assert.Equal("Local history is enabled.", window.ViewModel.HistoryStatusText);

            window.ViewModel.KeepLocalHistory = false;

            Assert.Equal("Local history is disabled.", window.ViewModel.HistoryStatusText);
        });
    }

    [Fact]
    public void ClearHistoryInvokesTheCallbackAndReportsImmediately()
    {
        StaTestRunner.Run(() =>
        {
            var cleared = false;
            var window = new SettingsWindow(AppSettings.Default, clearHistory: () => cleared = true);
            using var closer = new WindowCloser(window);

            window.ClearHistory();

            Assert.True(cleared);
            Assert.Equal("Local history cleared.", window.ViewModel.HistoryStatusText);
        });
    }

    [Fact]
    public void OpenHistoryIsDisabledWithoutACallbackAndWiredWithOne()
    {
        StaTestRunner.Run(() =>
        {
            var without = new SettingsWindow(AppSettings.Default);
            using var closerA = new WindowCloser(without);
            Assert.False(without.ViewModel.OpenHistoryCommand.CanExecute(null));

            var opened = false;
            var with = new SettingsWindow(AppSettings.Default, openHistory: () => opened = true);
            using var closerB = new WindowCloser(with);
            Assert.True(with.ViewModel.OpenHistoryCommand.CanExecute(null));

            with.OpenHistory();
            Assert.True(opened);
        });
    }

    [Fact]
    public void TheAnonymizerCalloutIsHonestAndRendered()
    {
        StaTestRunner.Run(() =>
        {
            var window = new SettingsWindow(AppSettings.Default);
            using var closer = new WindowCloser(window);

            VisualTreeAssert.AssertWindowContainsText(
                window, "They do not make public VirusTotal uploads private");
        });
    }

    [Fact]
    public void TheVersionCaptionShowsTheHelpersValue()
    {
        StaTestRunner.Run(() =>
        {
            var window = new SettingsWindow(AppSettings.Default);
            using var closer = new WindowCloser(window);

            VisualTreeAssert.AssertWindowContainsText(
                window, $"Version {AppVersionInfo.GetInformationalVersion()}");
        });
    }

    [Fact]
    public void TheVersionHelperReturnsANonEmptySemverShapedString()
    {
        var version = AppVersionInfo.GetInformationalVersion();

        Assert.False(string.IsNullOrWhiteSpace(version));
        Assert.Matches(@"^\d+\.\d+\.\d+", version);
    }

    [Fact]
    public void TheAppVersionMatchesTheCoreVersion()
    {
        // Both projects inherit their version from Directory.Build.props with
        // no per-project override; this fails if either one grows one back.
        var coreAssembly = Assembly.Load("VirusTotalOneClick.Core");

        var appVersion = AppVersionInfo.GetInformationalVersion();
        var coreVersion = AppVersionInfo.GetInformationalVersion(coreAssembly);

        Assert.Equal(coreVersion, appVersion);
    }
}

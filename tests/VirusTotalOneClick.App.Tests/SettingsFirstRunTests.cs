using VirusTotalOneClick.Core;
using VirusTotalOneClick.UI;

namespace VirusTotalOneClick.AppTests;

public class SettingsFirstRunTests
{
    private static readonly string ValidKey = new('b', 64);

    private static SettingsWindow FirstRunWindow(
        Action<AppSettings>? saveSettings = null,
        ISecretStore? secretStore = null,
        Action<AppSettings>? applyIntegration = null,
        Func<string, CancellationToken, Task<ApiKeyValidationStatus>>? validateApiKey = null)
        => new(
            AppSettings.Default,
            saveSettings,
            secretStore,
            clearHistory: null,
            applyIntegration,
            openHistory: null,
            validateApiKey,
            apiKeyValidator: null,
            isFirstRun: true);

    [Fact]
    public void FirstRunDressesThePageAsSetup()
    {
        StaTestRunner.Run(() =>
        {
            var window = FirstRunWindow();
            using var closer = new WindowCloser(window);

            Assert.True(window.ViewModel.IsFirstRun);
            Assert.Equal("Set up VirusTotal OneClick", window.ViewModel.PageTitleText);
            Assert.Equal("Save setup", window.ViewModel.SaveButtonText);
            Assert.Equal("Setup not saved.", window.SettingsStatusText);
            VisualTreeAssert.AssertWindowContainsText(window, "Set up VirusTotal OneClick");
            VisualTreeAssert.AssertWindowContainsText(window, "Save setup");
        });
    }

    [Fact]
    public void FirstRunCopyIsHonestAboutUploads()
    {
        StaTestRunner.Run(() =>
        {
            var window = FirstRunWindow();
            using var closer = new WindowCloser(window);

            VisualTreeAssert.AssertWindowContainsText(
                window, "Files upload to VirusTotal when you use the Explorer action");
            VisualTreeAssert.AssertWindowDoesNotContainText(
                window, "Your data stays on your machine");
        });
    }

    [Fact]
    public void FirstRunShowsTheKeyProtectionCaptionWhenTheBoxStartsEmpty()
    {
        StaTestRunner.Run(() =>
        {
            var window = FirstRunWindow(secretStore: new FakeSecretStore());
            using var closer = new WindowCloser(window);

            Assert.Equal("Stored with Windows current-user protection.", window.ApiKeyStatusText);
            VisualTreeAssert.AssertWindowContainsText(window, "Stored with Windows current-user protection.");
        });
    }

    [Fact]
    public void FirstRunWithAKeyAlreadyStoredReportsItSavedInstead()
    {
        StaTestRunner.Run(() =>
        {
            var store = new FakeSecretStore();
            store.SaveApiKey(ValidKey);
            var window = FirstRunWindow(secretStore: store);
            using var closer = new WindowCloser(window);

            Assert.Equal("API key saved.", window.ApiKeyStatusText);
        });
    }

    [Fact]
    public void FirstRunFocusesTheApiKeyBox()
    {
        StaTestRunner.Run(() =>
        {
            var window = FirstRunWindow();
            using var closer = new WindowCloser(window);

            window.Show();

            Assert.True(window.ApiKeyPasswordBox.IsKeyboardFocusWithin);
        });
    }

    [Fact]
    public void ANormalOpenKeepsTheSettingsDressing()
    {
        StaTestRunner.Run(() =>
        {
            var window = new SettingsWindow(AppSettings.Default, secretStore: new FakeSecretStore());
            using var closer = new WindowCloser(window);

            Assert.False(window.ViewModel.IsFirstRun);
            Assert.Equal("Settings", window.ViewModel.PageTitleText);
            Assert.Equal("Save settings", window.ViewModel.SaveButtonText);
            Assert.Equal("Settings not saved in this session.", window.SettingsStatusText);
            Assert.Equal("No API key saved.", window.ApiKeyStatusText);
        });
    }

    [Fact]
    public void SavingOnFirstRunPersistsKeySettingsAndIntegrationTogether()
    {
        StaTestRunner.Run(() =>
        {
            AppSettings? savedSettings = null;
            AppSettings? appliedSettings = null;
            var store = new FakeSecretStore();
            var window = FirstRunWindow(
                saveSettings: s => savedSettings = s,
                secretStore: store,
                applyIntegration: s => appliedSettings = s);
            using var closer = new WindowCloser(window);

            window.SetApiKeyText(ValidKey);
            window.ViewModel.ScrubSubmittedFilename = true;
            window.SaveSettings().GetAwaiter().GetResult();

            Assert.Equal(ValidKey, store.ReadApiKey());
            Assert.True(savedSettings!.ScrubSubmittedFilename);
            Assert.True(appliedSettings!.ScrubSubmittedFilename);
            Assert.Equal("Setup saved.", window.SettingsStatusText);
            Assert.DoesNotContain(ValidKey, savedSettings.ToString(), StringComparison.Ordinal);
        });
    }

    [Fact]
    public void AnEmptyKeyOnFirstRunIsAllowedAndSettingsStillSave()
    {
        StaTestRunner.Run(() =>
        {
            AppSettings? saved = null;
            var window = FirstRunWindow(
                saveSettings: s => saved = s,
                secretStore: new FakeSecretStore());
            using var closer = new WindowCloser(window);

            window.SaveSettings().GetAwaiter().GetResult();

            Assert.NotNull(saved);
            Assert.Equal("No API key saved.", window.ApiKeyStatusText);
            Assert.Equal("Setup saved.", window.SettingsStatusText);
        });
    }

    [Fact]
    public void ABadKeyOnFirstRunAbortsTheWholeSave()
    {
        StaTestRunner.Run(() =>
        {
            var savedCount = 0;
            var window = FirstRunWindow(
                saveSettings: _ => savedCount++,
                secretStore: new FakeSecretStore());
            using var closer = new WindowCloser(window);

            window.SetApiKeyText("garbage");
            window.SaveSettings().GetAwaiter().GetResult();

            Assert.Equal(0, savedCount);
            Assert.Equal("API key must be 64 hex characters.", window.ApiKeyStatusText);
            Assert.Equal("API key must be 64 hex characters.", window.SettingsStatusText);
        });
    }

    [Fact]
    public void ACanceledProbeOnFirstRunAbortsTheWholeSave()
    {
        StaTestRunner.Run(() =>
        {
            var savedCount = 0;
            using var cancellation = new CancellationTokenSource();
            var window = FirstRunWindow(
                saveSettings: _ => savedCount++,
                secretStore: new FakeSecretStore(),
                validateApiKey: (_, token) =>
                {
                    cancellation.Cancel();
                    return Task.FromCanceled<ApiKeyValidationStatus>(token);
                });
            using var closer = new WindowCloser(window);

            window.SetApiKeyText(ValidKey);
            window.SaveSettings(cancellation.Token).GetAwaiter().GetResult();

            Assert.Equal(0, savedCount);
            Assert.Equal("API key check canceled. Key was not saved.", window.ApiKeyStatusText);
        });
    }

    [Fact]
    public void TheCheckingStatusIsRenderedWhileAFirstRunProbeIsInFlight()
    {
        StaTestRunner.Run(() =>
        {
            var validationStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var allowValidation = new TaskCompletionSource<ApiKeyValidationStatus>(TaskCreationOptions.RunContinuationsAsynchronously);
            var window = FirstRunWindow(
                saveSettings: _ => { },
                secretStore: new FakeSecretStore(),
                validateApiKey: (_, _) =>
                {
                    validationStarted.SetResult();
                    return allowValidation.Task;
                });
            using var closer = new WindowCloser(window);

            window.SetApiKeyText(ValidKey);
            var saveTask = window.SaveSettings();
            validationStarted.Task.GetAwaiter().GetResult();

            Assert.Contains("Checking", window.ApiKeyStatusText, StringComparison.Ordinal);
            VisualTreeAssert.AssertWindowContainsText(window, "Checking");

            // The key caption carries the in-flight state on its own; the
            // footer keeps its opening line until the save actually lands.
            Assert.Equal("Setup not saved.", window.SettingsStatusText);

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
                saveSettings: _ => { },
                secretStore: store,
                clearHistory: null,
                applyIntegration: null,
                openHistory: null,
                validateApiKey: (_, _) => { probes++; return Task.FromResult(ApiKeyValidationStatus.Valid); },
                apiKeyValidator: new ApiKeyValidator(allowTestKey: true),
                isFirstRun: true);
            using var closer = new WindowCloser(window);

            window.SetApiKeyText("testdev");
            window.SaveSettings().GetAwaiter().GetResult();

            Assert.Equal(0, probes);
            Assert.Equal("testdev", store.ReadApiKey());
        });
    }

    [Fact]
    public void IntegrationFailureOnFirstRunStillReportsSavedWithTheMessage()
    {
        StaTestRunner.Run(() =>
        {
            var window = FirstRunWindow(
                saveSettings: _ => { },
                secretStore: new FakeSecretStore(),
                applyIntegration: _ => throw new InvalidOperationException("registry denied"));
            using var closer = new WindowCloser(window);

            window.SaveSettings().GetAwaiter().GetResult();

            Assert.Contains("Setup saved, but Windows integration failed", window.SettingsStatusText, StringComparison.Ordinal);
            Assert.Contains("registry denied", window.SettingsStatusText, StringComparison.Ordinal);
        });
    }

    [Fact]
    public void ASettingsSaveFailureOnFirstRunAbortsBeforeIntegration()
    {
        StaTestRunner.Run(() =>
        {
            var integrationRan = false;
            var window = FirstRunWindow(
                saveSettings: _ => throw new IOException("disk full"),
                secretStore: new FakeSecretStore(),
                applyIntegration: _ => integrationRan = true);
            using var closer = new WindowCloser(window);

            window.SaveSettings().GetAwaiter().GetResult();

            Assert.False(integrationRan);
            Assert.Equal("Could not save settings: disk full", window.SettingsStatusText);
        });
    }
}

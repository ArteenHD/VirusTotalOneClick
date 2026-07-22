using System.Windows;
using VirusTotalOneClick.Core;
using VirusTotalOneClick.UI;

namespace VirusTotalOneClick.AppTests;

public class SetupWindowTests
{
    private static readonly string ValidKey = new('b', 64);

    [Fact]
    public void TheWindowCanBeResizedWithASensibleMinimumSize()
    {
        StaTestRunner.Run(() =>
        {
            var window = new SetupWindow(AppSettings.Default);
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
            var window = new SetupWindow(AppSettings.Default);
            using var closer = new WindowCloser(window);

            Assert.Equal(
                AppWindow.ClampHeightToWorkArea(780, SystemParameters.WorkArea.Height, window.MinHeight),
                window.Height);
        });
    }

    [Fact]
    public void TheSaveOrCloseButtonIsTheDefaultButton()
    {
        StaTestRunner.Run(() =>
        {
            var window = new SetupWindow(AppSettings.Default);
            using var closer = new WindowCloser(window);

            Assert.True(window.SaveOrCloseButton.IsDefault);
        });
    }

    [Fact]
    public void InitialKeyboardFocusIsOnTheApiKeyBox()
    {
        StaTestRunner.Run(() =>
        {
            var window = new SetupWindow(AppSettings.Default);
            using var closer = new WindowCloser(window);

            window.Show();

            Assert.True(window.ApiKeyPasswordBox.IsKeyboardFocusWithin);
        });
    }

    [Fact]
    public void ConstructsWithTheFixedSectionOrder()
    {
        StaTestRunner.Run(() =>
        {
            using var closer = new WindowCloser(new SetupWindow(AppSettings.Default));

            Assert.Equal("VirusTotal OneClick", closer.Window.Title);
            Assert.Equal(
                ["API Key", "Upload Behavior", "Folders", "History", "Anonymizer", "Explorer Integration"],
                ((SetupWindow)closer.Window).SectionNames);
        });
    }

    [Fact]
    public void FirstRunCopyIsHonestAboutUploads()
    {
        StaTestRunner.Run(() =>
        {
            var window = new SetupWindow(AppSettings.Default);
            using var closer = new WindowCloser(window);

            VisualTreeAssert.AssertWindowContainsText(
                window, "Files upload to VirusTotal when you use the Explorer action");
            VisualTreeAssert.AssertWindowDoesNotContainText(
                window, "Your data stays on your machine");
        });
    }

    [Fact]
    public void SavingPersistsKeySettingsAndIntegrationTogether()
    {
        StaTestRunner.Run(() =>
        {
            AppSettings? savedSettings = null;
            AppSettings? appliedSettings = null;
            var store = new FakeSecretStore();
            var window = new SetupWindow(
                AppSettings.Default,
                saveSettings: s => savedSettings = s,
                secretStore: store,
                applyIntegration: s => appliedSettings = s);
            using var closer = new WindowCloser(window);

            window.SetApiKeyText(ValidKey);
            window.ViewModel.ScrubSubmittedFilename = true;
            window.SaveSetup().GetAwaiter().GetResult();

            Assert.Equal(ValidKey, store.ReadApiKey());
            Assert.True(savedSettings!.ScrubSubmittedFilename);
            Assert.True(appliedSettings!.ScrubSubmittedFilename);
            Assert.Equal("Setup saved.", window.SetupStatusText);
            Assert.True(window.ViewModel.IsSetupSaved);
            Assert.DoesNotContain(ValidKey, savedSettings.ToString(), StringComparison.Ordinal);
        });
    }

    [Fact]
    public void AnEmptyKeyIsAllowedAndSettingsStillSave()
    {
        StaTestRunner.Run(() =>
        {
            AppSettings? saved = null;
            var window = new SetupWindow(
                AppSettings.Default,
                saveSettings: s => saved = s,
                secretStore: new FakeSecretStore());
            using var closer = new WindowCloser(window);

            window.SaveSetup().GetAwaiter().GetResult();

            Assert.NotNull(saved);
            Assert.Equal("No API key saved.", window.ApiKeyStatusText);
            Assert.Equal("Setup saved.", window.SetupStatusText);
        });
    }

    [Fact]
    public void ABadKeyAbortsTheWholeSave()
    {
        StaTestRunner.Run(() =>
        {
            var savedCount = 0;
            var window = new SetupWindow(
                AppSettings.Default,
                saveSettings: _ => savedCount++,
                secretStore: new FakeSecretStore());
            using var closer = new WindowCloser(window);

            window.SetApiKeyText("garbage");
            window.SaveSetup().GetAwaiter().GetResult();

            Assert.Equal(0, savedCount);
            Assert.False(window.ViewModel.IsSetupSaved);
            Assert.Equal("API key must be 64 hex characters.", window.ApiKeyStatusText);
        });
    }

    [Fact]
    public void IntegrationFailureStillCountsAsSavedAndSurfacesTheMessage()
    {
        StaTestRunner.Run(() =>
        {
            var window = new SetupWindow(
                AppSettings.Default,
                saveSettings: _ => { },
                secretStore: new FakeSecretStore(),
                applyIntegration: _ => throw new InvalidOperationException("registry denied"));
            using var closer = new WindowCloser(window);

            window.SaveSetup().GetAwaiter().GetResult();

            Assert.True(window.ViewModel.IsSetupSaved);
            Assert.Contains("Windows integration failed", window.SetupStatusText, StringComparison.Ordinal);
            Assert.Contains("registry denied", window.SetupStatusText, StringComparison.Ordinal);
        });
    }

    [Fact]
    public void ASettingsSaveFailureAbortsBeforeIntegration()
    {
        StaTestRunner.Run(() =>
        {
            var integrationRan = false;
            var window = new SetupWindow(
                AppSettings.Default,
                saveSettings: _ => throw new IOException("disk full"),
                secretStore: new FakeSecretStore(),
                applyIntegration: _ => integrationRan = true);
            using var closer = new WindowCloser(window);

            window.SaveSetup().GetAwaiter().GetResult();

            Assert.False(integrationRan);
            Assert.False(window.ViewModel.IsSetupSaved);
            Assert.Equal("Could not save settings: disk full", window.SetupStatusText);
        });
    }

    [Fact]
    public void TheCheckingStatusIsRenderedWhileTheProbeIsInFlight()
    {
        StaTestRunner.Run(() =>
        {
            var validationStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var allowValidation = new TaskCompletionSource<ApiKeyValidationStatus>(TaskCreationOptions.RunContinuationsAsynchronously);
            var window = new SetupWindow(
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
            var saveTask = window.SaveSetup();
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
            var window = new SetupWindow(
                AppSettings.Default,
                saveSettings: _ => { },
                secretStore: store,
                validateApiKey: (_, _) => { probes++; return Task.FromResult(ApiKeyValidationStatus.Valid); },
                apiKeyValidator: new ApiKeyValidator(allowTestKey: true));
            using var closer = new WindowCloser(window);

            window.SetApiKeyText("testdev");
            window.SaveSetup().GetAwaiter().GetResult();

            Assert.Equal(0, probes);
            Assert.Equal("testdev", store.ReadApiKey());
        });
    }

    [Fact]
    public void ARejectedKeyAbortsTheSave()
    {
        StaTestRunner.Run(() =>
        {
            var store = new FakeSecretStore();
            var window = new SetupWindow(
                AppSettings.Default,
                saveSettings: _ => { },
                secretStore: store,
                validateApiKey: (_, _) => Task.FromResult(ApiKeyValidationStatus.Rejected));
            using var closer = new WindowCloser(window);

            window.SetApiKeyText(ValidKey);
            window.SaveSetup().GetAwaiter().GetResult();

            Assert.Null(store.ReadApiKey());
            Assert.False(window.ViewModel.IsSetupSaved);
            Assert.Contains("rejected", window.ApiKeyStatusText, StringComparison.OrdinalIgnoreCase);
        });
    }

    [Fact]
    public void WhitespaceAroundThePastedKeyIsTrimmed()
    {
        StaTestRunner.Run(() =>
        {
            string? probed = null;
            var store = new FakeSecretStore();
            var window = new SetupWindow(
                AppSettings.Default,
                saveSettings: _ => { },
                secretStore: store,
                validateApiKey: (key, _) => { probed = key; return Task.FromResult(ApiKeyValidationStatus.Valid); });
            using var closer = new WindowCloser(window);

            window.SetApiKeyText("  " + ValidKey + "\n");
            window.SaveSetup().GetAwaiter().GetResult();

            Assert.Equal(ValidKey, probed);
            Assert.Equal(ValidKey, store.ReadApiKey());
        });
    }

    [Fact]
    public void RadioTogglesRoundTripThroughBuildSettings()
    {
        StaTestRunner.Run(() =>
        {
            var window = new SetupWindow(AppSettings.Default);
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
}

namespace VirusTotalOneClick.Core.Tests;

public class AppSettingsStoreTests
{
    [Fact]
    public void DefaultsMatchProductDecisions()
    {
        var settings = AppSettings.Default;

        Assert.False(settings.ConfirmBeforeFileUpload);
        Assert.Equal(FolderUploadMode.ReviewSelectedFiles, settings.DefaultFolderMode);
        Assert.True(settings.KeepLocalHistory);
        Assert.False(settings.ScrubSubmittedFilename);
        Assert.True(settings.ScrubZipInternalPaths);
        Assert.True(settings.EnableClassicContextMenu);
        Assert.True(settings.EnableSendToShortcut);
        Assert.True(settings.EnableStartMenuShortcuts);
        Assert.False(settings.LaunchOnStartup);
    }

    [Fact]
    public void SaveThenLoadRoundTripsEveryProperty()
    {
        using var folder = new TemporaryFolder();
        var store = new AppSettingsStore(Path.Combine(folder.Path, "settings.json"));
        var settings = new AppSettings
        {
            ConfirmBeforeFileUpload = true,
            DefaultFolderMode = FolderUploadMode.ReviewThenZip,
            KeepLocalHistory = false,
            ScrubSubmittedFilename = true,
            ScrubZipInternalPaths = false,
            EnableClassicContextMenu = false,
            EnableSendToShortcut = false,
            EnableStartMenuShortcuts = false,
            LaunchOnStartup = true,
        };

        store.Save(settings);

        Assert.Equal(settings, store.Load());
    }

    [Fact]
    public void LoadReturnsDefaultsWhenFileMissingAndCreatesNoFile()
    {
        using var folder = new TemporaryFolder();
        var path = Path.Combine(folder.Path, "settings.json");

        var loaded = new AppSettingsStore(path).Load();

        Assert.Equal(AppSettings.Default, loaded);
        Assert.False(File.Exists(path));
    }

    [Fact]
    public void LoadReturnsDefaultsWhenFileIsCorrupt()
    {
        using var folder = new TemporaryFolder();
        var path = folder.CreateFile("settings.json", "{ not valid json ]");

        Assert.Equal(AppSettings.Default, new AppSettingsStore(path).Load());
    }

    [Fact]
    public void LoadReturnsDefaultsWhenFileContainsJsonNull()
    {
        using var folder = new TemporaryFolder();
        var path = folder.CreateFile("settings.json", "null");

        Assert.Equal(AppSettings.Default, new AppSettingsStore(path).Load());
    }

    [Fact]
    public void LoadFillsPropertiesMissingFromOlderFilesWithDefaults()
    {
        using var folder = new TemporaryFolder();
        var path = folder.CreateFile("settings.json", """{ "ConfirmBeforeFileUpload": true }""");

        var loaded = new AppSettingsStore(path).Load();

        Assert.True(loaded.ConfirmBeforeFileUpload);
        Assert.Equal(FolderUploadMode.ReviewSelectedFiles, loaded.DefaultFolderMode);
        Assert.True(loaded.KeepLocalHistory);
        Assert.True(loaded.ScrubZipInternalPaths);
        Assert.True(loaded.EnableClassicContextMenu);
    }

    [Fact]
    public void LoadIgnoresUnknownPropertiesFromNewerFiles()
    {
        using var folder = new TemporaryFolder();
        var path = folder.CreateFile("settings.json", """{ "KeepLocalHistory": false, "SomeFutureSetting": 42 }""");

        var loaded = new AppSettingsStore(path).Load();

        Assert.False(loaded.KeepLocalHistory);
    }

    [Fact]
    public void SaveCreatesTheParentDirectoryWhenMissing()
    {
        using var folder = new TemporaryFolder();
        var path = Path.Combine(folder.Path, "nested", "settings.json");

        new AppSettingsStore(path).Save(AppSettings.Default);

        Assert.True(File.Exists(path));
    }

    [Fact]
    public void SaveFullyOverwritesAnExistingFile()
    {
        using var folder = new TemporaryFolder();
        var path = folder.CreateFile("settings.json", new string('x', 10_000));
        var store = new AppSettingsStore(path);

        store.Save(AppSettings.Default);

        Assert.Equal(AppSettings.Default, store.Load());
        Assert.DoesNotContain("xxxx", File.ReadAllText(path), StringComparison.Ordinal);
    }

    [Fact]
    public void SettingsNeverCarryAnApiKeyProperty()
    {
        var offenders = typeof(AppSettings)
            .GetProperties()
            .Where(property => property.Name.Contains("ApiKey", StringComparison.OrdinalIgnoreCase))
            .Select(property => property.Name)
            .ToList();

        Assert.Empty(offenders);
    }
}

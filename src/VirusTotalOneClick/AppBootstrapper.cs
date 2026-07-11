using System.IO;
using System.Net.Http;
using VirusTotalOneClick.Core;

namespace VirusTotalOneClick;

public static class AppBootstrapper
{
#if DEBUG
    private const bool AllowTestKey = true;
#else
    private const bool AllowTestKey = false;
#endif

    private static readonly Uri VirusTotalBaseUri = new("https://www.virustotal.com/api/v3/");

    // Uploads run up to the 650 MB service ceiling, so the shared client must not
    // enforce the default 100-second timeout; cancellation comes solely from the
    // per-upload CancellationToken.
    internal static readonly HttpClient UploadHttpClient = new()
    {
        BaseAddress = VirusTotalBaseUri,
        Timeout = Timeout.InfiniteTimeSpan,
    };

    internal static readonly HttpClient ValidationHttpClient = new()
    {
        BaseAddress = VirusTotalBaseUri,
        Timeout = TimeSpan.FromSeconds(30),
    };

    public static bool TryRunNonInteractive(CommandLineOptions options)
        => TryRunNonInteractive(options, AppPaths.FromCurrentUser());

    public static bool TryRunNonInteractive(CommandLineOptions options, AppPaths paths)
    {
        var settingsStore = new AppSettingsStore(paths.SettingsPath);
        var runner = new ShellIntegrationCommandRunner(
            CreateIntegrationInstaller(),
            CreateIntegrationOptions(),
            settingsStore.Load);

        return runner.TryRun(options.Mode);
    }

    public static AppComposition Compose(CommandLineOptions options, AppPaths paths)
    {
        var settingsStore = new AppSettingsStore(paths.SettingsPath);
        var settings = settingsStore.Load();
        var secretStore = new DpapiFileSecretStore(paths.AppDataDirectory);
        var historyStore = new UploadHistoryStore(paths.HistoryPath);
        var integrationInstaller = CreateIntegrationInstaller();
        var integrationOptions = CreateIntegrationOptions();
        var uploadActionFactory = new FileUploadActionFactory(
            settings, secretStore, CreateVirusTotalClient, historyStore);
        var validationClient = new VirusTotalClient(
            ValidationHttpClient, new VirusTotalOptions(string.Empty, VirusTotalBaseUri));

        return new AppComposition
        {
            Options = ResolveInteractiveMode(options, paths),
            Settings = settings,
            SaveSettings = settingsStore.Save,
            SecretStore = secretStore,
            ClearHistory = historyStore.Clear,
            ReadHistory = historyStore.ReadAll,
            ApplyIntegration = current => integrationInstaller.Apply(current, integrationOptions),
            CreateUploadAction = uploadActionFactory.Create,
            CreateZipUploadAction = request => cancellationToken =>
                new FolderZipUploadWorkflow(settings, secretStore, CreateVirusTotalClient, historyStore)
                    .RunAsync(request, cancellationToken),
            ValidateApiKey = validationClient.ValidateApiKeyAsync,
            ApiKeyValidator = new UI.ApiKeyValidator(AllowTestKey),
        };
    }

    internal static string GetExecutablePath()
        => Environment.ProcessPath ?? AppContext.BaseDirectory;

    private static CommandLineOptions ResolveInteractiveMode(CommandLineOptions options, AppPaths paths)
    {
        if (options.Mode != AppMode.Interactive || !File.Exists(paths.SettingsPath))
        {
            return options;
        }

        // A saved settings file means setup already happened: returning users
        // land on Settings instead of the first-run experience.
        return new CommandLineOptions(AppMode.Settings, []);
    }

    private static ShellIntegrationInstaller CreateIntegrationInstaller()
        => new(
            new CurrentUserRegistryWriter(),
            new SendToShortcutWriter(),
            new StartMenuShortcutWriter(),
            new StartupShortcutWriter());

    private static ShellIntegrationOptions CreateIntegrationOptions()
        => new(GetExecutablePath(), "Upload to VirusTotal");

    private static VirusTotalClient CreateVirusTotalClient(string apiKey)
        => new(UploadHttpClient, new VirusTotalOptions(apiKey, VirusTotalBaseUri));
}

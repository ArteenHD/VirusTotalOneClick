namespace VirusTotalOneClick.Core;

public sealed class FileUploadActionFactory
{
    private readonly AppSettings _settings;
    private readonly ISecretStore _secretStore;
    private readonly Func<string, IVirusTotalClient> _clientFactory;
    private readonly IUploadHistoryStore _historyStore;

    public FileUploadActionFactory(
        AppSettings settings,
        ISecretStore secretStore,
        Func<string, IVirusTotalClient> clientFactory,
        IUploadHistoryStore historyStore)
    {
        _settings = settings;
        _secretStore = secretStore;
        _clientFactory = clientFactory;
        _historyStore = historyStore;
    }

    public Func<CancellationToken, Task<FileUploadWorkflowResult>> Create(IReadOnlyList<string> paths)
    {
        return cancellationToken =>
        {
            var workflow = new FileUploadWorkflow(_settings, _secretStore, _clientFactory, _historyStore);
            return workflow.RunAsync(paths, cancellationToken);
        };
    }
}

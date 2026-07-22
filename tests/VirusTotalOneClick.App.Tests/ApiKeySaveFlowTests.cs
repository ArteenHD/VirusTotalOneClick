using System.Net.Http;
using VirusTotalOneClick.Core;
using VirusTotalOneClick.UI;

namespace VirusTotalOneClick.AppTests;

public class ApiKeySaveFlowTests
{
    private static readonly ApiKeyValidator DenyTestKey = new(allowTestKey: false);
    private static readonly ApiKeyValidator AllowTestKey = new(allowTestKey: true);
    private static readonly string ValidKey = new('a', 64);

    [Fact]
    public async Task WithoutAStoreNothingIsSaved()
    {
        var outcome = await ApiKeySaveFlow.SaveAsync(
            ValidKey, secretStore: null, DenyTestKey, validateApiKey: null, onChecking: null, CancellationToken.None);

        Assert.False(outcome.Saved);
        Assert.Equal("API key cannot be saved in this session.", outcome.StatusText);
    }

    [Fact]
    public async Task ABadlyFormattedKeyIsRejectedBeforeAnyProbe()
    {
        var probes = 0;
        var store = new FakeSecretStore();
        var outcome = await ApiKeySaveFlow.SaveAsync(
            "not-a-key", store, DenyTestKey,
            (_, _) => { probes++; return Task.FromResult(ApiKeyValidationStatus.Valid); },
            onChecking: null, CancellationToken.None);

        Assert.False(outcome.Saved);
        Assert.Equal("API key must be 64 hex characters.", outcome.StatusText);
        Assert.Equal(0, probes);
        Assert.Null(store.ReadApiKey());
    }

    [Fact]
    public async Task SurroundingWhitespaceIsTrimmedBeforeProbeAndSave()
    {
        string? probed = null;
        var store = new FakeSecretStore();
        var outcome = await ApiKeySaveFlow.SaveAsync(
            "\t " + ValidKey + " \r\n", store, DenyTestKey,
            (key, _) => { probed = key; return Task.FromResult(ApiKeyValidationStatus.Valid); },
            onChecking: null, CancellationToken.None);

        Assert.True(outcome.Saved);
        Assert.Equal(ValidKey, probed);
        Assert.Equal(ValidKey, store.ReadApiKey());
    }

    [Fact]
    public async Task AValidKeyProbesExactlyOnceThenSaves()
    {
        var probes = 0;
        var store = new FakeSecretStore();
        var outcome = await ApiKeySaveFlow.SaveAsync(
            ValidKey, store, DenyTestKey,
            (_, _) => { probes++; return Task.FromResult(ApiKeyValidationStatus.Valid); },
            onChecking: null, CancellationToken.None);

        Assert.True(outcome.Saved);
        Assert.Equal("API key saved.", outcome.StatusText);
        Assert.Equal(1, probes);
        Assert.Equal(ValidKey, store.ReadApiKey());
    }

    [Fact]
    public async Task ARejectedKeyIsNeverSaved()
    {
        var store = new FakeSecretStore();
        var outcome = await ApiKeySaveFlow.SaveAsync(
            ValidKey, store, DenyTestKey,
            (_, _) => Task.FromResult(ApiKeyValidationStatus.Rejected),
            onChecking: null, CancellationToken.None);

        Assert.False(outcome.Saved);
        Assert.Equal("VirusTotal rejected this key. Check that you copied the full key.", outcome.StatusText);
        Assert.Null(store.ReadApiKey());
    }

    [Fact]
    public async Task ANetworkErrorStillSavesOptimistically()
    {
        var store = new FakeSecretStore();
        var outcome = await ApiKeySaveFlow.SaveAsync(
            ValidKey, store, DenyTestKey,
            (_, _) => Task.FromResult(ApiKeyValidationStatus.NetworkError),
            onChecking: null, CancellationToken.None);

        Assert.True(outcome.Saved);
        Assert.Equal(
            "Could not reach VirusTotal to validate. Key saved and will be verified on first upload.",
            outcome.StatusText);
        Assert.Equal(ValidKey, store.ReadApiKey());
    }

    [Fact]
    public async Task AProbeExceptionIsTreatedAsANetworkError()
    {
        var store = new FakeSecretStore();
        var outcome = await ApiKeySaveFlow.SaveAsync(
            ValidKey, store, DenyTestKey,
            (_, _) => Task.FromException<ApiKeyValidationStatus>(new HttpRequestException("offline")),
            onChecking: null, CancellationToken.None);

        Assert.True(outcome.Saved);
        Assert.StartsWith("Could not reach VirusTotal", outcome.StatusText, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ACanceledProbeAbortsWithoutSaving()
    {
        var store = new FakeSecretStore();
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        var outcome = await ApiKeySaveFlow.SaveAsync(
            ValidKey, store, DenyTestKey,
            (_, token) => Task.FromException<ApiKeyValidationStatus>(new OperationCanceledException(token)),
            onChecking: null, cts.Token);

        Assert.False(outcome.Saved);
        Assert.Equal("API key check canceled. Key was not saved.", outcome.StatusText);
        Assert.Null(store.ReadApiKey());
    }

    [Fact]
    public async Task ATimedOutProbeIsTreatedAsANetworkErrorNotACancellation()
    {
        var store = new FakeSecretStore();
        var outcome = await ApiKeySaveFlow.SaveAsync(
            ValidKey, store, DenyTestKey,
            (_, _) => Task.FromException<ApiKeyValidationStatus>(
                new TaskCanceledException("The request was canceled due to the configured HttpClient.Timeout.")),
            onChecking: null, CancellationToken.None);

        Assert.True(outcome.Saved);
        Assert.Equal(
            "Could not reach VirusTotal to validate. Key saved and will be verified on first upload.",
            outcome.StatusText);
        Assert.Equal(ValidKey, store.ReadApiKey());
    }

    [Fact]
    public async Task TheTestKeySkipsFormatAndProbeUnderAnAllowingPolicy()
    {
        var probes = 0;
        var store = new FakeSecretStore();
        var outcome = await ApiKeySaveFlow.SaveAsync(
            "testdev", store, AllowTestKey,
            (_, _) => { probes++; return Task.FromResult(ApiKeyValidationStatus.Valid); },
            onChecking: null, CancellationToken.None);

        Assert.True(outcome.Saved);
        Assert.Equal(0, probes);
        Assert.Equal("testdev", store.ReadApiKey());
    }

    [Fact]
    public async Task TheTestKeyIsAnOrdinaryBadKeyUnderADenyingPolicy()
    {
        var store = new FakeSecretStore();
        var outcome = await ApiKeySaveFlow.SaveAsync(
            "testdev", store, DenyTestKey, validateApiKey: null, onChecking: null, CancellationToken.None);

        Assert.False(outcome.Saved);
        Assert.Equal("API key must be 64 hex characters.", outcome.StatusText);
        Assert.Null(store.ReadApiKey());
    }

    [Fact]
    public async Task WithoutAProbeAValidKeySavesDirectly()
    {
        var store = new FakeSecretStore();
        var outcome = await ApiKeySaveFlow.SaveAsync(
            ValidKey, store, DenyTestKey, validateApiKey: null, onChecking: null, CancellationToken.None);

        Assert.True(outcome.Saved);
        Assert.Equal("API key saved.", outcome.StatusText);
    }

    [Fact]
    public async Task AFailingStoreSurfacesTheWriteError()
    {
        var store = new FakeSecretStore { ThrowOnSave = true };
        var outcome = await ApiKeySaveFlow.SaveAsync(
            ValidKey, store, DenyTestKey, validateApiKey: null, onChecking: null, CancellationToken.None);

        Assert.False(outcome.Saved);
        Assert.Equal("Could not save API key: secret store unavailable", outcome.StatusText);
    }

    [Fact]
    public async Task TheCheckingCallbackFiresBeforeTheProbe()
    {
        var order = new List<string>();
        var store = new FakeSecretStore();
        await ApiKeySaveFlow.SaveAsync(
            ValidKey, store, DenyTestKey,
            (_, _) => { order.Add("probe"); return Task.FromResult(ApiKeyValidationStatus.Valid); },
            onChecking: status => { order.Add("checking:" + status); return Task.CompletedTask; },
            CancellationToken.None);

        Assert.Equal(["checking:Checking VirusTotal API key...", "probe"], order);
    }
}

using System.Text;

namespace VirusTotalOneClick.Core.Tests;

public class DpapiFileSecretStoreTests
{
    [Fact]
    public void SaveReadDeleteRoundTripsThroughAProtectedFile()
    {
        using var folder = new TemporaryFolder();
        var store = new DpapiFileSecretStore(folder.Path);
        var blobPath = Path.Combine(folder.Path, "apikey.bin");

        store.SaveApiKey("0123456789abcdef");

        Assert.True(File.Exists(blobPath));
        var rawOnDisk = Encoding.Latin1.GetString(File.ReadAllBytes(blobPath));
        Assert.DoesNotContain("0123456789abcdef", rawOnDisk, StringComparison.Ordinal);
        Assert.Equal("0123456789abcdef", store.ReadApiKey());

        store.DeleteApiKey();

        Assert.False(File.Exists(blobPath));
        Assert.Null(store.ReadApiKey());
    }

    [Fact]
    public void SaveOverwritesThePreviousKey()
    {
        using var folder = new TemporaryFolder();
        var store = new DpapiFileSecretStore(folder.Path);

        store.SaveApiKey("first-key");
        store.SaveApiKey("second-key");

        Assert.Equal("second-key", store.ReadApiKey());
    }

    [Fact]
    public void ReadReturnsNullWhenNoKeyWasEverSaved()
    {
        using var folder = new TemporaryFolder();

        Assert.Null(new DpapiFileSecretStore(folder.Path).ReadApiKey());
    }

    [Fact]
    public void CorruptBlobReadsAsNullAndTheFileIsRemoved()
    {
        using var folder = new TemporaryFolder();
        var store = new DpapiFileSecretStore(folder.Path);
        var blobPath = folder.CreateFile("apikey.bin", "this is not a dpapi blob");

        Assert.Null(store.ReadApiKey());
        Assert.False(File.Exists(blobPath));
        Assert.Null(store.ReadApiKey());
    }

    [Fact]
    public void LockedBlobReadsAsNullAndTheFileIsPreserved()
    {
        using var folder = new TemporaryFolder();
        var store = new DpapiFileSecretStore(folder.Path);
        store.SaveApiKey("locked-key");
        var blobPath = Path.Combine(folder.Path, "apikey.bin");
        var originalBytes = File.ReadAllBytes(blobPath);

        // FileShare.Delete: blocks the read (transient lock, e.g. a scanner holding
        // the file open) while still permitting a delete, so this reproduces the
        // failure mode without masking it behind a delete that would fail anyway.
        using (new FileStream(blobPath, FileMode.Open, FileAccess.Read, FileShare.Delete))
        {
            Assert.Null(store.ReadApiKey());
        }

        Assert.True(File.Exists(blobPath));
        Assert.Equal(originalBytes, File.ReadAllBytes(blobPath));
        Assert.Equal("locked-key", store.ReadApiKey());
    }

    [Fact]
    public void DeleteIsANoOpWhenNothingIsStored()
    {
        using var folder = new TemporaryFolder();
        var store = new DpapiFileSecretStore(folder.Path);

        store.DeleteApiKey();

        Assert.Null(store.ReadApiKey());
    }

    [Fact]
    public void SaveCreatesTheAppDataDirectoryWhenMissing()
    {
        using var folder = new TemporaryFolder();
        var nested = Path.Combine(folder.Path, "not-yet-created");
        var store = new DpapiFileSecretStore(nested);

        store.SaveApiKey("some-key");

        Assert.Equal("some-key", store.ReadApiKey());
        Assert.True(File.Exists(Path.Combine(nested, "apikey.bin")));
    }

    [Fact]
    public void SecretStoreInterfaceExposesExactlyThreeOperations()
    {
        Assert.Equal(
            new[] { "DeleteApiKey", "ReadApiKey", "SaveApiKey" },
            typeof(ISecretStore).GetMethods().Select(method => method.Name).OrderBy(name => name, StringComparer.Ordinal).ToArray());
    }
}

namespace VirusTotalOneClick.Core.Tests;

public class ShellIntegrationServiceTests
{
    [Fact]
    public void CommandQuotesTheExecutableAndPassesTheSelectedPath()
    {
        var command = ShellIntegrationService.BuildCommand(@"C:\Apps\VirusTotalOneClick.exe");

        Assert.Equal("\"C:\\Apps\\VirusTotalOneClick.exe\" upload --path \"%1\"", command);
    }

    [Fact]
    public void ExecutablePathsContainingQuotesAreRejected()
    {
        Assert.Throws<ArgumentException>(
            () => ShellIntegrationService.BuildCommand("C:\\Apps\\evil\" whoami \".exe"));
    }

    [Fact]
    public void RegistryEntriesCoverFilesAndFoldersWithLabelIconModelAndCommand()
    {
        var options = new ShellIntegrationOptions(@"C:\Apps\VirusTotalOneClick.exe", "Upload to VirusTotal");

        var entries = ShellIntegrationService.BuildRegistryEntries(options);

        var expectedCommand = "\"C:\\Apps\\VirusTotalOneClick.exe\" upload --path \"%1\"";
        var expectedIcon = "\"C:\\Apps\\VirusTotalOneClick.exe\",0";
        Assert.Equal(
        [
            new ShellRegistryEntry(@"Software\Classes\*\shell\VirusTotalOneClick", null, "Upload to VirusTotal"),
            new ShellRegistryEntry(@"Software\Classes\*\shell\VirusTotalOneClick", "Icon", expectedIcon),
            new ShellRegistryEntry(@"Software\Classes\*\shell\VirusTotalOneClick", "MultiSelectModel", "Document"),
            new ShellRegistryEntry(@"Software\Classes\*\shell\VirusTotalOneClick\command", null, expectedCommand),
            new ShellRegistryEntry(@"Software\Classes\Directory\shell\VirusTotalOneClick", null, "Upload to VirusTotal"),
            new ShellRegistryEntry(@"Software\Classes\Directory\shell\VirusTotalOneClick", "Icon", expectedIcon),
            new ShellRegistryEntry(@"Software\Classes\Directory\shell\VirusTotalOneClick", "MultiSelectModel", "Document"),
            new ShellRegistryEntry(@"Software\Classes\Directory\shell\VirusTotalOneClick\command", null, expectedCommand),
        ], entries);
    }

    [Fact]
    public void RegistryValueNamesNormalizeNullToTheDefaultValueConvention()
    {
        Assert.Equal(string.Empty, CurrentUserRegistryWriter.NormalizeValueName(null));
        Assert.Equal("Icon", CurrentUserRegistryWriter.NormalizeValueName("Icon"));
    }
}

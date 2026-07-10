using System.Security.Cryptography;

namespace VirusTotalOneClick.Core.Tests;

public class AnonymizerServiceTests
{
    private static readonly AnonymizerOptions ScrubOn = new(ScrubSubmittedFilename: true, ScrubZipInternalPaths: false);
    private static readonly AnonymizerOptions ScrubOff = new(ScrubSubmittedFilename: false, ScrubZipInternalPaths: false);

    [Fact]
    public void ScrubbedNameUsesContentHashAndKeepsTheExtension()
    {
        using var folder = new TemporaryFolder();
        var path = folder.CreateFile("QuarterlyNumbers.pdf", "not really a pdf");
        var expectedShortId = ComputeExpectedShortId(path);

        var item = AnonymizerService.PrepareFile(path, ScrubOn);

        Assert.Equal($"sample-{expectedShortId}.pdf", item.SubmittedName);
    }

    [Fact]
    public void ScrubbedNameFallsBackToBinForExtensionlessFiles()
    {
        using var folder = new TemporaryFolder();
        var path = folder.CreateFile("README", "plain text");

        var item = AnonymizerService.PrepareFile(path, ScrubOn);

        Assert.Matches("^sample-[0-9a-f]{8}\\.bin$", item.SubmittedName);
    }

    [Fact]
    public void ScrubbedNameLowercasesTheExtension()
    {
        using var folder = new TemporaryFolder();
        var path = folder.CreateFile("Tool.EXE", "binary");

        var item = AnonymizerService.PrepareFile(path, ScrubOn);

        Assert.EndsWith(".exe", item.SubmittedName, StringComparison.Ordinal);
        Assert.DoesNotContain("EXE", item.SubmittedName, StringComparison.Ordinal);
    }

    [Fact]
    public void ScrubbingOffKeepsTheOriginalFileName()
    {
        using var folder = new TemporaryFolder();
        var path = folder.CreateFile("QuarterlyNumbers.pdf", "content");

        var item = AnonymizerService.PrepareFile(path, ScrubOff);

        Assert.Equal("QuarterlyNumbers.pdf", item.SubmittedName);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void UploadPathIsAlwaysTheOriginalFileAndBytesAreNeverTouched(bool scrub)
    {
        using var folder = new TemporaryFolder();
        var path = folder.CreateFile("sample.bin", "original bytes");
        var before = File.ReadAllBytes(path);
        var options = new AnonymizerOptions(ScrubSubmittedFilename: scrub, ScrubZipInternalPaths: false);

        var item = AnonymizerService.PrepareFile(path, options);

        Assert.Equal(path, item.OriginalPath);
        Assert.Equal(path, item.UploadPath);
        Assert.Equal(before, File.ReadAllBytes(path));
    }

    [Fact]
    public void ScrubbedNameLeaksNothingFromTheOriginalName()
    {
        using var folder = new TemporaryFolder();
        var path = folder.CreateFile("JohnsonPayrollQ3.xlsx", "cells");

        var item = AnonymizerService.PrepareFile(path, ScrubOn);

        Assert.Matches("^sample-[0-9a-f]{8}\\.xlsx$", item.SubmittedName);
        Assert.DoesNotContain("johnson", item.SubmittedName, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("payroll", item.SubmittedName, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("q3", item.SubmittedName, StringComparison.OrdinalIgnoreCase);
    }

    private static string ComputeExpectedShortId(string path)
    {
        using var stream = File.OpenRead(path);
        return Convert.ToHexStringLower(SHA256.HashData(stream))[..8];
    }
}

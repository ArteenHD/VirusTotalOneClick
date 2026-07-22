using VirusTotalOneClick.Core;
using VirusTotalOneClick.UI;

namespace VirusTotalOneClick.AppTests;

public class WindowFactoryTests
{
    private static AppComposition Composition(
        AppSettings? settings = null,
        ISecretStore? secretStore = null,
        Func<IReadOnlyList<UploadHistoryEntry>>? readHistory = null,
        Func<IReadOnlyList<string>, Func<CancellationToken, Task<FileUploadWorkflowResult>>>? createUploadAction = null,
        Func<FolderZipUploadRequest, Func<CancellationToken, Task<FileUploadWorkflowResult>>>? createZipUploadAction = null,
        bool allowTestKey = true)
        => new()
        {
            Options = new CommandLineOptions(AppMode.Interactive, []),
            Settings = settings ?? new AppSettings(),
            SaveSettings = _ => { },
            SecretStore = secretStore ?? new FakeSecretStore(),
            ClearHistory = () => { },
            ReadHistory = readHistory ?? (() => []),
            ApplyIntegration = _ => { },
            CreateUploadAction = createUploadAction
                ?? (paths => _ => Task.FromResult(new FileUploadWorkflowResult(FileUploadWorkflowStatus.Completed, paths.Count))),
            CreateZipUploadAction = createZipUploadAction
                ?? (_ => _ => Task.FromResult(new FileUploadWorkflowResult(FileUploadWorkflowStatus.Completed, 1))),
            ValidateApiKey = (_, _) => Task.FromResult(ApiKeyValidationStatus.Valid),
            ApiKeyValidator = new ApiKeyValidator(allowTestKey),
        };

    private static void WaitForScan(System.Windows.Window window)
    {
        if (window is FolderReviewWindow review)
        {
            review.ScanCompletion.GetAwaiter().GetResult();
        }
    }

    [Fact]
    public void SettingsAndHistoryAndDefaultModesRouteToTheirWindows()
    {
        StaTestRunner.Run(() =>
        {
            var factory = new WindowFactory(Composition());

            var settings = factory.Create(new CommandLineOptions(AppMode.Settings, []));
            using var closerA = new WindowCloser(settings);
            Assert.IsType<SettingsWindow>(settings);

            var history = factory.Create(new CommandLineOptions(AppMode.History, []));
            using var closerB = new WindowCloser(history);
            Assert.IsType<HistoryWindow>(history);

            var setup = factory.Create(new CommandLineOptions(AppMode.Interactive, []));
            using var closerC = new WindowCloser(setup);
            Assert.IsType<SetupWindow>(setup);
        });
    }

    [Fact]
    public void HistoryDisabledInSettingsShowsTheDisabledCaption()
    {
        StaTestRunner.Run(() =>
        {
            var factory = new WindowFactory(Composition(
                settings: new AppSettings { KeepLocalHistory = false }));

            var window = (HistoryWindow)factory.Create(new CommandLineOptions(AppMode.History, []));
            using var closer = new WindowCloser(window);

            Assert.Equal("Local history is disabled.", window.ViewModel.CaptionText);
        });
    }

    [Fact]
    public void HistoryEntriesComeFromTheComposedReader()
    {
        StaTestRunner.Run(() =>
        {
            var entry = new UploadHistoryEntry(
                @"C:\logged.txt", "logged.txt", new string('a', 64),
                DateTimeOffset.UtcNow, "file", "uploaded", "https://www.virustotal.com/gui/file/x");
            var factory = new WindowFactory(Composition(readHistory: () => [entry]));

            var window = (HistoryWindow)factory.Create(new CommandLineOptions(AppMode.History, []));
            using var closer = new WindowCloser(window);

            Assert.Equal(1, window.EntryCount);
            Assert.Equal(@"C:\logged.txt", window.FirstOriginalPathText);
        });
    }

    [Fact]
    public void SettingsOpenHistoryButtonReachesTheSameDisabledCaption()
    {
        StaTestRunner.Run(() =>
        {
            var factory = new WindowFactory(Composition(
                settings: new AppSettings { KeepLocalHistory = false }));

            var settings = (SettingsWindow)factory.Create(new CommandLineOptions(AppMode.Settings, []));
            using var closer = new WindowCloser(settings);

            HistoryWindow? opened = null;
            try
            {
                settings.ViewModel.OpenHistoryCommand.Execute(null);
                opened = System.Windows.Application.Current.Windows.OfType<HistoryWindow>().Single();
                Assert.Equal("Local history is disabled.", opened.ViewModel.CaptionText);
            }
            finally
            {
                opened?.Close();
            }
        });
    }

    [Fact]
    public void ALooseFileUploadOpensTheProgressWindowAutoStarting()
    {
        StaTestRunner.Run(() =>
        {
            using var folder = new TemporaryFolder();
            var file = folder.CreateFile("loose.txt", "content");
            IReadOnlyList<string>? actionPaths = null;
            var factory = new WindowFactory(Composition(
                createUploadAction: paths =>
                {
                    actionPaths = paths;
                    return _ => Task.FromResult(new FileUploadWorkflowResult(FileUploadWorkflowStatus.Completed, paths.Count));
                }));

            var window = factory.Create(new CommandLineOptions(AppMode.Upload, [file]));
            using var closer = new WindowCloser(window);

            var progress = Assert.IsType<UploadProgressWindow>(window);
            Assert.True(progress.AutoStartsUpload);
            Assert.Equal([file], actionPaths);
            Assert.Equal(file, progress.ViewModel.Items.Single().OriginalPath);
        });
    }

    [Fact]
    public void ConfirmBeforeFileUploadDisablesAutoStart()
    {
        StaTestRunner.Run(() =>
        {
            using var folder = new TemporaryFolder();
            var file = folder.CreateFile("confirm.txt", "content");
            var factory = new WindowFactory(Composition(
                settings: new AppSettings { ConfirmBeforeFileUpload = true }));

            var progress = (UploadProgressWindow)factory.Create(new CommandLineOptions(AppMode.Upload, [file]));
            using var closer = new WindowCloser(progress);

            Assert.False(progress.AutoStartsUpload);
        });
    }

    [Fact]
    public void AFolderUploadOpensTheReviewWindowWithItsContents()
    {
        StaTestRunner.Run(() =>
        {
            using var folder = new TemporaryFolder();
            folder.CreateFile("inside.txt", "content");
            var factory = new WindowFactory(Composition());

            var window = factory.Create(new CommandLineOptions(AppMode.Upload, [folder.Path]));
            using var closer = new WindowCloser(window);
            WaitForScan(window);

            var review = Assert.IsType<FolderReviewWindow>(window);
            Assert.Equal(1, review.CandidateFileCount);
        });
    }

    [Fact]
    public void AMixedSelectionReviewsTheLooseFileAndEveryFolder()
    {
        StaTestRunner.Run(() =>
        {
            using var folder = new TemporaryFolder();
            var loose = folder.CreateFile("loose.txt", "a");
            var dirA = Directory.CreateDirectory(Path.Combine(folder.Path, "first")).FullName;
            var dirB = Directory.CreateDirectory(Path.Combine(folder.Path, "second")).FullName;
            File.WriteAllText(Path.Combine(dirA, "a.txt"), "a");
            File.WriteAllText(Path.Combine(dirB, "b.txt"), "b");
            var factory = new WindowFactory(Composition());

            var review = (FolderReviewWindow)factory.Create(
                new CommandLineOptions(AppMode.Upload, [loose, dirA, dirB]));
            using var closer = new WindowCloser(review);
            WaitForScan(review);

            Assert.Equal(3, review.CandidateFileCount);
            var selected = review.RootNode!.SelectedFiles().Select(node => node.FullPath).ToList();
            Assert.Contains(loose, selected);
            Assert.Contains(Path.Combine(dirA, "a.txt"), selected);
            Assert.Contains(Path.Combine(dirB, "b.txt"), selected);
        });
    }

    [Fact]
    public void TheConfiguredFolderModeDrivesTheReviewDefaultAction()
    {
        StaTestRunner.Run(() =>
        {
            using var folder = new TemporaryFolder();
            folder.CreateFile("inside.txt", "content");
            var factory = new WindowFactory(Composition(
                settings: new AppSettings { DefaultFolderMode = FolderUploadMode.ReviewThenZip }));

            var review = (FolderReviewWindow)factory.Create(new CommandLineOptions(AppMode.Upload, [folder.Path]));
            using var closer = new WindowCloser(review);
            WaitForScan(review);

            Assert.Equal("Zip selected items and upload", review.DefaultActionText);
        });
    }

    [Fact]
    public void ATruncatedFactoryScanNamesTheConfiguredLimit()
    {
        StaTestRunner.Run(() =>
        {
            using var folder = new TemporaryFolder();
            folder.CreateFile("one.txt", "1");
            folder.CreateFile("two.txt", "2");
            var factory = new WindowFactory(
                Composition(),
                folderScanOptions: new FolderScanOptions(MaxFiles: 1));

            var review = (FolderReviewWindow)factory.Create(new CommandLineOptions(AppMode.Upload, [folder.Path]));
            using var closer = new WindowCloser(review);
            WaitForScan(review);

            Assert.True(review.ViewModel.HasTruncationWarning);
            Assert.Contains("first 1 files", review.ViewModel.TruncationWarningText, StringComparison.Ordinal);
        });
    }

    [Fact]
    public void ExactlyAtCapAcrossSelectionsWithATrailingEmptyFolderIsNotTruncated()
    {
        StaTestRunner.Run(() =>
        {
            using var folder = new TemporaryFolder();
            var dirA = Directory.CreateDirectory(Path.Combine(folder.Path, "first")).FullName;
            File.WriteAllText(Path.Combine(dirA, "a.txt"), "a");
            File.WriteAllText(Path.Combine(dirA, "b.txt"), "b");
            var dirB = Directory.CreateDirectory(Path.Combine(folder.Path, "second")).FullName;
            var factory = new WindowFactory(
                Composition(),
                folderScanOptions: new FolderScanOptions(MaxFiles: 2));

            var review = (FolderReviewWindow)factory.Create(new CommandLineOptions(AppMode.Upload, [dirA, dirB]));
            using var closer = new WindowCloser(review);
            WaitForScan(review);

            Assert.False(review.ViewModel.HasTruncationWarning);
            Assert.Equal(2, review.CandidateFileCount);
        });
    }

    [Fact]
    public void OneMoreFileInATrailingFolderAfterTheCapIsTruncated()
    {
        StaTestRunner.Run(() =>
        {
            using var folder = new TemporaryFolder();
            var dirA = Directory.CreateDirectory(Path.Combine(folder.Path, "first")).FullName;
            File.WriteAllText(Path.Combine(dirA, "a.txt"), "a");
            File.WriteAllText(Path.Combine(dirA, "b.txt"), "b");
            var dirB = Directory.CreateDirectory(Path.Combine(folder.Path, "second")).FullName;
            File.WriteAllText(Path.Combine(dirB, "c.txt"), "c");
            var factory = new WindowFactory(
                Composition(),
                folderScanOptions: new FolderScanOptions(MaxFiles: 2));

            var review = (FolderReviewWindow)factory.Create(new CommandLineOptions(AppMode.Upload, [dirA, dirB]));
            using var closer = new WindowCloser(review);
            WaitForScan(review);

            Assert.True(review.ViewModel.HasTruncationWarning);
            Assert.Equal(2, review.CandidateFileCount);
        });
    }

    [Fact]
    public void ALooseFileSelectionAfterTheCapIsTreatedAsADroppedFile()
    {
        StaTestRunner.Run(() =>
        {
            using var folder = new TemporaryFolder();
            var dirA = Directory.CreateDirectory(Path.Combine(folder.Path, "first")).FullName;
            File.WriteAllText(Path.Combine(dirA, "a.txt"), "a");
            File.WriteAllText(Path.Combine(dirA, "b.txt"), "b");
            var looseFile = folder.CreateFile("loose.txt", "content");
            var factory = new WindowFactory(
                Composition(),
                folderScanOptions: new FolderScanOptions(MaxFiles: 2));

            var review = (FolderReviewWindow)factory.Create(new CommandLineOptions(AppMode.Upload, [dirA, looseFile]));
            using var closer = new WindowCloser(review);
            WaitForScan(review);

            Assert.True(review.ViewModel.HasTruncationWarning);
            Assert.Equal(2, review.CandidateFileCount);
        });
    }

    [Fact]
    public void ReviewUploadHandsOffToARealProgressWindowAndClosesTheReview()
    {
        StaTestRunner.Run(() =>
        {
            using var folder = new TemporaryFolder();
            folder.CreateFile("inside.txt", "content");
            var factory = new WindowFactory(Composition());

            var review = (FolderReviewWindow)factory.Create(new CommandLineOptions(AppMode.Upload, [folder.Path]));
            WaitForScan(review);
            review.Show();

            UploadProgressWindow? progress = null;
            try
            {
                review.UploadSelectedFilesAsync(CancellationToken.None).GetAwaiter().GetResult();
                progress = System.Windows.Application.Current.Windows.OfType<UploadProgressWindow>().Single();

                Assert.True(progress.AutoStartsUpload);
                Assert.Single(progress.ViewModel.Items);
                Assert.False(review.IsVisible);
            }
            finally
            {
                progress?.Close();
                if (review.IsVisible)
                {
                    review.Close();
                }
            }
        });
    }

    [Fact]
    public void ReviewZipHandsOffASingleFolderRowToTheProgressWindow()
    {
        StaTestRunner.Run(() =>
        {
            using var folder = new TemporaryFolder();
            folder.CreateFile("inside.txt", "content");
            var factory = new WindowFactory(Composition(
                createZipUploadAction: request => _ => Task.FromResult(new FileUploadWorkflowResult(
                    FileUploadWorkflowStatus.Completed,
                    1,
                    ItemResults:
                    [
                        new FileUploadWorkflowItemResult(
                            request.SourceRootPath, "bundle.zip", "uploaded",
                            "https://www.virustotal.com/gui/file/zip"),
                    ]))));

            var review = (FolderReviewWindow)factory.Create(new CommandLineOptions(AppMode.Upload, [folder.Path]));
            WaitForScan(review);
            review.Show();

            UploadProgressWindow? progress = null;
            try
            {
                review.ZipSelectedItemsAsync(CancellationToken.None).GetAwaiter().GetResult();
                progress = System.Windows.Application.Current.Windows.OfType<UploadProgressWindow>().Single();

                var row = Assert.Single(progress.ViewModel.Items);

                // Showing the review's handed-off progress window already ran the
                // auto-start to completion synchronously (Loaded fires during
                // Show() on this harness's STA thread); TryTriggerAutoStart is
                // therefore a safe no-op here rather than the first trigger.
                progress.TryTriggerAutoStart()?.GetAwaiter().GetResult();
                Assert.Equal("Uploaded", row.Status);
                Assert.Equal("https://www.virustotal.com/gui/file/zip", row.ResultUrl);
            }
            finally
            {
                progress?.Close();
                if (review.IsVisible)
                {
                    review.Close();
                }
            }
        });
    }

    [Fact]
    public void DevModeFollowsTheComposedValidatorPolicy()
    {
        StaTestRunner.Run(() =>
        {
            using var folder = new TemporaryFolder();
            var file = folder.CreateFile("dev.txt", "content");
            var secretStore = new FakeSecretStore();
            secretStore.SaveApiKey("testdev");

            var allowing = new WindowFactory(Composition(secretStore: secretStore, allowTestKey: true));
            var progressDev = (UploadProgressWindow)allowing.Create(new CommandLineOptions(AppMode.Upload, [file]));
            using var closerA = new WindowCloser(progressDev);
            Assert.True(progressDev.IsDevMode);

            var strict = new WindowFactory(Composition(secretStore: secretStore, allowTestKey: false));
            var progressStrict = (UploadProgressWindow)strict.Create(new CommandLineOptions(AppMode.Upload, [file]));
            using var closerB = new WindowCloser(progressStrict);
            Assert.False(progressStrict.IsDevMode);
        });
    }

    [Fact]
    public void BlankPathsAreFilteredBeforePlanning()
    {
        StaTestRunner.Run(() =>
        {
            using var folder = new TemporaryFolder();
            var file = folder.CreateFile("real.txt", "content");
            var factory = new WindowFactory(Composition());

            var progress = (UploadProgressWindow)factory.Create(
                new CommandLineOptions(AppMode.Upload, ["", "   ", file]));
            using var closer = new WindowCloser(progress);

            Assert.Single(progress.ViewModel.Items);
        });
    }
}

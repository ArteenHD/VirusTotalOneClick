namespace VirusTotalOneClick.AppTests;

public class CoalescedUploadSessionTests
{
    private sealed class CollectingTraceListener : System.Diagnostics.TraceListener
    {
        public List<string> Messages { get; } = [];

        public override void Write(string? message)
        {
        }

        public override void WriteLine(string? message) => Messages.Add(message ?? string.Empty);
    }

    private static string UniqueName() => "VirusTotalOneClick.Test." + Guid.NewGuid().ToString("N");

    private static async Task<IReadOnlyList<string>> AwaitBatch(
        TaskCompletionSource<IReadOnlyList<string>> signal, TimeSpan timeout)
    {
        var winner = await Task.WhenAny(signal.Task, Task.Delay(timeout));
        Assert.True(ReferenceEquals(winner, signal.Task), "Timed out waiting for a batch.");
        return await signal.Task;
    }

    [Fact]
    public void OnlyOneSessionBecomesPrimaryPerInstanceName()
    {
        var name = UniqueName();
        using var primary = new CoalescedUploadSession(name);
        using var secondary = new CoalescedUploadSession(name);

        Assert.True(primary.TryBecomePrimary());
        Assert.False(secondary.TryBecomePrimary());
    }

    [Fact]
    public async Task SeededPathsArriveAsTheFirstBatch()
    {
        var name = UniqueName();
        using var session = new CoalescedUploadSession(
            name,
            idleDebounce: TimeSpan.FromMilliseconds(50),
            maxBatchWindow: TimeSpan.FromMilliseconds(500));
        Assert.True(session.TryBecomePrimary());

        var firstBatch = new TaskCompletionSource<IReadOnlyList<string>>(TaskCreationOptions.RunContinuationsAsynchronously);
        session.SeedAndPump([@"C:\seed.txt"], batch => firstBatch.TrySetResult(batch));

        Assert.Equal([@"C:\seed.txt"], await AwaitBatch(firstBatch, TimeSpan.FromSeconds(10)));
    }

    [Fact]
    public async Task AForwardedInvocationMergesIntoThePrimaryBatch()
    {
        var name = UniqueName();
        // The idle debounce is the window the secondary's pipe connect+write
        // must fit inside for the paths to merge into one batch; a loaded
        // machine can take well over 200ms for that roundtrip.
        using var primary = new CoalescedUploadSession(
            name,
            idleDebounce: TimeSpan.FromMilliseconds(1500),
            maxBatchWindow: TimeSpan.FromSeconds(10));
        Assert.True(primary.TryBecomePrimary());

        var firstBatch = new TaskCompletionSource<IReadOnlyList<string>>(TaskCreationOptions.RunContinuationsAsynchronously);
        primary.SeedAndPump([@"C:\mine.txt"], batch => firstBatch.TrySetResult(batch));

        using var secondary = new CoalescedUploadSession(name);
        var forwarded = await secondary.TryForwardAsync([@"C:\theirs.txt"], CancellationToken.None);

        Assert.True(forwarded);
        var batch = await AwaitBatch(firstBatch, TimeSpan.FromSeconds(10));
        Assert.Contains(@"C:\mine.txt", batch);
        Assert.Contains(@"C:\theirs.txt", batch);
    }

    [Fact]
    public async Task ALateArrivalAfterABatchClosedStartsANewBatch()
    {
        var name = UniqueName();
        using var primary = new CoalescedUploadSession(
            name,
            idleDebounce: TimeSpan.FromMilliseconds(50),
            maxBatchWindow: TimeSpan.FromMilliseconds(500));
        Assert.True(primary.TryBecomePrimary());

        var batches = new System.Collections.Concurrent.BlockingCollection<IReadOnlyList<string>>();
        primary.SeedAndPump([@"C:\first.txt"], batches.Add);

        Assert.True(batches.TryTake(out var first, TimeSpan.FromSeconds(10)));
        Assert.Equal([@"C:\first.txt"], first);

        using var secondary = new CoalescedUploadSession(name);
        Assert.True(await secondary.TryForwardAsync([@"C:\late.txt"], CancellationToken.None));

        Assert.True(batches.TryTake(out var second, TimeSpan.FromSeconds(10)));
        Assert.Equal([@"C:\late.txt"], second);
    }

    [Fact]
    public async Task ForwardingWithoutAPrimaryFails()
    {
        using var secondary = new CoalescedUploadSession(UniqueName());

        Assert.False(await secondary.TryForwardAsync([@"C:\alone.txt"], CancellationToken.None));
    }

    [Fact]
    public async Task AFailingBatchDeliveryDoesNotStopLaterBatches()
    {
        var name = UniqueName();
        using var primary = new CoalescedUploadSession(
            name,
            idleDebounce: TimeSpan.FromMilliseconds(50),
            maxBatchWindow: TimeSpan.FromMilliseconds(500));
        Assert.True(primary.TryBecomePrimary());

        var deliveryCount = 0;
        var firstAttempted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var secondBatch = new TaskCompletionSource<IReadOnlyList<string>>(TaskCreationOptions.RunContinuationsAsynchronously);
        var listener = new CollectingTraceListener();
        System.Diagnostics.Trace.Listeners.Add(listener);
        try
        {
            primary.SeedAndPump([@"C:\first.txt"], batch =>
            {
                if (Interlocked.Increment(ref deliveryCount) == 1)
                {
                    firstAttempted.TrySetResult();
                    throw new InvalidOperationException("Simulated failure delivering the first batch.");
                }

                secondBatch.TrySetResult(batch);
            });

            var attemptWinner = await Task.WhenAny(firstAttempted.Task, Task.Delay(TimeSpan.FromSeconds(10)));
            Assert.True(ReferenceEquals(attemptWinner, firstAttempted.Task), "Timed out waiting for the first delivery attempt.");

            using var secondary = new CoalescedUploadSession(name);
            Assert.True(await secondary.TryForwardAsync([@"C:\second.txt"], CancellationToken.None));

            Assert.Equal([@"C:\second.txt"], await AwaitBatch(secondBatch, TimeSpan.FromSeconds(10)));

            Assert.Contains(
                listener.Messages,
                message => message.Contains("Simulated failure delivering the first batch.", StringComparison.Ordinal));
        }
        finally
        {
            System.Diagnostics.Trace.Listeners.Remove(listener);
        }
    }
}

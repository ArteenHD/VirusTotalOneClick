namespace VirusTotalOneClick.Core.Tests;

public class InvocationCoalescerTests
{
    private static readonly TimeSpan Idle = TimeSpan.FromMilliseconds(500);
    private static readonly TimeSpan MaxWindow = TimeSpan.FromSeconds(3);
    private static readonly DateTimeOffset Start = DateTimeOffset.UnixEpoch;

    private sealed class ManualClock
    {
        public DateTimeOffset Now { get; set; } = Start;
    }

    [Fact]
    public async Task ABatchFlushesOnceArrivalsGoIdle()
    {
        var clock = new ManualClock();
        var delays = new List<TimeSpan>();
        var coalescer = new InvocationCoalescer(
            Idle,
            MaxWindow,
            () => clock.Now,
            (delay, _) =>
            {
                delays.Add(delay);
                clock.Now += delay;
                return Task.CompletedTask;
            });

        coalescer.Add([@"C:\a.txt"]);
        var batch = await coalescer.WaitForBatchAsync(CancellationToken.None);

        Assert.Equal([@"C:\a.txt"], batch);
        Assert.Equal([Idle], delays);
    }

    [Fact]
    public async Task ALateArrivalExtendsTheQuietPeriod()
    {
        var clock = new ManualClock();
        var delayCount = 0;
        InvocationCoalescer coalescer = null!;
        coalescer = new InvocationCoalescer(
            Idle,
            MaxWindow,
            () => clock.Now,
            (delay, _) =>
            {
                if (delayCount++ == 0)
                {
                    // A second launch lands 200 ms into the first quiet period.
                    clock.Now += TimeSpan.FromMilliseconds(200);
                    coalescer.Add([@"C:\b.txt"]);
                }
                else
                {
                    clock.Now += delay;
                }

                return Task.CompletedTask;
            });

        coalescer.Add([@"C:\a.txt"]);
        var batch = await coalescer.WaitForBatchAsync(CancellationToken.None);

        Assert.Equal([@"C:\a.txt", @"C:\b.txt"], batch);
        Assert.Equal(2, delayCount);
        Assert.Equal(Start + TimeSpan.FromMilliseconds(700), clock.Now);
    }

    [Fact]
    public async Task ASteadyStreamStillFlushesAtTheBatchWindowCeiling()
    {
        var clock = new ManualClock();
        var next = 0;
        InvocationCoalescer coalescer = null!;
        coalescer = new InvocationCoalescer(
            Idle,
            MaxWindow,
            () => clock.Now,
            (delay, _) =>
            {
                if (clock.Now < Start + TimeSpan.FromMilliseconds(2800))
                {
                    // Arrivals keep landing every 400 ms — under the idle gap,
                    // so only the window ceiling can end the batch.
                    clock.Now += TimeSpan.FromMilliseconds(400);
                    coalescer.Add([$@"C:\file-{next++}.txt"]);
                }
                else
                {
                    clock.Now += delay;
                }

                return Task.CompletedTask;
            });

        coalescer.Add([@"C:\file-first.txt"]);
        var batch = await coalescer.WaitForBatchAsync(CancellationToken.None);

        Assert.Equal(Start + MaxWindow, clock.Now);
        Assert.Equal(8, batch.Count);
    }

    [Fact]
    public async Task ArrivalsAfterABatchIsTakenFormANewBatch()
    {
        var clock = new ManualClock();
        var coalescer = new InvocationCoalescer(
            Idle,
            MaxWindow,
            () => clock.Now,
            (delay, _) =>
            {
                clock.Now += delay;
                return Task.CompletedTask;
            });

        coalescer.Add([@"C:\first.txt"]);
        var first = await coalescer.WaitForBatchAsync(CancellationToken.None);
        coalescer.Add([@"C:\second.txt"]);
        var second = await coalescer.WaitForBatchAsync(CancellationToken.None);

        Assert.Equal([@"C:\first.txt"], first);
        Assert.Equal([@"C:\second.txt"], second);
    }

    [Fact]
    public async Task DuplicateAndBlankPathsAreDroppedWithinABatch()
    {
        var clock = new ManualClock();
        var coalescer = new InvocationCoalescer(
            Idle,
            MaxWindow,
            () => clock.Now,
            (delay, _) =>
            {
                clock.Now += delay;
                return Task.CompletedTask;
            });

        coalescer.Add([@"C:\a.txt", @"c:\A.TXT", "", "   ", @"C:\b.txt"]);
        coalescer.Add([@"C:\a.txt"]);
        var batch = await coalescer.WaitForBatchAsync(CancellationToken.None);

        Assert.Equal([@"C:\a.txt", @"C:\b.txt"], batch);
    }

    [Fact]
    public async Task WaitingBeforeAnyArrivalCompletesOnTheFirstAdd()
    {
        var clock = new ManualClock();
        var coalescer = new InvocationCoalescer(
            Idle,
            MaxWindow,
            () => clock.Now,
            (delay, _) =>
            {
                clock.Now += delay;
                return Task.CompletedTask;
            });

        var waiting = coalescer.WaitForBatchAsync(CancellationToken.None);
        Assert.False(waiting.IsCompleted);

        coalescer.Add([@"C:\a.txt"]);
        var batch = await waiting;

        Assert.Equal([@"C:\a.txt"], batch);
    }

    [Fact]
    public async Task CancellationStopsAnIdleWait()
    {
        var coalescer = new InvocationCoalescer(Idle, MaxWindow);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => coalescer.WaitForBatchAsync(cancellation.Token));
    }
}

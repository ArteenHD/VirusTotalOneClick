using System.Buffers.Binary;
using System.IO.Pipes;
using System.Security.AccessControl;
using System.Security.Principal;
using VirusTotalOneClick.Core;

namespace VirusTotalOneClick.AppTests;

public class SingleInstanceCoordinatorTests
{
    private static string UniqueName()
        => "VirusTotalOneClick.Tests." + Guid.NewGuid().ToString("N");

    private static InvocationCoalescer FastCoalescer()
        => new(TimeSpan.FromMilliseconds(100), TimeSpan.FromSeconds(5));

    private static CancellationToken TestTimeout()
        => new CancellationTokenSource(TimeSpan.FromSeconds(15)).Token;

    [Fact]
    public void OnlyTheFirstCoordinatorBecomesPrimary()
    {
        var name = UniqueName();
        using var first = new SingleInstanceCoordinator(name);
        using var second = new SingleInstanceCoordinator(name);

        Assert.True(first.TryBecomePrimary());
        Assert.False(second.TryBecomePrimary());
    }

    [Fact]
    public void DisposingThePrimaryFreesTheElectionForTheNextLaunch()
    {
        var name = UniqueName();
        var first = new SingleInstanceCoordinator(name);
        Assert.True(first.TryBecomePrimary());
        first.Dispose();

        using var second = new SingleInstanceCoordinator(name);
        Assert.True(second.TryBecomePrimary());
    }

    [Fact]
    public async Task ForwardedPathsReachThePrimaryBatch()
    {
        var name = UniqueName();
        using var primary = new SingleInstanceCoordinator(name);
        Assert.True(primary.TryBecomePrimary());
        var coalescer = FastCoalescer();
        primary.StartServer(coalescer);

        using var secondary = new SingleInstanceCoordinator(name);
        var forwarded = await secondary.TryForwardAsync([@"C:\from-secondary.txt"], TestTimeout());
        var batch = await coalescer.WaitForBatchAsync(TestTimeout());

        Assert.True(forwarded);
        Assert.Equal([@"C:\from-secondary.txt"], batch);
    }

    [Fact]
    public async Task TwoForwardsLandInOneBatch()
    {
        var name = UniqueName();
        using var primary = new SingleInstanceCoordinator(name);
        Assert.True(primary.TryBecomePrimary());
        var coalescer = FastCoalescer();
        primary.StartServer(coalescer);

        using var secondary = new SingleInstanceCoordinator(name);
        Assert.True(await secondary.TryForwardAsync([@"C:\one.txt"], TestTimeout()));
        Assert.True(await secondary.TryForwardAsync([@"C:\two.txt"], TestTimeout()));
        var batch = await coalescer.WaitForBatchAsync(TestTimeout());

        Assert.Equal([@"C:\one.txt", @"C:\two.txt"], batch);
    }

    [Fact]
    public async Task ForwardingWithoutAPrimaryFallsBackToStandalone()
    {
        using var secondary = new SingleInstanceCoordinator(
            UniqueName(),
            connectRetryDelays: [TimeSpan.Zero, TimeSpan.Zero],
            delayAsync: (_, _) => Task.CompletedTask);

        Assert.False(await secondary.TryForwardAsync([@"C:\a.txt"], TestTimeout()));
    }

    [Fact]
    public async Task ForwardingRetriesWithBackoffBeforeGivingUp()
    {
        var delays = new List<TimeSpan>();
        using var secondary = new SingleInstanceCoordinator(
            UniqueName(),
            connectRetryDelays: [TimeSpan.FromMilliseconds(1), TimeSpan.FromMilliseconds(2)],
            delayAsync: (delay, _) =>
            {
                delays.Add(delay);
                return Task.CompletedTask;
            });

        var forwarded = await secondary.TryForwardAsync([@"C:\a.txt"], TestTimeout());

        Assert.False(forwarded);
        Assert.Equal([TimeSpan.FromMilliseconds(1), TimeSpan.FromMilliseconds(2)], delays);
    }

    [Fact]
    public async Task AMalformedConnectionDoesNotStopLaterForwards()
    {
        var name = UniqueName();
        using var primary = new SingleInstanceCoordinator(name);
        Assert.True(primary.TryBecomePrimary());
        var coalescer = FastCoalescer();
        primary.StartServer(coalescer);

        using (var raw = new NamedPipeClientStream(".", name, PipeDirection.Out))
        {
            await raw.ConnectAsync(TestTimeout());
            var bogusHeader = new byte[4];
            BinaryPrimitives.WriteInt32LittleEndian(bogusHeader, int.MaxValue);
            await raw.WriteAsync(bogusHeader, TestTimeout());
        }

        using var secondary = new SingleInstanceCoordinator(name);
        var forwarded = await secondary.TryForwardAsync([@"C:\real.txt"], TestTimeout());
        var batch = await coalescer.WaitForBatchAsync(TestTimeout());

        Assert.True(forwarded);
        Assert.Equal([@"C:\real.txt"], batch);
    }

    [Fact]
    public async Task ASilentConnectionIsDroppedAndLaterForwardsSucceed()
    {
        var name = UniqueName();
        using var primary = new SingleInstanceCoordinator(
            name,
            connectionReadTimeout: TimeSpan.FromMilliseconds(200));
        Assert.True(primary.TryBecomePrimary());
        var coalescer = FastCoalescer();
        primary.StartServer(coalescer);

        using var silent = new NamedPipeClientStream(".", name, PipeDirection.Out);
        await silent.ConnectAsync(TestTimeout());

        using var secondary = new SingleInstanceCoordinator(
            name,
            connectRetryDelays: [.. Enumerable.Repeat(TimeSpan.FromMilliseconds(300), 5)]);
        var forwarded = await secondary.TryForwardAsync([@"C:\after-stall.txt"], TestTimeout());
        var batch = await coalescer.WaitForBatchAsync(TestTimeout());

        Assert.True(forwarded);
        Assert.Equal([@"C:\after-stall.txt"], batch);
    }

    [Fact]
    public void PipeAccessIsRestrictedToTheCurrentUser()
    {
        var security = SingleInstanceCoordinator.CreatePipeSecurity();
        var rules = security
            .GetAccessRules(includeExplicit: true, includeInherited: true, typeof(SecurityIdentifier))
            .Cast<PipeAccessRule>()
            .ToList();

        var rule = Assert.Single(rules);
        using var identity = WindowsIdentity.GetCurrent();
        Assert.Equal(identity.User, rule.IdentityReference);
        Assert.Equal(AccessControlType.Allow, rule.AccessControlType);
        Assert.Equal(PipeAccessRights.FullControl, rule.PipeAccessRights);
    }
}

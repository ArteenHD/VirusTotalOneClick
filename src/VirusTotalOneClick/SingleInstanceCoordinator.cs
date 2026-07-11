using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Security.AccessControl;
using System.Security.Principal;
using VirusTotalOneClick.Core;

namespace VirusTotalOneClick;

/// <summary>
/// Elects one primary instance per user session via a named mutex and moves
/// launch arguments from secondary processes to the primary over a named pipe
/// that only the current user can open.
/// </summary>
public sealed class SingleInstanceCoordinator : IDisposable
{
    private const int ConnectTimeoutMilliseconds = 250;

    private static readonly TimeSpan[] DefaultConnectRetryDelays =
    [
        TimeSpan.FromMilliseconds(100),
        TimeSpan.FromMilliseconds(200),
        TimeSpan.FromMilliseconds(400),
        TimeSpan.FromMilliseconds(800),
    ];

    private static readonly TimeSpan DefaultConnectionReadTimeout = TimeSpan.FromSeconds(5);

    private readonly string _instanceName;
    private readonly IReadOnlyList<TimeSpan> _connectRetryDelays;
    private readonly Func<TimeSpan, CancellationToken, Task> _delayAsync;
    private readonly TimeSpan _connectionReadTimeout;
    private readonly CancellationTokenSource _serverShutdown = new();
    private Mutex? _primaryMutex;
    private Task? _serverLoop;

    public SingleInstanceCoordinator(
        string? instanceName = null,
        IReadOnlyList<TimeSpan>? connectRetryDelays = null,
        Func<TimeSpan, CancellationToken, Task>? delayAsync = null,
        TimeSpan? connectionReadTimeout = null)
    {
        _instanceName = instanceName ?? DefaultInstanceName();
        _connectRetryDelays = connectRetryDelays ?? DefaultConnectRetryDelays;
        _delayAsync = delayAsync ?? Task.Delay;
        _connectionReadTimeout = connectionReadTimeout ?? DefaultConnectionReadTimeout;
    }

    public bool TryBecomePrimary()
    {
        var mutex = new Mutex(initiallyOwned: true, @"Local\" + _instanceName, out var createdNew);
        if (createdNew)
        {
            _primaryMutex = mutex;
            return true;
        }

        mutex.Dispose();
        return false;
    }

    public void StartServer(InvocationCoalescer coalescer)
    {
        _serverLoop = Task.Run(() => RunServerLoopAsync(coalescer, _serverShutdown.Token));
    }

    public async Task<bool> TryForwardAsync(IReadOnlyList<string> paths, CancellationToken cancellationToken)
    {
        for (var attempt = 0; ; attempt++)
        {
            try
            {
                using var client = new NamedPipeClientStream(".", _instanceName, PipeDirection.Out);
                await client.ConnectAsync(ConnectTimeoutMilliseconds, cancellationToken).ConfigureAwait(false);
                await InvocationProtocol
                    .WriteAsync(client, new CoalescedInvocation(paths), cancellationToken)
                    .ConfigureAwait(false);
                return true;
            }
            catch (Exception ex) when (ex is TimeoutException or IOException or UnauthorizedAccessException)
            {
                if (attempt >= _connectRetryDelays.Count)
                {
                    return false;
                }

                await _delayAsync(_connectRetryDelays[attempt], cancellationToken).ConfigureAwait(false);
            }
        }
    }

    public void Dispose()
    {
        _serverShutdown.Cancel();
        try
        {
            _serverLoop?.Wait(TimeSpan.FromSeconds(2));
        }
        catch (AggregateException)
        {
        }

        _serverShutdown.Dispose();
        _primaryMutex?.Dispose();
    }

    internal static PipeSecurity CreatePipeSecurity()
    {
        // Exactly one ACE: the launching user. No other account — local or
        // remote — can open the pipe and inject upload arguments.
        using var identity = WindowsIdentity.GetCurrent();
        var security = new PipeSecurity();
        security.AddAccessRule(new PipeAccessRule(
            identity.User!,
            PipeAccessRights.FullControl,
            AccessControlType.Allow));
        return security;
    }

    private async Task RunServerLoopAsync(InvocationCoalescer coalescer, CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                using var server = CreateServerStream();
                await server.WaitForConnectionAsync(cancellationToken).ConfigureAwait(false);

                // A silent client must not stall launch handoff: bound the read
                // so one connection can never wedge the only server instance.
                using var readWindow = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                readWindow.CancelAfter(_connectionReadTimeout);
                var invocation = await InvocationProtocol
                    .ReadAsync(server, readWindow.Token)
                    .ConfigureAwait(false);
                if (invocation is not null)
                {
                    coalescer.Add(invocation.Paths);
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception)
            {
                // The listener must outlive any single bad connection.
            }
        }
    }

    private NamedPipeServerStream CreateServerStream()
    {
        return NamedPipeServerStreamAcl.Create(
            _instanceName,
            PipeDirection.In,
            maxNumberOfServerInstances: 1,
            PipeTransmissionMode.Byte,
            PipeOptions.Asynchronous,
            inBufferSize: 0,
            outBufferSize: 0,
            CreatePipeSecurity());
    }

    private static string DefaultInstanceName()
    {
        // Pipes are machine-global, so the name itself is scoped to the user
        // and logon session; the ACL enforces the same boundary.
        using var identity = WindowsIdentity.GetCurrent();
        using var process = Process.GetCurrentProcess();
        var user = identity.User?.Value ?? "anonymous";
        return FormattableString.Invariant($"VirusTotalOneClick.{user}.{process.SessionId}");
    }
}

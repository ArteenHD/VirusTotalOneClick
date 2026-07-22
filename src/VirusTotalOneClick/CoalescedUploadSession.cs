using System.Diagnostics;
using VirusTotalOneClick.Core;

namespace VirusTotalOneClick;

/// <summary>
/// One upload launch's view of the single-instance protocol: elect a primary,
/// forward paths to it, or — as the primary — merge arrivals into debounced
/// batches and hand each batch to the window opener.
/// </summary>
public sealed class CoalescedUploadSession : IDisposable
{
    private static readonly TimeSpan DefaultIdleDebounce = TimeSpan.FromMilliseconds(300);
    private static readonly TimeSpan DefaultMaxBatchWindow = TimeSpan.FromSeconds(2);

    private readonly SingleInstanceCoordinator _coordinator;
    private readonly InvocationCoalescer _coalescer;
    private readonly CancellationTokenSource _shutdown = new();
    private Task? _pump;

    public CoalescedUploadSession(
        string? instanceName = null,
        TimeSpan? idleDebounce = null,
        TimeSpan? maxBatchWindow = null)
    {
        _coordinator = new SingleInstanceCoordinator(instanceName);
        _coalescer = new InvocationCoalescer(
            idleDebounce ?? DefaultIdleDebounce,
            maxBatchWindow ?? DefaultMaxBatchWindow);
    }

    public bool TryBecomePrimary() => _coordinator.TryBecomePrimary();

    public Task<bool> TryForwardAsync(IReadOnlyList<string> paths, CancellationToken cancellationToken)
        => _coordinator.TryForwardAsync(paths, cancellationToken);

    /// <summary>
    /// Primary only: seeds this process's own paths as the first arrivals,
    /// starts the pipe server, and pumps closed batches to the callback until
    /// disposed. Batches arriving after earlier work started become new
    /// batches — never a merge into work already underway.
    /// </summary>
    public void SeedAndPump(IReadOnlyList<string> initialPaths, Action<IReadOnlyList<string>> openBatch)
    {
        ArgumentNullException.ThrowIfNull(openBatch);
        _coalescer.Add(initialPaths);
        _coordinator.StartServer(_coalescer);
        _pump = Task.Run(() => PumpAsync(openBatch, _shutdown.Token));
    }

    public void Dispose()
    {
        _shutdown.Cancel();
        try
        {
            _pump?.Wait(TimeSpan.FromSeconds(2));
        }
        catch (AggregateException)
        {
        }

        _shutdown.Dispose();
        _coordinator.Dispose();
    }

    private async Task PumpAsync(Action<IReadOnlyList<string>> openBatch, CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            IReadOnlyList<string> batch;
            try
            {
                batch = await _coalescer.WaitForBatchAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                return;
            }

            if (batch.Count > 0)
            {
                // One batch that can't be delivered — a bad path, a window that
                // refuses to open — must not end launch handling for every batch
                // that arrives after it, so delivery failures are contained here.
                try
                {
                    openBatch(batch);
                }
                catch (Exception ex)
                {
                    Trace.TraceError("Batch delivery failed: {0}", ex);
                }
            }
        }
    }
}

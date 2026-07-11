namespace VirusTotalOneClick.Core;

/// <summary>
/// Merges near-simultaneous launches into batches: a batch closes once arrivals
/// stay quiet for the idle debounce, or unconditionally when the batch window
/// ceiling passes. Paths arriving after a batch was taken start the next batch —
/// they are never merged into work that already began.
/// </summary>
public sealed class InvocationCoalescer
{
    private readonly Lock _gate = new();
    private readonly TimeSpan _idleDebounce;
    private readonly TimeSpan _maxBatchWindow;
    private readonly Func<DateTimeOffset> _utcNow;
    private readonly Func<TimeSpan, CancellationToken, Task> _delayAsync;
    private readonly List<string> _batch = [];
    private DateTimeOffset _firstArrival;
    private DateTimeOffset _lastArrival;
    private TaskCompletionSource _arrivalSignal = NewSignal();

    public InvocationCoalescer(
        TimeSpan idleDebounce,
        TimeSpan maxBatchWindow,
        Func<DateTimeOffset>? utcNow = null,
        Func<TimeSpan, CancellationToken, Task>? delayAsync = null)
    {
        _idleDebounce = idleDebounce;
        _maxBatchWindow = maxBatchWindow;
        _utcNow = utcNow ?? (() => DateTimeOffset.UtcNow);
        _delayAsync = delayAsync ?? Task.Delay;
    }

    public void Add(IReadOnlyList<string> paths)
    {
        lock (_gate)
        {
            var now = _utcNow();
            if (_batch.Count == 0)
            {
                _firstArrival = now;
            }

            _lastArrival = now;
            foreach (var path in paths)
            {
                if (string.IsNullOrWhiteSpace(path))
                {
                    continue;
                }

                if (!_batch.Contains(path, StringComparer.OrdinalIgnoreCase))
                {
                    _batch.Add(path);
                }
            }

            _arrivalSignal.TrySetResult();
        }
    }

    public async Task<IReadOnlyList<string>> WaitForBatchAsync(CancellationToken cancellationToken)
    {
        while (true)
        {
            Task? arrival = null;
            TimeSpan wait = TimeSpan.Zero;
            lock (_gate)
            {
                if (_batch.Count == 0)
                {
                    _arrivalSignal = NewSignal();
                    arrival = _arrivalSignal.Task;
                }
                else
                {
                    var deadline = Earlier(
                        _lastArrival + _idleDebounce,
                        _firstArrival + _maxBatchWindow);
                    var now = _utcNow();
                    if (now >= deadline)
                    {
                        var batch = _batch.ToList();
                        _batch.Clear();
                        return batch;
                    }

                    wait = deadline - now;
                }
            }

            if (arrival is not null)
            {
                await arrival.WaitAsync(cancellationToken).ConfigureAwait(false);
            }
            else
            {
                await _delayAsync(wait, cancellationToken).ConfigureAwait(false);
            }
        }
    }

    private static DateTimeOffset Earlier(DateTimeOffset left, DateTimeOffset right)
        => left < right ? left : right;

    private static TaskCompletionSource NewSignal()
        => new(TaskCreationOptions.RunContinuationsAsynchronously);
}

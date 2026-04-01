namespace Minnaloushe.Core.Toolbox.TestHelpers;

public sealed class AsyncThresholdCollection<T>
{
    private readonly List<T> _items = [];
    private readonly object _lock = new();

    private TaskCompletionSource<bool> _thresholdReached =
        new(TaskCreationOptions.RunContinuationsAsynchronously);

    private int _awaitedCount;

    public int Count
    {
        get
        {
            lock (_lock)
            {
                return _items.Count;
            }
        }
    }

    public void Add(T item)
    {
        TaskCompletionSource<bool>? toRelease = null;

        lock (_lock)
        {
            _items.Add(item);

            if (_items.Count >= _awaitedCount &&
                !_thresholdReached.Task.IsCompleted)
            {
                toRelease = _thresholdReached;
            }
        }

        toRelease?.TrySetResult(true);
    }

    /// <summary>
    /// Awaits until Count >= targetCount OR waitDelay elapses (whichever comes first).
    /// Returns true if threshold reached, false if timed out.
    /// </summary>
    public async Task<bool> WaitUntilCountAtLeastAsync(
        int targetCount,
        TimeSpan waitDelay,
        CancellationToken ct = default)
    {
        Task<bool> waitTask;

        lock (_lock)
        {
            if (_items.Count >= targetCount)
            {
                return true;
            }

            _awaitedCount = targetCount;

            if (_thresholdReached.Task.IsCompleted)
            {
                _thresholdReached = new(TaskCreationOptions.RunContinuationsAsynchronously);
            }

            waitTask = _thresholdReached.Task;
        }

        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);

        var timeoutTask = Task.Delay(waitDelay, timeoutCts.Token);

        var completed = await Task.WhenAny(waitTask, timeoutTask).ConfigureAwait(false);

        if (completed == waitTask)
        {
            timeoutCts.Cancel(); // stop delay task
            return true;
        }

        ct.ThrowIfCancellationRequested();
        return false; // timed out
    }

    public IReadOnlyList<T> GetSnapshot()
    {
        lock (_lock)
        {
            return _items.ToArray();
        }
    }

    public void Clear()
    {
        lock (_lock)
        {
            _items.Clear();
        }
    }
}
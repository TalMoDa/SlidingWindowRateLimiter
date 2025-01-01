using System.Collections.Concurrent;
using System.Diagnostics;
using RateLimiter.Records;

public class RateLimiter<TArg> : IAsyncDisposable
{
    private readonly Func<TArg, Task> _action;
    private readonly Stopwatch _stopwatch = Stopwatch.StartNew();
    private readonly Dictionary<RateLimit, Queue<long>> _rateLimitActionTimestamps = new();
    private readonly SemaphoreSlim _semaphore = new(1, 1);

    public RateLimiter(Func<TArg, Task> action, params RateLimit[] rateLimits)
    {
        if (rateLimits == null || rateLimits.Length == 0)
            throw new ArgumentException("At least one rate limit must be specified.", nameof(rateLimits));

        _action = action ?? throw new ArgumentNullException(nameof(action));
        foreach (var rateLimit in rateLimits.OrderBy(x => x.Period))
        {
            _rateLimitActionTimestamps.TryAdd(rateLimit, new Queue<long>());
        }
    }

    public async Task PerformAsync(TArg argument, CancellationToken cancellationToken = default)
    {
        await _semaphore.WaitAsync(cancellationToken);
        try
        {
            var currentTicks = _stopwatch.ElapsedTicks;
            
            CleanUpOldTimestamps(currentTicks);

            var maxWaitTime = GetRateLimitWaitingTicks(currentTicks)
                .Aggregate(
                    TimeSpan.Zero,
                    (current, waitTick) =>
                    {
                        var waitTime = TimeSpan.FromSeconds((double)waitTick / Stopwatch.Frequency);
                        return waitTime > current ? waitTime : current;
                    });

            if (maxWaitTime > TimeSpan.Zero)
            {
                await Task.Delay(maxWaitTime, cancellationToken);
            }

            UpdateActionTimestamps(currentTicks);
        }
        finally
        {
            _semaphore.Release();
        }

        await _action(argument);
    }
    

    private void CleanUpOldTimestamps(long currentTicks)
    {
        foreach (var rateLimitActionTimestamp in _rateLimitActionTimestamps)
        {
            var timestamps = rateLimitActionTimestamp.Value;
            var periodTicks = (long)(rateLimitActionTimestamp.Key.Period.TotalSeconds * Stopwatch.Frequency);
            var cutoffTicks = currentTicks - periodTicks;

            while (timestamps.TryPeek(out var oldest) && oldest <= cutoffTicks)
            {
                timestamps.TryDequeue(out _);
            }
        }
        
    }

    private void UpdateActionTimestamps(long currentTicks)
    {
        foreach (var timestamps in _rateLimitActionTimestamps.Values)
        {
            timestamps.Enqueue(currentTicks);
        }
    }

    private IEnumerable<long> GetRateLimitWaitingTicks(long currentTicks)
    {
        foreach (var limit in _rateLimitActionTimestamps)
        {
            if (IsWaitingTick(limit.Key, limit.Value, currentTicks, out var ticks))
            {
                yield return ticks;
            }
        }
    }

    private bool IsWaitingTick(RateLimit limit, Queue<long> timestamps, long currentTicks, out long waitTicks)
    {

        waitTicks = 0;
        
        if (timestamps.Count < limit.MaxActions || !timestamps.TryPeek(out var oldestTimestamp))
            return false;

        var periodTicks = (long)(limit.Period.TotalSeconds * Stopwatch.Frequency);
        
        waitTicks = oldestTimestamp + periodTicks - currentTicks;

        if (waitTicks <= 0)
            return false;

        Console.WriteLine(
            $"[RateLimiter] Limit hit! Period: {limit.Period}," +
            $" MaxActions: {limit.MaxActions}," +
            $" TotalActions: {timestamps.Count}," +
            $" WaitTime: {TimeSpan.FromSeconds((double)waitTicks / Stopwatch.Frequency).TotalMilliseconds:F2} ms");
        
        return true;
    }

 

    public async ValueTask DisposeAsync()
    {
        _semaphore.Dispose();
    }
}

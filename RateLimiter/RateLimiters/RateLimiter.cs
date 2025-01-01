using System.Diagnostics;
using RateLimiter.Records;

// The rate limiter class to limit the number of actions performed in a given period 
// This is a windows based rate limiter that uses a stopwatch to measure time with high precision
public class RateLimiter<TArg> : IAsyncDisposable
{
    // The action to perform 
    private readonly Func<TArg, Task> _action;
    
    // Stopwatch to measure time with high precision
    private readonly Stopwatch _stopwatch = Stopwatch.StartNew();
    
    // Dictionary to store the rate limits and their timestamps(the history of actions executed)
    private readonly Dictionary<RateLimit, Queue<long>> _rateLimitActionTimestamps = new();
    
    // Semaphore to limit the number of concurrent actions at the same.
    private readonly SemaphoreSlim _semaphore = new(1, 1);

    // Constructor to create a RateLimiter instance
    public RateLimiter(Func<TArg, Task> action, params RateLimit[] rateLimits)
    {
        // Check if at least one rate limit is specified throw an exception if not
        if (rateLimits == null || rateLimits.Length == 0)
            throw new ArgumentException("At least one rate limit must be specified.", nameof(rateLimits));

        // Store the action to perform throw an exception if not specified 
        _action = action ?? throw new ArgumentNullException(nameof(action));
        
        // Initialize the rate limits and their timestamps ordered by the period
        foreach (var rateLimit in rateLimits.OrderBy(x => x.Period))
        {
            _rateLimitActionTimestamps.TryAdd(rateLimit, new Queue<long>());
        }
    }

    // Method to perform the action with rate limiting
    public async Task PerformAsync(TArg argument, CancellationToken cancellationToken = default)
    {
        // Wait for the semaphore to be released
        await _semaphore.WaitAsync(cancellationToken);
        try
        {
            // Get the current time in ticks
            var currentTicks = _stopwatch.ElapsedTicks;
            
            // Clean up the old timestamps that are older than the period provided in the rate limit initialization
            CleanUpOldTimestamps(currentTicks);

            // Get the maximum wait time for the rate limits to delay the action before executing it
            var maxWaitTime = GetRateLimitWaitingTicks(currentTicks)
                .Aggregate(
                    TimeSpan.Zero,
                    (current, waitTick) =>
                    {
                        var waitTime = TimeSpan.FromSeconds((double)waitTick / Stopwatch.Frequency);
                        return waitTime > current ? waitTime : current;
                    });

            // Delay the action if the maximum wait time is greater than zero
            if (maxWaitTime > TimeSpan.Zero)
            {
                await Task.Delay(maxWaitTime, cancellationToken);
            }

            // Update all the action timestamps after the delay
            UpdateActionTimestamps(currentTicks);
        }
        finally
        {
            _semaphore.Release();
        }

        await _action(argument);
    }
    

    // Method to clean up the old timestamps that are older than the period provided in the rate limit initialization
    private void CleanUpOldTimestamps(long currentTicks)
    {
        foreach (var rateLimitActionTimestamp in _rateLimitActionTimestamps)
        {
            var timestamps = rateLimitActionTimestamp.Value;
            var periodTicks = (long)(rateLimitActionTimestamp.Key.Period.TotalSeconds * Stopwatch.Frequency);
            var cutoffTicks = currentTicks - periodTicks;

            // Remove the old timestamps that are older than the period
            while (timestamps.TryPeek(out var oldest) && oldest <= cutoffTicks)
            {
                timestamps.TryDequeue(out _);
            }
        }
        
    }

    // Method to update all the action timestamps after the delay 
    private void UpdateActionTimestamps(long currentTicks)
    {
        foreach (var timestamps in _rateLimitActionTimestamps.Values)
        {
            timestamps.Enqueue(currentTicks);
        }
    }

    // Method to get the rate limit waiting ticks for all the rate limits provided
    private IEnumerable<long> GetRateLimitWaitingTicks(long currentTicks)
    {
        foreach (var limit in _rateLimitActionTimestamps)
        {
            if (IsWaitingTick(limit.Key, limit.Value, currentTicks, out var ticks))
            {
                // Return the waiting ticks for the rate limit
                yield return ticks;
            }
        }
    }

    // Method to check if the rate limit is need waiting for the ticks to delay the action
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

 

    // Method to dispose the semaphore used in the rate limiter
    public ValueTask DisposeAsync()
    {
        _semaphore.Dispose();
        return new ValueTask(Task.CompletedTask);
    }
}

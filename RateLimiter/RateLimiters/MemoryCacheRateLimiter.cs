using System.Diagnostics;
using Microsoft.Extensions.Caching.Memory;
using RateLimiter.Records;


// Rate limit for each action using a fixed window and memory cache
public class MemoryCacheRateLimiter<TArg> : IAsyncDisposable
{
    // Action to perform with rate limiting
    private readonly Func<TArg, Task> _action;

    // MemoryCache to store timestamps
    private readonly MemoryCache _cache = new(new MemoryCacheOptions());

    // Stopwatch to measure time with high precision
    private readonly Stopwatch _stopwatch = Stopwatch.StartNew();

    // Rate limit for each limit and a unique key for caching
    private readonly Dictionary<RateLimit, string> _rateLimitKeys = new();

    // SemaphoreSlim to ensure thread-safe operations
    private readonly SemaphoreSlim _semaphore = new(1, 1);

    public MemoryCacheRateLimiter(Func<TArg, Task> action, params RateLimit[] rateLimits)
    {
        // Ensure at least one rate limit is specified
        if (rateLimits == null || rateLimits.Length == 0)
            throw new ArgumentException("At least one rate limit must be specified.", nameof(rateLimits));

        // Ensure the action is not null
        _action = action ?? throw new ArgumentNullException(nameof(action));

        // Assign a unique key for each rate limit 
        // If we want later on to use redis with multiple instances of this application we can create a shared key for all instances
        // |nd all limits will be shared and honored
        foreach (var rateLimit in rateLimits)
        {
            _rateLimitKeys.Add(rateLimit, Guid.NewGuid().ToString());
        }
    }

    // Perform the action with rate limiting
    public async Task PerformAsync(TArg argument, CancellationToken cancellationToken = default)
    {
        // Wait for the semaphore to be available
        await _semaphore.WaitAsync(cancellationToken);
        try
        {
            // Check all rate limits to ensure no violation
            var delayTime = await Task.WhenAll(_rateLimitKeys.Keys
                .Select(GetWaitingTimeAsync))
                .ContinueWith(task => task.Result.Max(), cancellationToken);

            // Wait for the delay time if any rate limit is hit
            if (delayTime > TimeSpan.Zero)
            {
                Console.WriteLine($"[RateLimiter] Limit hit! Waiting for {delayTime.TotalMilliseconds:F2} ms.");
                await Task.Delay(delayTime, cancellationToken);
            }

            // Record action timestamps for all rate limits
            await RecordActionTimestampsAsync();
        }
        finally
        {
            // Release the semaphore
            _semaphore.Release();
        }

        // Execute the action
        await _action(argument);
    }

    private Task<TimeSpan> GetWaitingTimeAsync(RateLimit rateLimit)
    {
        var cacheKey = _rateLimitKeys[rateLimit];

        // Retrieve timestamps from the cache if available if not return waitTime as TimeSpan.Zero
        if(!_cache.TryGetValue<List<long>>(cacheKey, out var timestamps))
            return Task.FromResult(TimeSpan.Zero);
        
        // Check if the number of timestamps is less than the maximum allowed action 
        if (timestamps is not { Count: > 0 } || timestamps.Count < rateLimit.MaxActions)
            return Task.FromResult(TimeSpan.Zero);

        var currentTicks = _stopwatch.ElapsedTicks;
        var periodTicks = rateLimit.Period.Ticks;
        
        // Calculate wait time based on the oldest timestamp and the period it is first available in the array
        var nextAvailable = timestamps[0] + periodTicks;
        
        // set the waitTime to the next available time
        return Task.FromResult(TimeSpan.FromTicks(nextAvailable - currentTicks));
    }


    // Record the current timestamp for each rate limit
    private async Task RecordActionTimestampsAsync()
    {
        // Get the current ticks
        var currentTicks = _stopwatch.ElapsedTicks;

        // Create a list of tasks to run concurrently
        var tasks = _rateLimitKeys.Keys.Select(async rateLimit =>
        {
            // Get the cache key for the rate limit
            var cacheKey = _rateLimitKeys[rateLimit];

            // Add or update the list of timestamps in the cache
            var timestamps = await _cache.GetOrCreateAsync(cacheKey, entry =>
            {
                entry.AbsoluteExpirationRelativeToNow = rateLimit.Period; // Sliding expiration based on the period
                return Task.FromResult(new List<long>());
            });

            timestamps?.Add(currentTicks);
        });

        // Wait for all tasks to complete
        await Task.WhenAll(tasks);
    }
    
    public ValueTask DisposeAsync()
    {
        _semaphore.Dispose();
        _cache.Dispose();
        return new ValueTask(Task.CompletedTask);
    }
}

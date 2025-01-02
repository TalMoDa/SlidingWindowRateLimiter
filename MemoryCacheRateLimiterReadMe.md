# MemoryCache RateLimiter README

## Purpose

The `MemoryCacheRateLimiter` class regulates the execution of actions (`Func<TArg, Task>`) based on multiple rate limits. This ensures that:
- All rate limits are respected.
- Execution is delayed as needed to meet the constraints of the rate limits.
- Concurrent calls are handled safely across multiple threads.

This implementation supports **sliding window rate limiting** and uses **MemoryCache** for efficient memory management and auto-expiration of entries.

---

## Features

1. **Rate Limiting**:
    - Supports multiple rate limits (e.g., 10 calls per second, 100 calls per minute, 1000 calls per day).

2. **Sliding Window**:
    - Dynamically enforces rate limits over a rolling time window for smoother control of requests.

3. **Memory Efficiency**:
    - Uses `MemoryCache` to store and automatically expire timestamps, reducing manual cleanup.

4. **Thread Safety**:
    - Thread-safe operations with `SemaphoreSlim`.

5. **Asynchronous Execution**:
    - Fully asynchronous design ensures non-blocking behavior and scalability.

---

## Sliding Window vs Absolute Window

### Sliding Window
#### **Pros**:
- Real-time enforcement of rate limits.
- Prevents bursts of requests.
- Works well for real-time traffic patterns.

#### **Cons**:
- Slightly more complex to implement.
- Requires storing individual timestamps.

---

## Key Methods

### `PerformAsync(TArg argument)`
- Executes the action while ensuring compliance with all rate limits.
- Calculates the necessary delay, applies it, and updates timestamps after execution.

### `GetWaitingTimeAsync(RateLimit rateLimit)`
- Checks if the action violates a specific rate limit.
- Calculates the required wait time, if any, to honor the rate limit.

---

## Previous Implementation: Manual Timestamp Management

### How It Worked
- Timestamps for each action were stored in a `Dictionary<RateLimit, Queue<long>>`.
- Expired timestamps were removed manually to enforce the sliding window logic.

### Limitations
- **Manual Cleanup**: Required additional logic to remove expired timestamps.
- **Memory Usage**: Queues for each rate limit could grow in size over time.
- **Thread Safety**: Required synchronization for concurrent access to shared data structures.

---

## New Implementation: MemoryCache Integration

### How It Works
- Timestamps are stored in **MemoryCache** with an expiration time (`AbsoluteExpirationRelativeToNow`).
- Expired entries are automatically removed, eliminating the need for manual cleanup.
- Cache operations are asynchronous, enabling better scalability under concurrent access.

### Why It Was Made
- To simplify the codebase and reduce the need for manual memory management.
- To leverage the built-in capabilities of `MemoryCache` for auto-expiration.
- To improve scalability and performance in high-concurrency scenarios.

---

### Key Benefits of the New Implementation

| **Aspect**              | **Previous Implementation**                                       | **New Implementation**                                   |
|--------------------------|-------------------------------------------------------------------|---------------------------------------------------------|
| **Memory Management**    | Manual cleanup of expired timestamps.                           | Automatic expiration via `MemoryCache`.                |
| **Concurrency Handling** | Potential race conditions in manual synchronization.            | Full thread-safety using `SemaphoreSlim`.              |
| **Scalability**          | Synchronous operations could block threads.                     | Asynchronous operations enable better scalability.      |
| **Code Simplicity**      | Complex logic for managing timestamps and cleanup.              | Simplified by offloading expiration to `MemoryCache`.   |

---

## New Section: MemoryCache Advantages

### 1. **Automatic Expiration**
- Entries in the `MemoryCache` automatically expire after the specified period, removing the need for manual cleanup logic.

### 2. **Asynchronous Cache Operations**
- Operations like `GetOrCreateAsync` ensure non-blocking interactions with the cache.

### 3. **Efficient Memory Usage**
- Expired entries are removed automatically, ensuring the cache only holds active data.

### 4. **Thread Safety**
- The `MemoryCache` ensures thread-safe access to cache entries, eliminating the need for additional synchronization mechanisms.

### Example of Cache-Based Sliding Window
```csharp
var timestamps = await _cache.GetOrCreateAsync(cacheKey, entry =>
{
    entry.AbsoluteExpirationRelativeToNow = rateLimit.Period; // Auto-expire after the period
    return Task.FromResult(new List<long>());
});

timestamps?.Add(currentTicks);
```

---

## Testing

The updated implementation was rigorously tested to ensure:
1. Accurate enforcement of multiple rate limits.
2. Thread safety in high-concurrency scenarios.
3. Proper expiration and cleanup of entries using `MemoryCache`.

---

This enhanced version of `RateLimiter` simplifies the codebase, reduces memory overhead, and ensures robust performance under high-concurrency workloads, making it more reliable and maintainable.

Let me know if further clarifications are needed!
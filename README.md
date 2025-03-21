# RateLimiter Assignment

## Purpose

The task involves creating a `RateLimiter` class in C#. The purpose of this class is to:

- Be initialized with a `Func` (representing an action to execute) and multiple rate limits.
- Provide a method `Perform(TArg argument)` to perform the action while ensuring that all the provided rate limits are honored.
- Delay execution until all rate limits can be honored if needed.
- Accommodate concurrent calls from multiple threads.

For example, the passed function might represent a call to an external API, and the rate limits could be:

- 10 calls per second,
- 100 calls per minute,
- 1000 calls per day.

## Requirements

1. The `RateLimiter` must:

    - Ensure compliance with all provided rate limits.
    - Delay execution if necessary to honor the rate limits.
    - Be thread-safe, supporting calls from multiple threads simultaneously.

2. Approaches to Rate Limiting:

    - **Sliding Window**: Ensures no more than a specified number of actions (e.g., 10) were executed in the last defined period (e.g., 24 hours), even if the call is made in the middle of the day.
    - **Absolute Window**: Ensures no more than a specified number of actions (e.g., 10) were executed since the start of the defined period (e.g., since 00:00).

   The choice between these approaches must be explained (pros and cons).

3. Constraints:

    - Do not use or consult external libraries for the implementation.

4. Deliverables:

    - The implemented `RateLimiter` class.
    - A README file explaining the implementation and approach chosen.

5. Testing:

    - Ensure that the `RateLimiter` works as expected with multiple threads and honors all rate limits accurately.

## Implementation Details

### Approach Chosen: Sliding Window

#### Why Sliding Window?

Sliding Window dynamically enforces rate limits based on the most recent time period. This approach provides smoother and more precise control over requests.

### Pros of Sliding Window:

- **Real-time Control:** Tracks the last `n` actions and dynamically adjusts, ensuring better accuracy.
- **Prevents Spikes:** Avoids sudden bursts of requests by spreading them evenly.
- **Flexible:** Works well in real-time scenarios where requests vary.

### Cons of Sliding Window:

- **More Complex:** Requires managing individual timestamps for each request.
- **Memory Usage:** Needs storage for recent action timestamps, which can grow for high traffic.

#### Why Not Absolute Window?

The Absolute Window approach resets limits at fixed intervals (e.g., midnight). While simpler, it has significant drawbacks.

### Pros of Absolute Window:

- **Simple:** Easier to implement and manage.
- **Predictable:** Fixed intervals make it easier to understand.

### Cons of Absolute Window:

- **Traffic Spikes:** Allows bursts right after the reset period, which can overload systems.
- **Less Flexible:** Cannot adapt to real-time traffic variations.
- **Inefficient:** Creates uneven request distributions.

Sliding Window was chosen because it offers real-time adaptability and smoother rate control, which aligns better with dynamic systems.

## Implementation Highlights

1. **Thread Safety:**

    - A `SemaphoreSlim` ensures safe access to shared resources when the rate limiter is used across multiple threads.

2. **Rate Limits:**

    - Rate limits are defined as `RateLimit` objects, with each specifying a `Period` (e.g., 1 second) and `MaxActions` (e.g., 10 calls).

3. **High-Precision Timing:**

    - A `Stopwatch` provides accurate time measurements, ensuring precise tracking of request timestamps.

4. **Sliding Window Logic:**

    - Each rate limit maintains a queue of timestamps representing recent actions. Old timestamps are removed dynamically, ensuring compliance with the defined period.

5. **Delay Mechanism:**

    - The system calculates the maximum required delay across all rate limits and applies it to ensure all limits are honored.

### Key Methods

- **`PerformAsync(TArg argument)`**:

    - Executes the action while ensuring compliance with all rate limits.
    - Calculates the required delay, applies it, and updates action timestamps.

- **`CleanUpOldTimestamps(long currentTicks)`**:

    - Removes timestamps older than the rate limit period from the queue.

- **`GetRateLimitWaitingTicks(long currentTicks)`**:

    - Calculates the delay required to honor each rate limit.

- **`IsWaitingTick`**:

    - Checks if a delay is needed for a specific rate limit and calculates the required wait time.

### Usage Example

```csharp
var multiThreadedRunner = new MultiThreadedRunner();
await multiThreadedRunner.Run();
```

### Sample Output

```
Starting Rate-Limited Calls...
Executed 0
Executed 1
...
Executed 9
[RateLimiter] Limit hit! Period: 00:00:01, MaxActions: 10, TotalActions: 10, WaitTime: 962.33 ms
...
Completed Rate-Limited Calls.
```

## Testing

The `RateLimiter` implementation was tested with:

- Multiple threads executing actions concurrently.
- Multiple rate limits with varying periods and maximum actions.

The implementation ensures:

- Accurate enforcement of rate limits.
- Thread safety under high concurrency.


using RateLimiter.Records;

namespace RateLimiter.Runners;

// This class demonstrates how to use the WindowRateLimiter class to rate-limit calls to a function with multiple threads.
public class MultiThreadedRunner
{
    public async Task Run()
    {
        Console.WriteLine("Starting Rate-Limited Calls...");

        // Create a new WindowRateLimiter instance
        await using var limiter = new RateLimiter<int>(
            // the function to be rate-limited (in this case, a simple async lambda that writes to the console)
            i =>
            {
                // Write to the console to demonstrate that the function is being executed
                Console.WriteLine($"Executed {i}");
                return Task.CompletedTask;
            },
            // the rate limits to apply this is params array of tuples with period and limit
            rateLimits: 
            [
               new RateLimit( TimeSpan.FromSeconds(1), 10),
               new RateLimit( TimeSpan.FromMinutes(1), 100),
               new RateLimit( TimeSpan.FromDays(1), 1000),
            ]
        );
    
        try
        {
            // Parallel execution of 9999 tasks
            var tasks = Enumerable.Range(0, 9999).Select(i => limiter.PerformAsync(i));
            await Task.WhenAll(tasks);
        
            Console.WriteLine("Completed Rate-Limited Calls.");

        }
        catch (Exception e)
        {
            Console.WriteLine(e);
            throw;
        }
    }
}
namespace RateLimiter;

public class MultiThreadedRunner
{
    public async Task Run()
    {
        Console.WriteLine("Starting Rate-Limited Calls...");

        await using var limiter = new RateLimiter<int>(
            // the function to be rate-limited
            async i =>
            {
                // Simulate some work with a delay
                await Task.Delay(150);
                Console.WriteLine($"Executed {i}");
            },
            // the rate limits to apply this is params array of tuples with period and limit
            rateLimits: 
            [
                ( TimeSpan.FromSeconds(1), 10),
                ( TimeSpan.FromMinutes(1), 100),
                ( TimeSpan.FromDays(1), 1000),
            ]
        );
    
        try
        {
            // Parallel execution
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
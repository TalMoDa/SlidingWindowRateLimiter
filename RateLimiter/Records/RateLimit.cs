namespace RateLimiter.Records;

public record struct RateLimit(TimeSpan Period, int MaxActions);
using Auth.API.Options;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

namespace Auth.API.Services;

public sealed class EmailRateLimiter(IMemoryCache cache, IOptions<EmailOptions> emailOptions)
{
    private readonly EmailOptions _options = emailOptions.Value;

    public bool TryAcquire(string bucketKey)
    {
        var key = $"email-rate:{bucketKey}";
        var count = cache.GetOrCreate(key, entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(_options.RateLimitWindowMinutes);
            return 0;
        });

        if (count >= _options.RateLimitMaxAttempts)
            return false;

        cache.Set(key, count + 1, TimeSpan.FromMinutes(_options.RateLimitWindowMinutes));
        return true;
    }
}

namespace RaceIQ.Infrastructure.Strava;

/// Strava's rate limit (100 req/15 min) applies to the whole registered app, not
/// per user — this must be a singleton shared by every Strava call the app makes.
public class StravaRequestThrottle
{
    private const int MaxRequestsPerWindow = 90; // buffer below Strava's 100 limit
    private static readonly TimeSpan Window = TimeSpan.FromMinutes(15);

    private readonly Queue<DateTime> _recentRequests = new();
    private readonly SemaphoreSlim _lock = new(1, 1);

    public async Task WaitForSlotAsync()
    {
        while (true)
        {
            TimeSpan? waitTime = null;

            await _lock.WaitAsync();
            try
            {
                var cutoff = DateTime.UtcNow - Window;
                while (_recentRequests.Count > 0 && _recentRequests.Peek() < cutoff)
                    _recentRequests.Dequeue();

                if (_recentRequests.Count < MaxRequestsPerWindow)
                {
                    _recentRequests.Enqueue(DateTime.UtcNow);
                    return;
                }

                waitTime = _recentRequests.Peek() + Window - DateTime.UtcNow;
            }
            finally
            {
                _lock.Release();
            }

            if (waitTime is { } delay && delay > TimeSpan.Zero)
                await Task.Delay(delay);
        }
    }
}

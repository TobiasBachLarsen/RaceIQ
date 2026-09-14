using RaceIQ.Application;
using RaceIQ.Domain;

namespace RaceIQ.UnitTests.Fakes;

public class FakeProviderSyncService : IProviderSyncService
{
    private readonly Func<string, Task>? _onSync;

    public FakeProviderSyncService(ConnectedAccountProvider provider, Func<string, Task>? onSync = null)
    {
        Provider = provider;
        _onSync = onSync;
    }

    public ConnectedAccountProvider Provider { get; }

    public int SyncCallCount { get; private set; }

    public async Task SyncAsync(string userId)
    {
        SyncCallCount++;
        if (_onSync is not null)
            await _onSync(userId);
    }
}

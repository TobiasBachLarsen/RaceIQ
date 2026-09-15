using Microsoft.Extensions.Logging;
using RaceIQ.Application;
using RaceIQ.Domain;

namespace RaceIQ.Infrastructure.ZwiftPower;

public class ZwiftPowerSyncService : ResultSyncServiceBase<ZwiftPowerRaceResult>
{
    private readonly IZwiftPowerApiClient _apiClient;

    public ZwiftPowerSyncService(
        IZwiftPowerApiClient apiClient,
        IConnectedAccountRepository accountRepository,
        RaceResultImporter importer,
        ILogger<ZwiftPowerSyncService> logger)
        : base(accountRepository, importer, logger)
    {
        _apiClient = apiClient;
    }

    public override ConnectedAccountProvider Provider => ConnectedAccountProvider.ZwiftPower;

    protected override Task<IReadOnlyList<ZwiftPowerRaceResult>> FetchAsync(ConnectedAccount account) =>
        _apiClient.GetRecentResultsAsync(account.AccessToken, account.ExternalAccountId!);

    protected override RaceResult Project(ZwiftPowerRaceResult result, string userId) => new()
    {
        UserId = userId,
        Provider = ConnectedAccountProvider.ZwiftPower,
        ProviderResultId = result.RaceId,
        EventName = result.EventName,
        EventDate = result.EventDate,
        Category = result.Category,
        Position = result.Position,
        Duration = result.Duration
    };
}

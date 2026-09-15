using Microsoft.Extensions.Logging;
using RaceIQ.Application;
using RaceIQ.Domain;

namespace RaceIQ.Infrastructure.ZwiftRacing;

public class ZwiftRacingSyncService : ResultSyncServiceBase<ZwiftRacingRaceResult>
{
    private readonly IZwiftRacingApiClient _apiClient;

    public ZwiftRacingSyncService(
        IZwiftRacingApiClient apiClient,
        IConnectedAccountRepository accountRepository,
        RaceResultImporter importer,
        ILogger<ZwiftRacingSyncService> logger)
        : base(accountRepository, importer, logger)
    {
        _apiClient = apiClient;
    }

    public override ConnectedAccountProvider Provider => ConnectedAccountProvider.ZwiftRacing;

    protected override Task<IReadOnlyList<ZwiftRacingRaceResult>> FetchAsync(ConnectedAccount account) =>
        _apiClient.GetRecentResultsAsync(account.AccessToken, account.ExternalAccountId!);

    protected override RaceResult Project(ZwiftRacingRaceResult result, string userId) => new()
    {
        UserId = userId,
        Provider = ConnectedAccountProvider.ZwiftRacing,
        ProviderResultId = result.RaceId,
        EventName = result.EventTitle,
        EventDate = result.EventTime,
        Category = result.Category,
        Position = result.Position,
        FieldSize = result.FieldSize,
        Duration = result.Duration,
        RatingChange = result.RatingDelta
    };
}

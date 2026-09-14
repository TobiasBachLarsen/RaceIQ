using Microsoft.Extensions.Logging;
using RaceIQ.Application;
using RaceIQ.Application.Exceptions;
using RaceIQ.Domain;

namespace RaceIQ.Infrastructure.ZwiftPower;

public class ZwiftPowerSyncService : IProviderSyncService
{
    private readonly IZwiftPowerApiClient _apiClient;
    private readonly IConnectedAccountRepository _accountRepository;
    private readonly RaceResultImporter _importer;
    private readonly ILogger<ZwiftPowerSyncService> _logger;

    public ConnectedAccountProvider Provider => ConnectedAccountProvider.ZwiftPower;

    public ZwiftPowerSyncService(
        IZwiftPowerApiClient apiClient,
        IConnectedAccountRepository accountRepository,
        RaceResultImporter importer,
        ILogger<ZwiftPowerSyncService> logger)
    {
        _apiClient = apiClient;
        _accountRepository = accountRepository;
        _importer = importer;
        _logger = logger;
    }

    public async Task SyncAsync(string userId)
    {
        var account = await _accountRepository.GetAsync(userId, ConnectedAccountProvider.ZwiftPower)
            ?? throw new ConnectedAccountNotFoundException(userId);

        if (account.Status == ConnectedAccountStatus.NeedsReconnect)
            return;

        IReadOnlyList<ZwiftPowerRaceResult> results;
        try
        {
            results = await _apiClient.GetRecentResultsAsync(account.AccessToken, account.ExternalAccountId!);
        }
        catch (Exception ex)
        {
            // Covers both a real network failure (HttpRequestException) and an expired
            // session that HttpClient's default redirect-following turns into an HTTP 200
            // login-page response: EnsureSuccessStatusCode() passes on that, and the
            // subsequent ReadFromJsonAsync<T> call is what actually throws (JsonException,
            // NotSupportedException depending on the returned content-type, or
            // TaskCanceledException on a slow/broken redirect chain). Any of these shapes
            // means the credential can no longer be used, so treat them the same way.
            _logger.LogWarning(ex,
                "ZwiftPower sync failed for user {UserId}; flipping account to NeedsReconnect", userId);
            account.Status = ConnectedAccountStatus.NeedsReconnect;
            await _accountRepository.UpsertAsync(account);
            return;
        }

        var incoming = results.Select(result => new RaceResult
        {
            UserId = userId,
            Provider = ConnectedAccountProvider.ZwiftPower,
            ProviderResultId = result.RaceId,
            EventName = result.EventName,
            EventDate = result.EventDate,
            Category = result.Category,
            Position = result.Position,
            Duration = result.Duration
        }).ToList();

        await _importer.ImportAsync(userId, incoming);
    }
}

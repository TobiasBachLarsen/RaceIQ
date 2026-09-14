using RaceIQ.Application;
using RaceIQ.Application.Exceptions;
using RaceIQ.Domain;

namespace RaceIQ.Infrastructure.ZwiftRacing;

public class ZwiftRacingSyncService : IProviderSyncService
{
    private readonly IZwiftRacingApiClient _apiClient;
    private readonly IConnectedAccountRepository _accountRepository;
    private readonly RaceResultImporter _importer;

    public ConnectedAccountProvider Provider => ConnectedAccountProvider.ZwiftRacing;

    public ZwiftRacingSyncService(
        IZwiftRacingApiClient apiClient,
        IConnectedAccountRepository accountRepository,
        RaceResultImporter importer)
    {
        _apiClient = apiClient;
        _accountRepository = accountRepository;
        _importer = importer;
    }

    public async Task SyncAsync(string userId)
    {
        var account = await _accountRepository.GetAsync(userId, ConnectedAccountProvider.ZwiftRacing)
            ?? throw new ConnectedAccountNotFoundException(userId);

        if (account.Status == ConnectedAccountStatus.NeedsReconnect)
            return;

        IReadOnlyList<ZwiftRacingRaceResult> results;
        try
        {
            results = await _apiClient.GetRecentResultsAsync(account.AccessToken, account.ExternalAccountId!);
        }
        catch (Exception)
        {
            // Covers both a real network failure (HttpRequestException) and an expired
            // API key that HttpClient's default redirect-following turns into an HTTP 200
            // login-page response: EnsureSuccessStatusCode() passes on that, and the
            // subsequent ReadFromJsonAsync<T> call is what actually throws (JsonException,
            // NotSupportedException depending on the returned content-type, or
            // TaskCanceledException on a slow/broken redirect chain). Any of these shapes
            // means the credential can no longer be used, so treat them the same way.
            account.Status = ConnectedAccountStatus.NeedsReconnect;
            await _accountRepository.UpsertAsync(account);
            return;
        }

        var incoming = results.Select(result => new RaceResult
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
        }).ToList();

        await _importer.ImportAsync(userId, incoming);
    }
}
